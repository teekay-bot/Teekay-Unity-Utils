using System;
using System.Collections.Generic;
using UnityEngine;

// The console's class name matches its namespace (TeekayUtils.DevConsole.DevConsole), so an
// unqualified `DevConsole` here would resolve to the namespace and fail to compile.
using DevConsoleApi = TeekayUtils.DevConsole.DevConsole;

namespace TeekayUtils
{
    /// <summary>
    /// The one place debug overlays are rendered. Systems implement <see cref="IDebugDrawable"/>,
    /// register here, and describe their measured state once per frame; this hub owns every surface
    /// it appears on — GL lines over the Game view (and builds), gizmos in the Scene view, IMGUI for
    /// the labels — plus the console toggles that switch them on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists because that plumbing is identical for every overlay and easy to get subtly wrong:
    /// resolving a camera and attaching <see cref="GLDebugDrawRenderer"/> (URP never calls
    /// <c>OnPostRender</c>), subscribing and unsubscribing the draw event, keeping gizmos out of the
    /// Game view where the GL pass already drew the same lines, running late enough that the facts
    /// are this frame's, and projecting label positions by hand. Written once here, every system
    /// that draws gets it right; written per overlay, it was copied verbatim and drifted.
    /// </para>
    /// <para>
    /// Auto-created on first registration in Play mode (no scene setup, nothing to forget), and
    /// nothing about it exists in a non-development build — see <see cref="Register"/>.
    /// </para>
    /// </remarks>
    // After everything that produces facts. Overlays read state their system computed this frame
    // (an Interactor's freshly scored buffer, a motor's grounding verdict), so collection has to be
    // the last thing to happen — this is the whole reason overlays used to carry an execution-order
    // attribute of their own.
    [DefaultExecutionOrder(10000)]
    [AddComponentMenu("")] // auto-created; not something to add by hand
    public sealed class DebugDrawHub : PersistentSingleton<DebugDrawHub>, IDebugLabelSink
    {
        /// <summary>Console variable prefix, so `debugdraw` + Tab lists every overlay switch.</summary>
        public const string CVarPrefix = "debugdraw";

        // Development builds and the editor default to ON so a registered overlay's own toggle is
        // the only switch that matters. A release build has no hub at all, but the field still
        // compiles, and defaulting it to false means nothing draws even if one is constructed.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const bool DefaultEnabled = true;
#else
        const bool DefaultEnabled = false;
#endif

        const int LabelFontSize = 12;

        // A box, not a drop shadow. Debug labels land on arbitrary scenery and, worse, on the overlay's
        // own wireframes — and a 1px shadow behind bold text is no contrast at all against lines of
        // similar luminance, which is what made annotated shapes unreadable exactly where they were
        // busiest. An opaque-enough plate wins against anything underneath.
        // Nearly opaque on purpose. The plate's job is to OCCLUDE, and the overlay's own wireframes are
        // saturated green and yellow — at 0.82 the ~18% that bled through was still bright enough to
        // read as lines crossing the text. What is left of the alpha only softens the edge against the
        // scene.
        static readonly Color LabelFill = new Color(0.05f, 0.05f, 0.07f, 0.94f);
        static readonly Color LabelOutline = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color LabelText = Color.white;
        static readonly Color LabelStem = new Color(1f, 1f, 1f, 0.35f);
        const float LabelCornerRadius = 4f;
        const float LabelStemWidth = 1f;

        /// <summary>
        /// Master switch over every overlay — the `debugdraw` console variable. Off means no
        /// drawable is even asked to draw.
        /// </summary>
        public static bool Enabled { get; set; } = DefaultEnabled;

        readonly List<IDebugDrawable> _drawables = new List<IDebugDrawable>(8);
        readonly DebugDrawBuffer _buffer = new DebugDrawBuffer();
        readonly List<(Vector3 Position, string Text)> _labels = new List<(Vector3, string)>(32);

        // Reused every frame on both surfaces: projection and placement are per-view (the Scene view
        // and the Game view see the same world from different cameras), the collected facts are not.
        readonly List<DebugLabelLayout.Request> _labelRequests = new List<DebugLabelLayout.Request>(32);
        readonly List<DebugLabelLayout.Placed> _placedLabels = new List<DebugLabelLayout.Placed>(32);

#if UNITY_EDITOR
        readonly GizmosDebugDrawer _gizmos = new GizmosDebugDrawer();
#endif

        GLDebugDrawRenderer _renderer;
        Camera _camera;
        GUIStyle _labelStyle;
        bool _warnedNoCamera;

        /// <summary>
        /// Starts drawing <paramref name="drawable"/>. Call from <c>OnEnable</c> and pair with
        /// <see cref="Unregister"/> in <c>OnDisable</c>.
        /// <para>
        /// Compiled away outside the editor and development builds: a release player creates no hub,
        /// spawns no console, and calls no draw code, so an overlay costs a shipped game nothing.
        /// </para>
        /// </summary>
        public static void Register(IDebugDrawable drawable)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Play mode only: OnEnable also runs in the editor while authoring a scene, and
            // auto-creating a hub there would spawn a GameObject into the user's scene.
            if (drawable == null || !Application.isPlaying) return;

            DebugDrawHub hub = Instance;
            if (hub != null && !hub._drawables.Contains(drawable)) hub._drawables.Add(drawable);
#endif
        }

        /// <summary>Stops drawing <paramref name="drawable"/>.</summary>
        public static void Unregister(IDebugDrawable drawable)
        {
            // HasInstance, not Instance: this runs during teardown, where touching the lazy
            // property would resurrect the singleton just to remove something from it.
            if (!HasInstance) return;

            instance._drawables.Remove(drawable);
        }

        /// <summary>
        /// Exposes an overlay's toggle as the console variable <c>debugdraw.{name}</c>. Pass
        /// accessors for the field that already holds the flag — the console must READ and WRITE
        /// that field, not shadow it with a copy of its own, or the Inspector checkbox and the
        /// console end up disagreeing about what is on.
        /// </summary>
        /// <remarks>
        /// Deliberately not a persistent variable: a persistent one restores its saved value at
        /// registration, overwriting whatever the component serialized — two sources of truth for
        /// one flag. Serialized defaults belong to the scene; console edits last for the session.
        /// </remarks>
        public static void RegisterToggle(string name, string description, Func<bool> get, Action<bool> set)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (string.IsNullOrWhiteSpace(name) || get == null || set == null) return;

            DevConsoleApi.RegisterBool($"{CVarPrefix}.{name}", description, get, set);
#endif
        }

        /// <summary>
        /// Drops a toggle registered by <see cref="RegisterToggle"/>. Console variable names are
        /// global, so an object that owns some must release them when it goes away — a variable
        /// still bound to a disabled component answers with values nobody can see, which reads as a
        /// broken toggle rather than an unowned one.
        /// </summary>
        public static void UnregisterToggle(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (string.IsNullOrWhiteSpace(name)) return;

            DevConsoleApi.Unregister($"{CVarPrefix}.{name}");
#endif
        }

        protected override void Awake()
        {
            base.Awake();
            if (instance != this) return; // duplicate; the base already destroyed it

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevConsoleApi.RegisterBool(CVarPrefix, "Master switch for every built-in debug overlay.",
                () => Enabled, value => Enabled = value);
#endif
        }

        protected override void OnDestroy()
        {
            if (_renderer != null) _renderer.Drawing -= OnRendererDrawing;
            base.OnDestroy();
        }

#if UNITY_EDITOR
        // Labels are drawn from the Scene view's own GUI pass rather than from OnDrawGizmos, because
        // that pass runs AFTER the view has rendered its contents — so a plate is guaranteed to sit on
        // top of every gizmo, including ones this hub does not draw (the selected object's collider
        // wireframe, other components' OnDrawGizmos). Ordering between gizmo callbacks is not
        // something a drawable can control, so the fix is to stop depending on it.
        void OnEnable() => UnityEditor.SceneView.duringSceneGui += OnSceneGui;

        void OnDisable() => UnityEditor.SceneView.duringSceneGui -= OnSceneGui;

        void OnSceneGui(UnityEditor.SceneView sceneView)
        {
            // Repaint only: every other event type is bookkeeping, and drawing during those is how
            // IMGUI ends up reporting mismatched layout.
            if (!Enabled || _labels.Count == 0 || Event.current.type != EventType.Repaint) return;

            var bounds = new Rect(0f, 0f, sceneView.position.width, sceneView.position.height);

            // Opens a 2D GUI block inside the handle context, the documented pairing for
            // HandleUtility.WorldToGUIPoint.
            UnityEditor.Handles.BeginGUI();
            DrawLabels(sceneView: true, bounds);
            UnityEditor.Handles.EndGUI();
        }
#endif

        // The single collection pass. Everything else in this class only replays what it produced.
        void LateUpdate()
        {
            _buffer.Clear();
            _labels.Clear();

            if (!Enabled) return;

            ResolveRenderer();

            // Backwards so a drawable destroyed without unregistering (scene unload, a killed
            // character) can be dropped mid-walk.
            for (int i = _drawables.Count - 1; i >= 0; i--)
            {
                IDebugDrawable drawable = _drawables[i];

                // A destroyed MonoBehaviour behind an interface reference is NOT null — the check
                // has to go through Unity's own comparison or this NREs a frame after a despawn.
                if (drawable.IsUnityNull())
                {
                    _drawables.RemoveAt(i);
                    continue;
                }

                if (!drawable.DebugEnabled) continue;

                drawable.DrawDebug(_buffer, this);
            }
        }

        /// <summary>
        /// Finds the camera the Game-view lines go on. Retried until it succeeds: a scene can load
        /// its camera later than the character that registered, and <c>Camera.main</c> changes when
        /// scenes do.
        /// </summary>
        void ResolveRenderer()
        {
            if (_renderer != null) return;

            Camera main = Camera.main;
            if (main == null)
            {
                if (!_warnedNoCamera)
                {
                    _warnedNoCamera = true;
                    Debug.LogWarning("[DebugDrawHub] No Camera.main — debug overlays will draw in the " +
                                     "Scene view only. Tag a camera as MainCamera for Game-view lines.");
                }

                return;
            }

            _camera = main;
            _renderer = main.GetOrAdd<GLDebugDrawRenderer>();
            _renderer.Drawing += OnRendererDrawing;
            _warnedNoCamera = false;
        }

        /// <summary>Runs inside the renderer's open GL block — replay only, no GL.Begin/End here.</summary>
        void OnRendererDrawing(IDebugDrawer drawer) => _buffer.Replay(drawer);

        void OnGUI()
        {
            if (!Enabled || _labels.Count == 0 || _camera == null) return;

            DrawLabels(sceneView: false, new Rect(0f, 0f, Screen.width, Screen.height));
        }

        /// <summary>
        /// The one label renderer, shared by the Game view and the Scene view. Only the projection
        /// differs between them; everything the eye judges — the plate, the font, the placement, the
        /// deduping — has to be identical, or the same overlay reads as two different tools.
        /// <para>
        /// This is why the Scene view no longer calls <c>Handles.Label(position, text)</c>: that
        /// overload takes "the label style from the current GUISkin" (per its own documentation),
        /// which is unstyled, unboxed, left-anchored text — the worst possible surface for a label
        /// that lands on top of the wireframes this hub just drew.
        /// </para>
        /// </summary>
        /// <param name="bounds">
        /// The visible area in GUI space, measured by the CALLER. Each surface knows its own extent
        /// before the GUI block opens; asking mid-draw would mean guessing which camera is current.
        /// </param>
        void DrawLabels(bool sceneView, Rect bounds)
        {
            GUIStyle style = LabelStyle;
            Color previousColor = GUI.color;
            GUI.color = Color.white; // the plate and the text carry their own colours

            _labelRequests.Clear();

            foreach ((Vector3 position, string text) in _labels)
            {
                if (!TryProjectToGui(position, sceneView, out Vector2 anchor)) continue;

                _labelRequests.Add(new DebugLabelLayout.Request
                {
                    Anchor = anchor,
                    Size = style.CalcSize(new GUIContent(text)),
                    Text = text
                });
            }

            DebugLabelLayout.Arrange(_labelRequests, _placedLabels, bounds);

            for (int i = 0; i < _placedLabels.Count; i++) DrawLabel(_placedLabels[i], style);

            GUI.color = previousColor;
        }

        /// <summary>
        /// Built lazily rather than in a field initialiser: <c>GUI.skin</c> is only valid inside a GUI
        /// block, and the text colour has to be forced because the two skins disagree — the editor's
        /// label is near-black, which would vanish on the dark plate.
        /// </summary>
        GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle != null) return _labelStyle;

                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = LabelFontSize,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                    // The plate is sized from CalcSize, which includes padding — so this is what
                    // separates the text from the plate's edge.
                    padding = new RectOffset(6, 6, 3, 4)
                };
                _labelStyle.normal.textColor = LabelText;

                return _labelStyle;
            }
        }

        /// <summary>
        /// World position to GUI point (y growing downward), returning false for anything the view
        /// cannot show.
        /// </summary>
        bool TryProjectToGui(Vector3 worldPosition, bool sceneView, out Vector2 guiPoint)
        {
#if UNITY_EDITOR
            if (sceneView)
            {
                // Already in GUI space and already flipped, and it follows the scene camera the
                // gizmo pass is drawing for — projecting by hand here would be a second, disagreeing
                // version of what Handles already knows.
                Vector3 sceneGui = UnityEditor.HandleUtility.WorldToGUIPointWithDepth(worldPosition);
                guiPoint = new Vector2(sceneGui.x, sceneGui.y);

                return sceneGui.z > 0f; // z is the distance from the camera
            }
#endif
            Vector3 screen = _camera.WorldToScreenPoint(worldPosition);
            guiPoint = new Vector2(screen.x, Screen.height - screen.y);

            return screen.z > 0f;
        }

        static void DrawLabel(in DebugLabelLayout.Placed label, GUIStyle style)
        {
            Rect box = label.Box;

            // A box that had to move no longer sits on the thing it describes, so it gets a stem back
            // to it. Without one the numbers appear to belong to whatever the box now hovers over,
            // which is the one failure mode worse than an unreadable label.
            if (label.Displaced)
            {
                float stemHeight = label.Anchor.y - box.yMax;
                if (stemHeight > 0f)
                    GUI.DrawTexture(new Rect(label.Anchor.x, box.yMax, LabelStemWidth, stemHeight),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, LabelStem,
                        Vector4.zero, 0f);
            }

            // borderWidths zero fills the rect, non-zero strokes it — the same call does plate and
            // outline, and it is the only IMGUI path that rounds corners at all.
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, LabelFill,
                Vector4.zero, LabelCornerRadius);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, LabelOutline,
                Vector4.one, LabelCornerRadius);

            GUI.Label(box, label.Text, style);
        }

        public void Label(Vector3 worldPosition, string text)
        {
            if (!string.IsNullOrEmpty(text)) _labels.Add((worldPosition, text));
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // The Game view draws gizmos too when its Gizmos toggle is on, and there the GL pass
            // plus OnGUI have already drawn this exact overlay — every line and label would double.
            // The Scene view is the only surface gizmos are responsible for.
            if (Camera.current == null || Camera.current.cameraType != CameraType.SceneView) return;

            // Shapes only. The labels for this same overlay are drawn in OnSceneGui, one pass later,
            // so nothing the gizmo pass draws can land on top of them.
            _buffer.Replay(_gizmos);
        }
#endif
    }
}
