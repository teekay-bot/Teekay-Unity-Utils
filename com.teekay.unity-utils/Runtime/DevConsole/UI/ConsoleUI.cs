using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TeekayUtils.DevConsole.UI
{
    /// <summary>
    /// uGUI front-end for <see cref="DevConsole"/>. Builds its entire UI hierarchy in code
    /// (no prefabs / no scene setup required) so the framework folder is truly drop-in.
    ///
    /// Iteration 2 layout:
    ///   Canvas (sortingOrder = short.MaxValue, screen-space overlay)
    ///   └── Window  (movable + resizable, anchored top-left, default = top half of screen)
    ///       ├── TitleBar     (drag handle, title text, close button)
    ///       ├── LogScroll    (scrollable log; child = LogText TMP)
    ///       ├── InputRow     (TMP_InputField on top, GhostText overlay behind for autocomplete)
    ///       └── ResizeHandle (bottom-right corner grip)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConsoleUI : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Window geometry (in-session memory — reset to defaults on play start)
        // ─────────────────────────────────────────────────────────────────────

        const float MIN_WINDOW_W = 320f;
        const float MIN_WINDOW_H = 180f;
        // Resize grab zones are sized for touch, not just a mouse cursor. They straddle the
        // window border (half outside, half inside) and are drawn BEHIND the interactive content
        // (see EnsureBuilt), so content wins where it sits and the handles win on the bare border
        // and just outside it. The bottom-right corner is content-free, so it's the big easy grip.
        const float EDGE_HANDLE_THICKNESS = 36f;  // resize-strip thickness, centred on each side
        const float CORNER_HANDLE_SIZE    = 64f;  // corner squares for diagonal resize
        const float PADDING    = 10f;
        const float DEFAULT_WINDOW_W = 460f;
        const float DEFAULT_WINDOW_H = 620f;

        // Chrome heights scale with the configured font so large fonts (e.g. high-DPI mobile)
        // grow their rows instead of clipping/overlapping. Each padding is the breathing room
        // added ABOVE and BELOW the glyph line, tuned so the legacy 14pt default reproduces the
        // original fixed heights exactly (input 36 = 14+2×11, title 34 = 14+2×10, row 28 = 14+2×7).
        const float INPUT_ROW_PADDING_V  = 11f;
        const float TITLE_BAR_PADDING_V  = 10f;
        const float SUGGESTION_PADDING_V = 7f;

        /// <summary>Input-row height, scaled to the configured font size so large fonts grow the
        /// row rather than getting clipped by its viewport mask.</summary>
        float InputRowHeight => DevConsoleSettings.FontSize + INPUT_ROW_PADDING_V * 2f;

        /// <summary>Title-bar height, scaled to the configured font size.</summary>
        float TitleBarHeight => DevConsoleSettings.FontSize + TITLE_BAR_PADDING_V * 2f;

        /// <summary>Per-row height of the autocomplete dropdown, scaled to the font so rows
        /// don't overlap at large font sizes.</summary>
        float SuggestionRowHeight => DevConsoleSettings.FontSize + SUGGESTION_PADDING_V * 2f;

        // ─────────────────────────────────────────────────────────────────────
        //  Theme — Warp/Linear inspired, deep blue-black + cyan accent. Centralised so
        //  the entire chrome can be retoned by tweaking values in one place.
        // ─────────────────────────────────────────────────────────────────────
        static class Theme
        {
            // Surfaces (three elevation tiers: window < elevated < hover).
            public static readonly Color BgWindow   = new(0.055f, 0.063f, 0.078f, 0.97f); // #0E1014
            public static readonly Color BgElevated = new(0.086f, 0.098f, 0.122f, 1.00f); // #16191F
            public static readonly Color BgHover    = new(0.118f, 0.137f, 0.165f, 1.00f); // #1E232A

            // Borders / separators — almost-invisible white on dark.
            public static readonly Color BorderSubtle  = new(1f, 1f, 1f, 0.05f);
            public static readonly Color SeparatorLine = new(1f, 1f, 1f, 0.07f);

            // Text tiers.
            public static readonly Color TextPrimary = new(0.92f, 0.94f, 0.96f);
            public static readonly Color TextMuted   = new(0.55f, 0.60f, 0.66f);
            public static readonly Color TextSubtle  = new(0.38f, 0.42f, 0.48f);

            // Accent — Tailwind cyan-400 (#22D3EE) and its translucent variants.
            public static readonly Color Accent      = new(0.133f, 0.827f, 0.933f, 1.00f);
            public static readonly Color AccentMuted = new(0.133f, 0.827f, 0.933f, 0.55f);
            public static readonly Color AccentSoft  = new(0.133f, 0.827f, 0.933f, 0.14f);

            // Close button — transparent at rest, soft red on hover.
            public static readonly Color CloseIdle  = new(0f, 0f, 0f, 0f);
            public static readonly Color CloseHover = new(0.96f, 0.42f, 0.45f, 0.18f);

            // Selection background for suggestion rows (cyan tinted, mostly transparent).
            public static readonly Color RowSelected = new(0.133f, 0.827f, 0.933f, 0.16f);

            // Procedurally-generated rounded sprite. Built once on first access (~1ms) and
            // cached. White-tinted by Image.color downstream. Generating it ourselves avoids the
            // Unity-version lottery of `Resources.GetBuiltinResource("UI/Skin/UISprite.psd")` —
            // that path is missing in Unity 6's URP template.
            const int RoundedSize   = 32; // texture is 32×32 with 8px radius, 9-sliced into thirds
            const int RoundedRadius = 8;

            static Sprite _rounded;
            public static Sprite Rounded
            {
                get
                {
                    if (_rounded != null) return _rounded;
                    var tex = new Texture2D(RoundedSize, RoundedSize, TextureFormat.RGBA32, false)
                    {
                        wrapMode   = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags  = HideFlags.HideAndDontSave,
                        name       = "DevConsole_RoundedTex"
                    };
                    for (int y = 0; y < RoundedSize; y++)
                    for (int x = 0; x < RoundedSize; x++)
                    {
                        // Distance from the nearest "inner corner anchor" — zero in the straight
                        // edge regions, growing as we approach a corner.
                        float dx = Mathf.Max(0f, Mathf.Max(RoundedRadius - x, x - (RoundedSize - 1 - RoundedRadius)));
                        float dy = Mathf.Max(0f, Mathf.Max(RoundedRadius - y, y - (RoundedSize - 1 - RoundedRadius)));
                        float d  = Mathf.Sqrt(dx * dx + dy * dy);
                        // Anti-aliased coverage: full inside the radius, smooth 1-pixel falloff
                        // at the boundary, zero outside.
                        float a = Mathf.Clamp01(RoundedRadius + 0.5f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                    tex.Apply(false, true);
                    var border = new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius);
                    _rounded = Sprite.Create(tex, new Rect(0, 0, RoundedSize, RoundedSize),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                    _rounded.name = "DevConsole_Rounded";
                    _rounded.hideFlags = HideFlags.HideAndDontSave;
                    return _rounded;
                }
            }
        }

        Vector2 _windowSize;
        Vector2 _windowPos;   // anchoredPosition relative to top-left of canvas
        bool _windowInitialized;

        DevConsole _console;

        // — Built UI references —
        Canvas _canvas;
        RectTransform _canvasRt;
        GameObject _window;
        RectTransform _windowRt;
        TMP_Text _logText;
        ScrollRect _scroll;
        TMP_InputField _input;
        TMP_Text _ghostText;
        Image _inputFocusRing;     // 1px cyan underline that lights up while typing
        EventSystem _createdEventSystem;   // non-null only if the console spawned its own EventSystem

        // — Suggestion dropdown —
        // Visible rows are fixed (panel footprint matches the original 5-row design); the
        // collection cap is larger so Up/Down can cycle through every match by scrolling the
        // visible window.
        const int MAX_VISIBLE_SUGGESTIONS = 5;
        const int MAX_COLLECTED_SUGGESTIONS = 64;
        GameObject _suggestionPanel;
        readonly TMP_Text[] _suggestionRows = new TMP_Text[MAX_VISIBLE_SUGGESTIONS];
        readonly List<ConsoleAutocomplete.MatchResult> _matchesBuf = new(MAX_COLLECTED_SUGGESTIONS);
        int _selectedSuggestion = -1; // -1 = none
        int _suggestionViewOffset = 0; // index of the first visible match in _matchesBuf

        // — Focus tracking — polled each frame; transitions drive DevConsole.NotifyFocusChanged —
        bool _lastFocusState;
        // On touch platforms, focus is driven by WHERE the user presses (in/out of the window)
        // rather than by EventSystem selection — otherwise re-selecting the input to keep focus
        // would reopen the soft keyboard on every scroll/drag/resize. See LateUpdate.
        bool _touchMode;
        bool _touchFocused;

        // — Log redraw state —
        readonly StringBuilder _logBuilder = new(4096);
        bool _logDirty = true;
        bool _scrollToBottomQueued;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public void Bind(DevConsole console)
        {
            _console = console;
            _touchMode = Application.isMobilePlatform;
            _console.OnLogAppended += OnLogAppended;
            _console.OnLogCleared  += OnLogCleared;
            EnsureBuilt();
        }

        void OnDestroy()
        {
            if (_console != null)
            {
                _console.OnLogAppended -= OnLogAppended;
                _console.OnLogCleared  -= OnLogCleared;
            }
            if (_input != null)
            {
                _input.onValueChanged.RemoveListener(OnInputValueChanged);
                _input.onSubmit.RemoveListener(OnInputSubmit);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Visibility
        // ─────────────────────────────────────────────────────────────────────

        public void SetOpen(bool open)
        {
            EnsureBuilt();
            if (_window == null) return;
            // Drop a console-made EventSystem if the scene now provides its own, so we never
            // end up with two. Runs on both open and close.
            CleanupRedundantEventSystem();
            _window.SetActive(open);
            if (open)
            {
                // Create an EventSystem only when actually opening and the scene has none.
                EnsureEventSystem();
                MarkLogDirty();
                FocusInput();
            }
            else
            {
                // Clear leftover text and drop focus so gameplay keys aren't swallowed.
                if (_input != null) _input.text = string.Empty;
                if (_ghostText != null) _ghostText.text = string.Empty;
                if (EventSystem.current != null &&
                    EventSystem.current.currentSelectedGameObject == _input.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                // Reset focus tracking so a later re-open re-fires NotifyFocusChanged(true) and
                // re-applies pause/cursor side effects (the LateUpdate poll is skipped while the
                // window is inactive, so without this the stale "true" would suppress the next open).
                _lastFocusState = false;
                _touchFocused   = false;
            }
        }

        void FocusInput()
        {
            if (_input == null) return;
            _input.text = string.Empty;
            _input.ActivateInputField();
            EventSystem.current?.SetSelectedGameObject(_input.gameObject);
            _touchFocused = true; // opening / re-focusing counts as focused in touch mode
        }

        /// <summary>
        /// Re-focus the input field WITHOUT clearing the text. Called by DevConsole when the
        /// user presses the toggle key while the console is open but unfocused — gives them a
        /// keyboard-only path back to typing in the console (without this, once cursor lock
        /// returns to gameplay the user has no way to click on the console UI).
        /// </summary>
        public void RefocusInput()
        {
            EnsureBuilt();
            if (_input == null) return;
            _input.ActivateInputField();
            EventSystem.current?.SetSelectedGameObject(_input.gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public hooks for drag/resize handles
        // ─────────────────────────────────────────────────────────────────────

        public void OnWindowDrag(Vector2 deltaPixels)
        {
            // anchoredPosition with top-left anchor: +x moves right, +y moves up.
            // Pointer delta also has +y up, so direct addition is correct.
            _windowPos += deltaPixels;
            ApplyWindowRect();
        }

        /// <summary>
        /// Apply a drag delta to one or more edges. Coordinate system: pointer.delta has +y up,
        /// while _windowPos uses anchoredPosition (top-left anchor) where y is 0 at canvas top
        /// and goes negative downward. The "opposite" edge of the dragged one stays anchored —
        /// we recompute size from the (possibly shifted) edges so clamping is trivial.
        /// </summary>
        public void OnEdgeResize(ResizeEdges edges, Vector2 deltaPixels)
        {
            // Current right and bottom edge positions (in anchored-pos space).
            float right  = _windowPos.x + _windowSize.x;
            float bottom = _windowPos.y - _windowSize.y; // _windowPos.y is the TOP, bottom is lower (more negative)

            if ((edges & ResizeEdges.Right)  != 0) right         += deltaPixels.x;
            if ((edges & ResizeEdges.Left)   != 0) _windowPos.x  += deltaPixels.x;
            if ((edges & ResizeEdges.Top)    != 0) _windowPos.y  += deltaPixels.y;
            if ((edges & ResizeEdges.Bottom) != 0) bottom        += deltaPixels.y;

            // Re-derive size from the (possibly shifted) edges.
            _windowSize.x = right - _windowPos.x;
            _windowSize.y = _windowPos.y - bottom;

            // Clamp size to minimum; if the DRAGGED edge would have pushed past it, pin that
            // edge to the min-size point so the OPPOSITE edge keeps its anchor.
            if (_windowSize.x < MIN_WINDOW_W)
            {
                if ((edges & ResizeEdges.Left) != 0) _windowPos.x = right - MIN_WINDOW_W;
                _windowSize.x = MIN_WINDOW_W;
            }
            if (_windowSize.y < MIN_WINDOW_H)
            {
                if ((edges & ResizeEdges.Top) != 0) _windowPos.y = bottom + MIN_WINDOW_H;
                _windowSize.y = MIN_WINDOW_H;
            }

            ApplyWindowRect();
        }

        /// <summary>Clamp current _windowPos / _windowSize to screen bounds and apply to the RectTransform.</summary>
        void ApplyWindowRect()
        {
            if (_windowRt == null || _canvasRt == null) return;

            Vector2 canvasSize = _canvasRt.rect.size;
            // Clamp size — must fit on screen, must be at least min size.
            _windowSize.x = Mathf.Clamp(_windowSize.x, MIN_WINDOW_W, canvasSize.x);
            _windowSize.y = Mathf.Clamp(_windowSize.y, MIN_WINDOW_H, canvasSize.y);

            // Anchor (0,1) + pivot (0,1) means anchoredPosition is the top-left of the window
            // relative to the top-left of the canvas, with +y going DOWN by Unity's convention
            // for top-anchored rects (anchoredPosition.y is negative below the anchor).
            // We work in "pixels from top-left" semantics: pos.x ≥ 0, pos.y ≤ 0.
            _windowPos.x = Mathf.Clamp(_windowPos.x, 0f, canvasSize.x - _windowSize.x);
            _windowPos.y = Mathf.Clamp(_windowPos.y, -(canvasSize.y - _windowSize.y), 0f);

            _windowRt.sizeDelta = _windowSize;
            _windowRt.anchoredPosition = _windowPos;

            // Persist on every change. PlayerPrefs.SetFloat is cheap; explicit Save() runs in
            // OnDestroy / OnApplicationQuit so we don't thrash the disk every drag frame.
            PlayerPrefs.SetFloat(PREF_KEY_POS_X,  _windowPos.x);
            PlayerPrefs.SetFloat(PREF_KEY_POS_Y,  _windowPos.y);
            PlayerPrefs.SetFloat(PREF_KEY_SIZE_X, _windowSize.x);
            PlayerPrefs.SetFloat(PREF_KEY_SIZE_Y, _windowSize.y);
        }

        const string PREF_KEY_POS_X  = "DevConsole.WindowPos.X";
        const string PREF_KEY_POS_Y  = "DevConsole.WindowPos.Y";
        const string PREF_KEY_SIZE_X = "DevConsole.WindowSize.X";
        const string PREF_KEY_SIZE_Y = "DevConsole.WindowSize.Y";

        bool TryLoadWindowRectFromPrefs()
        {
            // All four keys must be present; missing any falls back to defaults.
            if (!PlayerPrefs.HasKey(PREF_KEY_POS_X) || !PlayerPrefs.HasKey(PREF_KEY_POS_Y)
                || !PlayerPrefs.HasKey(PREF_KEY_SIZE_X) || !PlayerPrefs.HasKey(PREF_KEY_SIZE_Y))
                return false;
            _windowPos  = new Vector2(PlayerPrefs.GetFloat(PREF_KEY_POS_X),  PlayerPrefs.GetFloat(PREF_KEY_POS_Y));
            _windowSize = new Vector2(PlayerPrefs.GetFloat(PREF_KEY_SIZE_X), PlayerPrefs.GetFloat(PREF_KEY_SIZE_Y));
            return true;
        }

        void OnApplicationQuit() => PlayerPrefs.Save();

        // ─────────────────────────────────────────────────────────────────────
        //  Log re-render
        // ─────────────────────────────────────────────────────────────────────

        void OnLogAppended(ConsoleLogEntry _) => MarkLogDirty();
        void OnLogCleared() => MarkLogDirty();

        void MarkLogDirty()
        {
            _logDirty = true;
            _scrollToBottomQueued = true;
        }

        void LateUpdate()
        {
            if (_window == null || !_window.activeSelf) return;

            // Keep focus coherent while the user interacts with the console chrome. The two
            // platforms need opposite handling:
            //   • Desktop — re-select the input on any press inside the window so clicking chrome
            //     (title/scroll/resize) doesn't drop focus and relock the cursor mid-drag.
            //   • Touch   — do NOT re-select the input; that would call ActivateInputField and
            //     reopen the soft keyboard on every scroll/drag/resize. Instead track focus by
            //     whether the press landed inside the window. The keyboard then opens only when
            //     the user taps the input field itself (TMP does that), and closes when they
            //     touch the chrome.
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame &&
                _input != null && EventSystem.current != null)
            {
                bool insideConsole = IsPressWithinConsole(pointer.position.ReadValue());
                if (_touchMode)
                {
                    _touchFocused = insideConsole;
                }
                else if (insideConsole &&
                         EventSystem.current.currentSelectedGameObject != _input.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(_input.gameObject);
                    _input.ActivateInputField();
                }
            }

            // Poll focus state. Touch mode uses the press-region flag (above); desktop uses the
            // EventSystem selection. A transition tells DevConsole to apply/unwind pause + cursor
            // + input-block side effects.
            bool focused = _touchMode
                ? _touchFocused
                : (_input != null
                    && EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == _input.gameObject);
            if (focused != _lastFocusState)
            {
                _lastFocusState = focused;
                _console?.NotifyFocusChanged(focused);
                if (_inputFocusRing != null) _inputFocusRing.enabled = focused;
            }

            // Only consume keyboard when the input field has focus. Otherwise the user is
            // playing the game with the console open as a monitor — Enter/Tab/Arrows should
            // pass through to gameplay, not get swallowed by the console.
            if (focused) HandleInputKeys();

            if (_logDirty)
            {
                RebuildLogText();
                _logDirty = false;
            }

            if (_scrollToBottomQueued)
            {
                Canvas.ForceUpdateCanvases();
                if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
                _scrollToBottomQueued = false;
            }
        }

        void RebuildLogText()
        {
            _logBuilder.Clear();
            foreach (var entry in _console.EnumerateLog())
            {
                _logBuilder
                    .Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(entry.Color)).Append('>')
                    .Append($"[{entry.Category}] {entry.Message}")
                    .Append("</color>\n");
            }
            _logText.text = _logBuilder.ToString();
        }

        // True when a screen-space press falls within the window OR its straddling resize handles
        // (the window rect inflated by half a corner handle). Used to decide whether a press is
        // "interacting with the console" vs. a tap out in the game.
        bool IsPressWithinConsole(Vector2 screenPos)
        {
            if (_windowRt == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _windowRt, screenPos, null, out Vector2 local))
                return false;
            float margin = CORNER_HANDLE_SIZE * 0.5f;
            Rect r = _windowRt.rect;
            Rect inflated = new Rect(r.xMin - margin, r.yMin - margin,
                                     r.width + margin * 2f, r.height + margin * 2f);
            return inflated.Contains(local);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Input handling
        // ─────────────────────────────────────────────────────────────────────

        void HandleInputKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Enter ALWAYS submits the current input. CS2-style: Tab accepts completions,
            // Enter executes. Previously this checked _selectedSuggestion first, but because
            // _selectedSuggestion auto-snaps to 0 on every keystroke, that path swallowed
            // every Enter press and the command never actually ran.
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                Submit();
                return;
            }

            // Tab — accept the highlighted dropdown suggestion if any, otherwise accept the
            // inline ghost-suffix (prefix-only completion). When the input contains a space we
            // are completing an argument value, so only the active (last) token is replaced;
            // otherwise we replace the full input with the chosen command/CVar name.
            if (kb.tabKey.wasPressedThisFrame)
            {
                if (_selectedSuggestion >= 0 && _selectedSuggestion < _matchesBuf.Count)
                {
                    SetInputText(ApplyCompletion(_input.text, _matchesBuf[_selectedSuggestion].Name));
                    return;
                }
                if (ConsoleAutocomplete.TryGetCompletion(_input.text, DevConsole.Registry, out string suffix))
                {
                    SetInputText(_input.text + suffix);
                    return;
                }
            }

            // Up/Down — dropdown navigation when it has matches; history navigation otherwise.
            if (_matchesBuf.Count > 0)
            {
                if (kb.upArrowKey.wasPressedThisFrame)
                { _selectedSuggestion = (_selectedSuggestion - 1 + _matchesBuf.Count) % _matchesBuf.Count; ScrollViewToSelection(); RefreshSuggestionRows(); }
                else if (kb.downArrowKey.wasPressedThisFrame)
                { _selectedSuggestion = (_selectedSuggestion + 1) % _matchesBuf.Count; ScrollViewToSelection(); RefreshSuggestionRows(); }
            }
            else
            {
                if (kb.upArrowKey.wasPressedThisFrame)
                {
                    string prev = DevConsole.History.NavigatePrevious();
                    if (prev != null) SetInputText(prev);
                }
                else if (kb.downArrowKey.wasPressedThisFrame)
                {
                    string next = DevConsole.History.NavigateNext();
                    if (next != null) SetInputText(next);
                }
            }
        }

        // Last frame Submit() actually ran. Guards against a double-execute when BOTH the
        // manual enterKey poll (desktop) and the TMP onSubmit event (soft-keyboard return)
        // fire in the same frame.
        int _lastSubmitFrame = -1;

        void Submit()
        {
            if (Time.frameCount == _lastSubmitFrame) return;
            _lastSubmitFrame = Time.frameCount;

            string line = _input.text;
            _input.text = string.Empty;
            UpdateGhost(string.Empty);
            if (!string.IsNullOrWhiteSpace(line)) DevConsole.Execute(line);
            FocusInput();
        }

        // Soft-keyboard "return/done" (and hardware Enter routed through the EventSystem) arrive
        // here. The frame guard inside Submit() prevents a double-run with the manual poll.
        void OnInputSubmit(string _) => Submit();

        // Tap on a suggestion row = accept that completion (the touch equivalent of Tab). The row
        // passes its VISIBLE index; map it through the scroll offset to the real match index.
        void AcceptSuggestionRow(int visualRow)
        {
            int matchIdx = _suggestionViewOffset + visualRow;
            if (matchIdx < 0 || matchIdx >= _matchesBuf.Count) return;
            _selectedSuggestion = matchIdx;
            SetInputText(ApplyCompletion(_input.text, _matchesBuf[matchIdx].Name));
            // Keep the keyboard up and the field selected so the user can keep typing/submit.
            EventSystem.current?.SetSelectedGameObject(_input.gameObject);
        }

        // Picking a dropdown row in name-completion mode (no space) means "use this as the whole
        // line". In arg-completion mode (has a space), the match replaces only the active token —
        // the part the user is currently typing after the last space — so previous args survive.
        static string ApplyCompletion(string currentInput, string match)
        {
            int lastSpace = currentInput.LastIndexOf(' ');
            if (lastSpace < 0) return match;
            return currentInput.Substring(0, lastSpace + 1) + match;
        }

        void SetInputText(string text)
        {
            _input.text = text;
            _input.caretPosition = text.Length;
            _input.ActivateInputField();
            UpdateGhost(text);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Ghost hint
        // ─────────────────────────────────────────────────────────────────────

        void OnInputValueChanged(string newText) => UpdateGhost(newText);

        /// <summary>
        /// Update the ghost overlay text. The trick: render the typed portion as fully
        /// transparent (it acts as a spacer) and the suggested suffix in gray. The user's real
        /// text (from TMP_InputField's own text component) draws on top in white at the exact
        /// same metrics, so the visible result is white-typed + gray-suffix, perfectly aligned.
        /// No text-width measurement needed.
        /// </summary>
        void UpdateGhost(string text)
        {
            if (_ghostText == null) return;
            if (ConsoleAutocomplete.TryGetCompletion(text, DevConsole.Registry, out string suffix))
            {
                _ghostText.text = "<color=#00000000>" + text + "</color>" +
                                  "<color=#" + ColorUtility.ToHtmlStringRGBA(DevConsoleSettings.HintColor) + ">" +
                                  suffix + "</color>";
            }
            else
            {
                _ghostText.text = string.Empty;
            }

            // Refresh dropdown alongside ghost — they share the autocomplete data.
            ConsoleAutocomplete.GetMatches(text, DevConsole.Registry, MAX_COLLECTED_SUGGESTIONS, _matchesBuf);
            _selectedSuggestion = _matchesBuf.Count > 0 ? 0 : -1;
            _suggestionViewOffset = 0;
            RefreshSuggestionRows();
        }

        // Keep the selection visible inside the fixed-size window. Handles wrap (last→first or
        // first→last) by snapping the window to the new selection's edge.
        void ScrollViewToSelection()
        {
            if (_matchesBuf.Count <= MAX_VISIBLE_SUGGESTIONS)
            {
                _suggestionViewOffset = 0;
                return;
            }
            if (_selectedSuggestion < _suggestionViewOffset)
                _suggestionViewOffset = _selectedSuggestion;
            else if (_selectedSuggestion >= _suggestionViewOffset + MAX_VISIBLE_SUGGESTIONS)
                _suggestionViewOffset = _selectedSuggestion - MAX_VISIBLE_SUGGESTIONS + 1;
        }

        void RefreshSuggestionRows()
        {
            if (_suggestionPanel == null) return;
            bool any = _matchesBuf.Count > 0;
            _suggestionPanel.SetActive(any);
            if (!any) return;

            int visibleCount = Mathf.Min(MAX_VISIBLE_SUGGESTIONS, _matchesBuf.Count);

            // Resize the panel to fit the actual number of visible rows.
            var panelRt = (RectTransform)_suggestionPanel.transform;
            panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, SuggestionRowHeight * visibleCount);

            for (int i = 0; i < MAX_VISIBLE_SUGGESTIONS; i++)
            {
                int matchIdx = _suggestionViewOffset + i;
                if (matchIdx >= _matchesBuf.Count)
                {
                    _suggestionRows[i].transform.parent.gameObject.SetActive(false);
                    continue;
                }
                _suggestionRows[i].transform.parent.gameObject.SetActive(true);

                var m = _matchesBuf[matchIdx];
                bool selected = matchIdx == _selectedSuggestion;

                // Tag colors: cyan for CVar (value-bearing), muted gray for Cmd (action).
                string kind = m.IsCVar ? "VAR" : "CMD";
                string kindColor = m.IsCVar ? "#22D3EE" : "#7C8390";
                string nameColor = selected ? "#FFFFFF" : "#E8EBEF";
                string descColor = selected ? "#9CA3AF" : "#6B7280";

                string body = $"<color={kindColor}><size=80%><b>{kind}</b></size></color>  " +
                              $"<color={nameColor}><b>{m.Name}</b></color>";
                if (!string.IsNullOrEmpty(m.Description))
                    body += $"  <color={descColor}>{m.Description}</color>";

                // Append "(n/total)" position indicator to the highlighted row when scrolling is
                // in play, so the user can see where they are in the full list.
                if (selected && _matchesBuf.Count > MAX_VISIBLE_SUGGESTIONS)
                    body += $"  <color=#4B5563><size=80%>{_selectedSuggestion + 1}/{_matchesBuf.Count}</size></color>";

                _suggestionRows[i].text = body;

                // Selection chrome: tint background, light up the left accent stripe.
                var rowParent = _suggestionRows[i].transform.parent;
                var rowImage = rowParent.GetComponent<Image>();
                rowImage.color = selected ? Theme.RowSelected
                                          : (i % 2 == 0 ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 1f, 1f, 0.018f));
                var stripeT = rowParent.Find("AccentStripe");
                if (stripeT != null)
                {
                    var stripeImg = stripeT.GetComponent<Image>();
                    if (stripeImg != null) stripeImg.enabled = selected;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI construction (pure code, no prefabs)
        // ─────────────────────────────────────────────────────────────────────

        void EnsureBuilt()
        {
            if (_canvas != null) return;
            if (!IsTextMeshProReady())
            {
                Debug.LogWarning("[DevConsole] TextMeshPro Essentials are not imported, so the console " +
                                 "window cannot be shown. Commands, CVars and log capture still work. " +
                                 "Import via 'Window > TextMeshPro > Import TMP Essentials' and reload.");
                return;
            }

            BuildCanvas();
            BuildWindow();
            // Resize handles go in BEFORE the interactive content so they sit behind it: the title
            // bar, close button, input field and Run button win raycasts where they overlap, while
            // the handles win on the bare window border and just outside it. This lets the grab
            // zones be large (touch-friendly) without swallowing the close button or input row.
            BuildResizeHandles();
            BuildTitleBar();
            BuildLogScroll();
            BuildInputRow();
            BuildSuggestionPanel();

            // Restore previous layout from PlayerPrefs if available; otherwise default to a
            // 400×600 window in the top-left corner with a small margin.
            if (!_windowInitialized)
            {
                if (!TryLoadWindowRectFromPrefs())
                {
                    _windowSize = new Vector2(DEFAULT_WINDOW_W, DEFAULT_WINDOW_H);
                    _windowPos  = new Vector2(PADDING, -PADDING);
                }
                _windowInitialized = true;
            }
            ApplyWindowRect();
        }

        /// <summary>
        /// True only when TMP Essentials are importable AND present. When they are missing,
        /// TMP_Settings.instance is null and its property getters throw NullReferenceException
        /// rather than returning null — so this must be wrapped in try/catch, not a plain
        /// null check, to avoid crashing consumers who forgot to import TMP Essentials.
        /// </summary>
        static bool IsTextMeshProReady()
        {
            try { return TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null; }
            catch { return false; }
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("DevConsoleCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvasRt = (RectTransform)canvasGo.transform;
        }

        // Create an EventSystem only if the scene genuinely has none. The robust check catches
        // EventSystems that exist but haven't run OnEnable yet (EventSystem.current still null)
        // and disabled ones. The created instance is tracked so it can be cleaned up later.
        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
            _createdEventSystem = es.GetComponent<EventSystem>();
        }

        // If we previously created our own EventSystem and another now exists in the scene,
        // destroy ours so there is never a duplicate.
        void CleanupRedundantEventSystem()
        {
            if (_createdEventSystem == null) return;
            EventSystem[] all = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (EventSystem es in all)
            {
                if (es != _createdEventSystem)
                {
                    Destroy(_createdEventSystem.gameObject);
                    _createdEventSystem = null;
                    return;
                }
            }
        }

        void BuildWindow()
        {
            _window = new GameObject("Window", typeof(Image));
            _window.transform.SetParent(_canvas.transform, false);
            _windowRt = (RectTransform)_window.transform;
            // Top-left anchor + pivot — anchoredPosition is the window's top-left corner.
            _windowRt.anchorMin = new Vector2(0f, 1f);
            _windowRt.anchorMax = new Vector2(0f, 1f);
            _windowRt.pivot     = new Vector2(0f, 1f);

            var bg = _window.GetComponent<Image>();
            bg.sprite = Theme.Rounded;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1.5f;
            bg.color = Theme.BgWindow;

            // Hairline outer border — child Image stretched to the window with a transparent
            // body and a faint white tint. Using the same rounded sprite at a slightly larger
            // outset would require padding; instead we rely on the sprite's existing edge.
            var border = new GameObject("Border", typeof(Image));
            border.transform.SetParent(_window.transform, false);
            var borderRt = (RectTransform)border.transform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            var borderImg = border.GetComponent<Image>();
            borderImg.sprite = Theme.Rounded;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = 1.5f;
            borderImg.color = Theme.BorderSubtle;
            borderImg.raycastTarget = false; // don't intercept clicks
        }

        void BuildTitleBar()
        {
            // Bar is transparent — the window background shows through, so the rounded top
            // corners stay clean. We still need an Image (raycast target) for the drag handle.
            var titleGo = new GameObject("TitleBar", typeof(Image), typeof(ConsoleWindowDragHandle));
            titleGo.transform.SetParent(_window.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot     = new Vector2(0f, 1f);
            titleRt.anchoredPosition = Vector2.zero;
            titleRt.sizeDelta = new Vector2(0f, TitleBarHeight);
            var titleImg = titleGo.GetComponent<Image>();
            titleImg.color = new Color(0f, 0f, 0f, 0f); // invisible, still raycasts
            titleGo.GetComponent<ConsoleWindowDragHandle>()._owner = this;

            // Status dot — small cyan circle on the far left. Indicates "console is alive".
            var dotGo = new GameObject("StatusDot", typeof(Image));
            dotGo.transform.SetParent(titleGo.transform, false);
            var dotRt = (RectTransform)dotGo.transform;
            dotRt.anchorMin = new Vector2(0f, 0.5f);
            dotRt.anchorMax = new Vector2(0f, 0.5f);
            dotRt.pivot     = new Vector2(0f, 0.5f);
            dotRt.anchoredPosition = new Vector2(14f, 0f);
            dotRt.sizeDelta = new Vector2(7f, 7f);
            var dotImg = dotGo.GetComponent<Image>();
            dotImg.sprite = Theme.Rounded;
            dotImg.type = Image.Type.Sliced;
            dotImg.color = Theme.Accent;
            dotImg.raycastTarget = false;

            // Title label — muted color, semi-bold, slight letter-spacing for the Linear feel.
            var labelGo = new GameObject("Title", typeof(RectTransform));
            labelGo.transform.SetParent(titleGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = DevConsoleSettings.FontSize - 1;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 4f;       // gives the uppercase title some breathing room
            label.color = Theme.TextMuted;
            label.text = "DEV CONSOLE";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(30f, 0f);          // sits right of the status dot
            labelRt.offsetMax = new Vector2(-TitleBarHeight, 0f);   // leave room for the close button

            // Close button — soft, transparent at rest; the Button's built-in ColorBlock
            // tints on hover/press so we don't have to listen ourselves.
            var closeGo = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeGo.transform.SetParent(titleGo.transform, false);
            var closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot     = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-8f, 0f);
            closeRt.sizeDelta = new Vector2(TitleBarHeight - 12f, TitleBarHeight - 12f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.sprite = Theme.Rounded;
            closeImg.type = Image.Type.Sliced;
            closeImg.color = Color.white; // tinted by ColorBlock below
            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.transition = Selectable.Transition.ColorTint;
            closeBtn.colors = new ColorBlock
            {
                normalColor      = Theme.CloseIdle,
                highlightedColor = Theme.CloseHover,
                pressedColor     = new Color(Theme.CloseHover.r, Theme.CloseHover.g, Theme.CloseHover.b, 0.30f),
                selectedColor    = Theme.CloseIdle,
                disabledColor    = Theme.CloseIdle,
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f,
            };
            closeBtn.onClick.AddListener(() => DevConsole.Close());

            var closeLabelGo = new GameObject("X", typeof(RectTransform));
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            var closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
            closeLabel.font = TMP_Settings.defaultFontAsset;
            closeLabel.fontSize = DevConsoleSettings.FontSize + 2;
            closeLabel.color = Theme.TextMuted;
            closeLabel.text = "×"; // multiplication sign — softer than 'X'
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.raycastTarget = false;
            var closeLabelRt = closeLabel.rectTransform;
            closeLabelRt.anchorMin = Vector2.zero;
            closeLabelRt.anchorMax = Vector2.one;
            closeLabelRt.offsetMin = Vector2.zero;
            closeLabelRt.offsetMax = Vector2.zero;

            // 1px hairline separator at the bottom of the title bar — the only visible boundary.
            var sepGo = new GameObject("Separator", typeof(Image));
            sepGo.transform.SetParent(titleGo.transform, false);
            var sepRt = (RectTransform)sepGo.transform;
            sepRt.anchorMin = new Vector2(0f, 0f);
            sepRt.anchorMax = new Vector2(1f, 0f);
            sepRt.pivot     = new Vector2(0.5f, 0f);
            sepRt.anchoredPosition = Vector2.zero;
            sepRt.sizeDelta = new Vector2(0f, 1f);
            var sepImg = sepGo.GetComponent<Image>();
            sepImg.color = Theme.SeparatorLine;
            sepImg.raycastTarget = false;
        }

        void BuildLogScroll()
        {
            var scrollGo = new GameObject("LogScroll", typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(_window.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            // Stretch to fill the window minus title bar (top) and input row (bottom).
            scrollRt.offsetMin = new Vector2(PADDING + 2f, InputRowHeight + PADDING);
            scrollRt.offsetMax = new Vector2(-(PADDING + 2f), -(TitleBarHeight + 4f));
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.viewport = scrollRt;

            var logTextGo = new GameObject("LogText", typeof(RectTransform));
            logTextGo.transform.SetParent(scrollGo.transform, false);
            _logText = logTextGo.AddComponent<TextMeshProUGUI>();
            _logText.font = TMP_Settings.defaultFontAsset;
            _logText.fontSize = DevConsoleSettings.FontSize;
            _logText.textWrappingMode = TextWrappingModes.Normal;
            _logText.richText = true;
            _logText.color = Theme.TextPrimary;
            _logText.alignment = TextAlignmentOptions.TopLeft;
            _logText.raycastTarget = false;
            _logText.lineSpacing = 4f; // breathing room between log lines
            var logTextRt = _logText.rectTransform;
            logTextRt.anchorMin = new Vector2(0f, 1f);
            logTextRt.anchorMax = new Vector2(1f, 1f);
            logTextRt.pivot     = new Vector2(0f, 1f);
            logTextRt.anchoredPosition = Vector2.zero;
            logTextRt.sizeDelta = Vector2.zero;
            var fitter = logTextGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = logTextRt;
        }

        void BuildInputRow()
        {
            // Architecture: TMP_InputField forces its textComponent.rectTransform to fill the
            // textViewport (and re-asserts that on layout / focus / text changes). To get a
            // chevron strip without fighting that override, we give the InputField a SMALLER
            // viewport that already excludes the chevron's space. The ghost overlay then sits
            // inside the same viewport with the same fill — both texts always start at x=0 of
            // the viewport, which is the same physical position, so glyphs always align.
            const float LeftTextPad  = 26f; // chevron strip on the left of the card
            const float RightTextPad = 6f;
            const float RunButtonW   = 62f; // explicit submit button on the right of the card

            // Card: elevated rounded surface that sits inside the window with a small inset.
            var inputGo = new GameObject("Input", typeof(Image));
            inputGo.transform.SetParent(_window.transform, false);
            var inputRt = (RectTransform)inputGo.transform;
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot     = new Vector2(0f, 0f);
            inputRt.offsetMin = new Vector2(PADDING, PADDING * 0.5f);
            inputRt.offsetMax = new Vector2(-PADDING, InputRowHeight + PADDING * 0.5f);
            var inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = Theme.Rounded;
            inputBg.type = Image.Type.Sliced;
            inputBg.pixelsPerUnitMultiplier = 1.5f;
            inputBg.color = Theme.BgElevated;

            // Decorative chevron — purely visual. raycastTarget=false so it never steals clicks
            // away from the input field beneath it.
            var chevronGo = new GameObject("Chevron", typeof(RectTransform));
            chevronGo.transform.SetParent(inputGo.transform, false);
            var chevronRt = (RectTransform)chevronGo.transform;
            chevronRt.anchorMin = new Vector2(0f, 0f);
            chevronRt.anchorMax = new Vector2(0f, 1f);
            chevronRt.pivot     = new Vector2(0f, 0.5f);
            chevronRt.anchoredPosition = Vector2.zero;
            chevronRt.sizeDelta = new Vector2(LeftTextPad, 0f);
            var chevron = chevronGo.AddComponent<TextMeshProUGUI>();
            chevron.font = TMP_Settings.defaultFontAsset;
            chevron.fontSize = DevConsoleSettings.FontSize + 2;
            chevron.color = Theme.AccentMuted;
            chevron.text = "›";
            chevron.alignment = TextAlignmentOptions.Center;
            chevron.raycastTarget = false;

            // Viewport: the rect TMP_InputField uses for clipping/scrolling. We make it smaller
            // than inputGo so the chevron strip on the left is preserved no matter what the
            // input field does to its textComponent.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(inputGo.transform, false);
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = new Vector2(0f, 0f);
            viewportRt.anchorMax = new Vector2(1f, 1f);
            viewportRt.pivot     = new Vector2(0f, 0.5f);
            viewportRt.offsetMin = new Vector2(LeftTextPad, 2f);
            viewportRt.offsetMax = new Vector2(-(RightTextPad + RunButtonW), -2f); // leave room for Run button

            _input = inputGo.AddComponent<TMP_InputField>();

            // Ghost text overlay — fills the viewport (the SAME rect TMP_InputField forces its
            // textComponent into). Both texts therefore share an origin at the viewport's left
            // edge, so "<transparent>typed</transparent><gray>suffix</gray>" lines up exactly
            // with the rendered input text. Ghost lives directly under the viewport so the
            // RectMask2D clips it to the same area as the typed text.
            var ghostGo = new GameObject("GhostText", typeof(RectTransform));
            ghostGo.transform.SetParent(viewportGo.transform, false);
            _ghostText = ghostGo.AddComponent<TextMeshProUGUI>();
            _ghostText.font = TMP_Settings.defaultFontAsset;
            _ghostText.fontSize = DevConsoleSettings.FontSize;
            _ghostText.color = Color.white;
            _ghostText.textWrappingMode = TextWrappingModes.NoWrap;
            _ghostText.richText = true;
            _ghostText.raycastTarget = false;
            _ghostText.margin = Vector4.zero;
            _ghostText.alignment = TextAlignmentOptions.MidlineLeft;
            var ghostRt = _ghostText.rectTransform;
            ghostRt.anchorMin = Vector2.zero;
            ghostRt.anchorMax = Vector2.one;
            ghostRt.offsetMin = Vector2.zero;
            ghostRt.offsetMax = Vector2.zero;
            ghostGo.transform.SetAsFirstSibling(); // draw BEHIND the input's own text

            // Input's own text component (drawn on top of the ghost). TMP_InputField will
            // force this rect to fill the viewport — that's exactly what we want.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(viewportGo.transform, false);
            var inputText = textGo.AddComponent<TextMeshProUGUI>();
            inputText.font = TMP_Settings.defaultFontAsset;
            inputText.fontSize = DevConsoleSettings.FontSize;
            inputText.color = Theme.TextPrimary;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            inputText.margin = Vector4.zero;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            var inputTextRt = inputText.rectTransform;
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = Vector2.zero;
            inputTextRt.offsetMax = Vector2.zero;

            _input.textViewport   = viewportRt;
            _input.textComponent  = inputText;
            _input.fontAsset      = TMP_Settings.defaultFontAsset;
            _input.pointSize      = DevConsoleSettings.FontSize;
            _input.lineType       = TMP_InputField.LineType.SingleLine;
            _input.richText       = false;
            _input.restoreOriginalTextOnEscape = false;
            _input.shouldHideMobileInput = true;
            // Match the chrome — cyan caret + transparent selection tinted with accent.
            _input.caretColor = Theme.Accent;
            _input.customCaretColor = true;
            _input.caretWidth = 2;
            _input.caretBlinkRate = 0.85f;
            _input.selectionColor = Theme.AccentSoft;
            // Disable selectable navigation so Tab isn't consumed by EventSystem traversal.
            _input.navigation = new Navigation { mode = Navigation.Mode.None };
            _input.onValueChanged.AddListener(OnInputValueChanged);
            // Soft-keyboard return/done executes the command — the mobile equivalent of Enter,
            // which HandleInputKeys only reads from the (phone-absent) hardware Keyboard.
            _input.onSubmit.AddListener(OnInputSubmit);

            // Bottom focus ring — 1px cyan underline that lights up only while the input is
            // the active EventSystem selection. Driven from LateUpdate's focus poll.
            var ringGo = new GameObject("FocusRing", typeof(Image));
            ringGo.transform.SetParent(inputGo.transform, false);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = new Vector2(0f, 0f);
            ringRt.anchorMax = new Vector2(1f, 0f);
            ringRt.pivot     = new Vector2(0.5f, 0f);
            ringRt.anchoredPosition = new Vector2(0f, 0f);
            ringRt.sizeDelta = new Vector2(-12f, 2f);
            _inputFocusRing = ringGo.GetComponent<Image>();
            _inputFocusRing.color = Theme.Accent;
            _inputFocusRing.raycastTarget = false;
            _inputFocusRing.enabled = false;

            // Run button — explicit submit affordance for touch. The soft keyboard's return key
            // (wired via onSubmit) is the primary path, but its behavior varies across Android
            // ROMs/keyboards; this button is the always-works fallback. Sits inside the card on
            // the right; the viewport above was shrunk by RunButtonW to make room.
            var runGo = new GameObject("RunButton", typeof(Image), typeof(Button));
            runGo.transform.SetParent(inputGo.transform, false);
            var runRt = (RectTransform)runGo.transform;
            runRt.anchorMin = new Vector2(1f, 0f);
            runRt.anchorMax = new Vector2(1f, 1f);
            runRt.pivot     = new Vector2(1f, 0.5f);
            runRt.anchoredPosition = new Vector2(-3f, 0f);
            runRt.sizeDelta = new Vector2(RunButtonW - 6f, -8f); // inset from the card edges
            var runImg = runGo.GetComponent<Image>();
            runImg.sprite = Theme.Rounded;
            runImg.type = Image.Type.Sliced;
            runImg.pixelsPerUnitMultiplier = 1.5f;
            runImg.color = Color.white; // tinted by ColorBlock below
            var runBtn = runGo.GetComponent<Button>();
            runBtn.transition = Selectable.Transition.ColorTint;
            runBtn.colors = new ColorBlock
            {
                normalColor      = Theme.AccentSoft,
                highlightedColor = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.28f),
                pressedColor     = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.42f),
                selectedColor    = Theme.AccentSoft,
                disabledColor    = Theme.AccentSoft,
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f,
            };
            // Selection navigation off so the button never traps EventSystem focus (the per-frame
            // Pointer re-focus in LateUpdate restores the input field after the tap anyway).
            runBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            runBtn.onClick.AddListener(Submit);

            var runLabelGo = new GameObject("Label", typeof(RectTransform));
            runLabelGo.transform.SetParent(runGo.transform, false);
            var runLabel = runLabelGo.AddComponent<TextMeshProUGUI>();
            runLabel.font = TMP_Settings.defaultFontAsset;
            runLabel.fontStyle = FontStyles.Bold;
            runLabel.color = Theme.Accent;
            runLabel.text = "Run"; // ASCII — guaranteed in the default TMP font (a ▶ glyph is not)
            runLabel.alignment = TextAlignmentOptions.Center;
            runLabel.textWrappingMode = TextWrappingModes.NoWrap; // never wrap "Run" onto two lines
            // Auto-size the label to its rect so "Run" always fits the button regardless of the
            // configured console font size — shrinks to fit, never overflows the card.
            runLabel.enableAutoSizing = true;
            runLabel.fontSizeMin = 8f;
            runLabel.fontSizeMax = DevConsoleSettings.FontSize;
            runLabel.raycastTarget = false;
            var runLabelRt = runLabel.rectTransform;
            runLabelRt.anchorMin = Vector2.zero;
            runLabelRt.anchorMax = Vector2.one;
            runLabelRt.offsetMin = new Vector2(4f, 0f);  // small horizontal breathing room so
            runLabelRt.offsetMax = new Vector2(-4f, 0f); // auto-size has padding to fit within
        }

        /// <summary>
        /// Build 8 invisible resize handles around the window's perimeter: 4 thin edge strips
        /// + 4 corner squares. Corners are added last so they're drawn on top of the edges at
        /// the corners (uGUI raycast picks the topmost graphic at a point). All are mostly
        /// transparent — a faint tint on the edges/corners hints at where the handles live.
        /// </summary>
        /// <summary>
        /// Build the suggestion dropdown — a vertical stack of N rows that sits directly above
        /// the input field. Anchored to the input row's top so it always tracks the input even
        /// when the window is resized. Rows are pre-allocated (one TMP_Text + parent Image each)
        /// and only their text/visibility changes per keystroke.
        /// </summary>
        void BuildSuggestionPanel()
        {
            _suggestionPanel = new GameObject("SuggestionPanel", typeof(Image));
            _suggestionPanel.transform.SetParent(_window.transform, false);
            var panelRt = (RectTransform)_suggestionPanel.transform;
            // Anchor: bottom-stretched, pivot at bottom-left, floating just above the input row.
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot     = new Vector2(0f, 0f);
            panelRt.anchoredPosition = new Vector2(PADDING, InputRowHeight + PADDING + 6f);
            panelRt.sizeDelta = new Vector2(-PADDING * 2f, SuggestionRowHeight * MAX_VISIBLE_SUGGESTIONS);
            var panelImg = _suggestionPanel.GetComponent<Image>();
            panelImg.sprite = Theme.Rounded;
            panelImg.type = Image.Type.Sliced;
            panelImg.pixelsPerUnitMultiplier = 1.5f;
            panelImg.color = Theme.BgElevated;
            _suggestionPanel.SetActive(false);

            // Hairline border around the panel (drawn after rows so it sits on top visually).
            var border = new GameObject("PanelBorder", typeof(Image));
            border.transform.SetParent(_suggestionPanel.transform, false);
            var borderRt = (RectTransform)border.transform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            var borderImg = border.GetComponent<Image>();
            borderImg.sprite = Theme.Rounded;
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = 1.5f;
            borderImg.color = Theme.BorderSubtle;
            borderImg.raycastTarget = false;

            for (int i = 0; i < MAX_VISIBLE_SUGGESTIONS; i++)
            {
                // Row container — holds the selection background + an accent stripe + the label.
                var rowGo = new GameObject($"Row{i}", typeof(Image));
                rowGo.transform.SetParent(_suggestionPanel.transform, false);
                var rowRt = (RectTransform)rowGo.transform;
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot     = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -SuggestionRowHeight * i);
                rowRt.sizeDelta = new Vector2(0f, SuggestionRowHeight);
                var rowImg = rowGo.GetComponent<Image>();
                rowImg.color = new Color(0f, 0f, 0f, 0f); // bg tinted in RefreshSuggestionRows
                rowImg.raycastTarget = true; // tappable on touch = accept this completion (Tab equiv.)

                // Touch handler: tapping the row accepts its completion. Captures the visual row
                // index; AcceptSuggestionRow maps it through the scroll offset at tap time.
                var rowClick = rowGo.AddComponent<ConsoleSuggestionRow>();
                rowClick.VisualIndex = i;
                rowClick.Clicked = AcceptSuggestionRow;

                // Left accent stripe — visible only when this row is selected. Sized to inset
                // slightly so it doesn't clip with the panel's rounded corners.
                var stripeGo = new GameObject("AccentStripe", typeof(Image));
                stripeGo.transform.SetParent(rowGo.transform, false);
                var stripeRt = (RectTransform)stripeGo.transform;
                stripeRt.anchorMin = new Vector2(0f, 0f);
                stripeRt.anchorMax = new Vector2(0f, 1f);
                stripeRt.pivot     = new Vector2(0f, 0.5f);
                stripeRt.anchoredPosition = new Vector2(3f, 0f);
                stripeRt.sizeDelta = new Vector2(2f, -8f); // shrink vertically for a "pill" look
                var stripeImg = stripeGo.GetComponent<Image>();
                stripeImg.color = Theme.Accent;
                stripeImg.raycastTarget = false;
                stripeImg.enabled = false; // toggled in RefreshSuggestionRows
                stripeImg.name = "AccentStripe"; // used by lookup below

                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(rowGo.transform, false);
                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.font = TMP_Settings.defaultFontAsset;
                text.fontSize = DevConsoleSettings.FontSize;
                text.color = Theme.TextPrimary;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.richText = true;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                var textRt = text.rectTransform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(14f, 0f); // leave room for the accent stripe
                textRt.offsetMax = new Vector2(-10f, 0f);

                _suggestionRows[i] = text;
            }

            // Make sure the panel border draws on top of all rows.
            border.transform.SetAsLastSibling();
        }

        void BuildResizeHandles()
        {
            // Thin tint on edges, slightly stronger on corners. Tweak alphas if you want them
            // more visible — they're mostly proprioceptive cues for cursor positioning.
            // Invisible — chrome is enough of a hint about the window boundary. We keep the
            // raycast targets so the cursor still flips to resize on hover.
            Color edgeColor   = new(0f, 0f, 0f, 0.001f);
            Color cornerColor = new(0f, 0f, 0f, 0.001f);

            // All handles use a CENTRED pivot anchored ON the edge/corner line, so the grab zone
            // straddles the border (half inside the window, half outside it). Grabbing from just
            // outside the border is natural on touch and means the inside half being covered by
            // content (log scroll, input row) still leaves the outside half usable.

            // ── 4 edges ── (centred on each side line, stretched along it)
            // Top edge: full width, centred on the top border line.
            CreateResizeHandle("ResizeTop", ResizeEdges.Top, edgeColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, EDGE_HANDLE_THICKNESS));
            // Bottom edge: full width, centred on the bottom border line.
            CreateResizeHandle("ResizeBottom", ResizeEdges.Bottom, edgeColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, EDGE_HANDLE_THICKNESS));
            // Left edge: full height, centred on the left border line.
            CreateResizeHandle("ResizeLeft", ResizeEdges.Left, edgeColor,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(EDGE_HANDLE_THICKNESS, 0f));
            // Right edge: full height, centred on the right border line.
            CreateResizeHandle("ResizeRight", ResizeEdges.Right, edgeColor,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(EDGE_HANDLE_THICKNESS, 0f));

            // ── 4 corners ── (added LAST so they win raycast over the edges at the intersections)
            Vector2 corner = new(CORNER_HANDLE_SIZE, CORNER_HANDLE_SIZE);
            CreateResizeHandle("ResizeTopLeft", ResizeEdges.Top | ResizeEdges.Left, cornerColor,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, corner);
            CreateResizeHandle("ResizeTopRight", ResizeEdges.Top | ResizeEdges.Right, cornerColor,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, corner);
            CreateResizeHandle("ResizeBottomLeft", ResizeEdges.Bottom | ResizeEdges.Left, cornerColor,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, corner);
            CreateResizeHandle("ResizeBottomRight", ResizeEdges.Bottom | ResizeEdges.Right, cornerColor,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, corner);
        }

        void CreateResizeHandle(string name, ResizeEdges edges, Color tint,
                                Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(Image), typeof(ConsoleWindowResizeHandle));
            go.transform.SetParent(_window.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            go.GetComponent<Image>().color = tint;
            var handle = go.GetComponent<ConsoleWindowResizeHandle>();
            handle._owner = this;
            handle._edges = edges;
        }
    }
}
