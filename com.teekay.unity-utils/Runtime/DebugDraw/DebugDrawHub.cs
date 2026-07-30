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

        const float LabelWidth = 300f;
        const int LabelFontSize = 12;

        /// <summary>
        /// Master switch over every overlay — the `debugdraw` console variable. Off means no
        /// drawable is even asked to draw.
        /// </summary>
        public static bool Enabled { get; set; } = DefaultEnabled;

        readonly List<IDebugDrawable> _drawables = new List<IDebugDrawable>(8);
        readonly DebugDrawBuffer _buffer = new DebugDrawBuffer();
        readonly List<(Vector3 Position, string Text)> _labels = new List<(Vector3, string)>(32);

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

            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = LabelFontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            foreach ((Vector3 position, string text) in _labels)
            {
                Vector3 screenPos = _camera.WorldToScreenPoint(position);
                if (screenPos.z <= 0f) continue; // behind the camera

                float height = _labelStyle.CalcHeight(new GUIContent(text), LabelWidth);
                var rect = new Rect(screenPos.x - LabelWidth * 0.5f, Screen.height - screenPos.y - height,
                    LabelWidth, height);

                // Shadow first, then the text: debug labels land on arbitrary scenery, and plain
                // white is unreadable against a bright wall.
                GUI.color = Color.black;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _labelStyle);
                GUI.color = Color.white;
                GUI.Label(rect, text, _labelStyle);
            }
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

            _buffer.Replay(_gizmos);

            foreach ((Vector3 position, string text) in _labels)
                UnityEditor.Handles.Label(position, text);
        }
#endif
    }
}
