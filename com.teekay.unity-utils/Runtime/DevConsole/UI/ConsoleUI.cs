using System.Collections.Generic;
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
    internal sealed class ConsoleUI : MonoBehaviour
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
        //  Theme — thin aliases over DevConsoleSettings, which is the single theming surface
        //  (chrome and content colors live together there and are config-editable). Translucent
        //  accent variants are derived from AccentColor so retinting stays a one-field change.
        // ─────────────────────────────────────────────────────────────────────
        static class Theme
        {
            public static Color BgWindow   => DevConsoleSettings.ChromeWindowColor;
            public static Color BgElevated => DevConsoleSettings.ChromeElevatedColor;
            public static Color BgHover    => DevConsoleSettings.ChromeHoverColor;

            // Borders / separators — almost-invisible white on dark.
            public static readonly Color BorderSubtle  = new(1f, 1f, 1f, 0.05f);
            public static readonly Color SeparatorLine = new(1f, 1f, 1f, 0.07f);

            public static Color TextPrimary => DevConsoleSettings.ChromeTextPrimary;
            public static Color TextMuted   => DevConsoleSettings.ChromeTextMuted;
            public static Color TextSubtle  => DevConsoleSettings.ChromeTextSubtle;

            public static Color Accent      => DevConsoleSettings.AccentColor;
            public static Color AccentMuted => WithAlpha(DevConsoleSettings.AccentColor, 0.55f);
            public static Color AccentSoft  => WithAlpha(DevConsoleSettings.AccentColor, 0.14f);

            // Close button — transparent at rest, soft error tint on hover.
            public static readonly Color CloseIdle = new(0f, 0f, 0f, 0f);
            public static Color CloseHover => WithAlpha(DevConsoleSettings.ErrorAccentColor, 0.18f);

            // Selection background for suggestion rows (accent tinted, mostly transparent).
            public static Color RowSelected => WithAlpha(DevConsoleSettings.AccentColor, 0.16f);

            /// <summary>Console font: the configured asset, or TMP's default when unset.</summary>
            public static TMP_FontAsset Font =>
                DevConsoleSettings.FontAsset != null ? DevConsoleSettings.FontAsset
                                                     : TMP_Settings.defaultFontAsset;

            static Color WithAlpha(Color c, float a) { c.a = a; return c; }

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
        CanvasGroup _windowGroup;  // open/close fade
        ScrollRect _scroll;
        RectTransform _scrollRt;
        ConsoleLogView _logView;
        TMP_InputField _input;
        TMP_Text _ghostText;
        RectTransform _ghostRt;
        TMP_Text _inputTextComponent; // the input's own text — measured to place the ghost
        Image _inputBg;            // flashed toward the error accent on failed commands
        Image _inputFocusRing;     // 1px cyan underline that lights up while typing
        EventSystem _createdEventSystem;   // non-null only if the console spawned its own EventSystem

        // — Toolbar / filter row —
        TMP_InputField _searchInput;
        GameObject _filterRow;
        RectTransform _filterChipStrip;
        readonly List<(RectTransform rt, float width)> _filterChips = new();
        int _filterChipLines = 1;
        float _lastChipLayoutWidth = -1f;
        TMP_Text _filterButtonLabel;
        GameObject _jumpPill;
        TMP_Text _jumpPillLabel;

        // — Animation / feedback state (unscaled time — the console pauses the game on focus) —
        const float OPEN_ANIM_SECONDS = 0.12f;
        const float SLIDE_PIXELS = 8f;
        // Ghost rect is the input text's rect shifted right by the typed width, so it needs slack
        // on the right or a long line would clip the suffix. Clipped by the viewport mask anyway.
        const float GHOST_EXTRA_WIDTH = 400f;
        float _openAnim;       // 0 = fully closed, 1 = fully open
        float _openAnimTarget;
        float _errorFlash;     // 1 → 0 after a failed command

        // — Suggestion dropdown —
        // Visible rows are fixed (panel footprint matches the original 5-row design); the
        // collection cap is larger so Up/Down can cycle through every match by scrolling the
        // visible window.
        const int MAX_VISIBLE_SUGGESTIONS = 5;
        const int MAX_COLLECTED_SUGGESTIONS = 64;
        GameObject _suggestionPanel;
        readonly TMP_Text[] _suggestionRows = new TMP_Text[MAX_VISIBLE_SUGGESTIONS];
        readonly ConsoleSuggestionRow[] _suggestionRowHandlers = new ConsoleSuggestionRow[MAX_VISIBLE_SUGGESTIONS];
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

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public void Bind(DevConsole console)
        {
            _console = console;
            _touchMode = Application.isMobilePlatform;
            DevConsole.OnLogAppended += OnLogAppended;
            DevConsole.OnLogCleared  += OnLogCleared;
            DevConsole.OnExecuteFailed += OnExecuteFailed;
            EnsureBuilt();
        }

        void OnDestroy()
        {
            if (_console != null)
            {
                DevConsole.OnLogAppended -= OnLogAppended;
                DevConsole.OnLogCleared  -= OnLogCleared;
                DevConsole.OnExecuteFailed -= OnExecuteFailed;
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
            _openAnimTarget = open ? 1f : 0f;
            if (open)
            {
                // Activate immediately and fade in; focus is granted right away so the user can
                // type before the (120ms, unscaled) tween finishes — animation never gates input.
                _window.SetActive(true);
                if (_windowGroup != null)
                {
                    // Interactive from frame one — only the visuals fade in.
                    _windowGroup.interactable = true;
                    _windowGroup.blocksRaycasts = true;
                }
                // Create an EventSystem only when actually opening and the scene has none.
                EnsureEventSystem();
                _logView?.MarkDirty();
                _logView?.ScrollToBottom();
                FocusInput();
            }
            else
            {
                // Drop focus and clear text NOW — the game unpauses instantly; only the fade-out
                // is deferred. SetActive(false) happens in the tween when alpha reaches zero.
                if (_input != null) _input.text = string.Empty;
                if (_ghostText != null) _ghostText.text = string.Empty;
                if (EventSystem.current != null && _input != null &&
                    EventSystem.current.currentSelectedGameObject == _input.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                // Reset focus tracking so a later re-open re-fires NotifyFocusChanged(true) and
                // re-applies pause/cursor side effects (the LateUpdate poll is skipped while the
                // window is inactive, so without this the stale "true" would suppress the next open).
                _lastFocusState = false;
                _touchFocused   = false;
                if (_windowGroup != null)
                {
                    // No interaction with a closing window — clicks fall through to the game.
                    _windowGroup.interactable = false;
                    _windowGroup.blocksRaycasts = false;
                }
                // Already fully faded (initial close at startup, or close-while-closed): the
                // tween would early-out on target == current, so deactivate here.
                if (Mathf.Approximately(_openAnim, 0f)) _window.SetActive(false);
            }
        }

        /// <summary>
        /// Open/close tween — unscaled time, because the console pauses the game (timeScale = 0)
        /// while focused; scaled time would freeze the animation exactly when it plays.
        /// Runs in Update so it still advances while LateUpdate's early-out is irrelevant.
        /// </summary>
        void Update()
        {
            if (_windowGroup == null || Mathf.Approximately(_openAnim, _openAnimTarget)) return;

            _openAnim = Mathf.MoveTowards(_openAnim, _openAnimTarget,
                Time.unscaledDeltaTime / OPEN_ANIM_SECONDS);
            float eased = 1f - (1f - _openAnim) * (1f - _openAnim); // ease-out quad
            _windowGroup.alpha = eased;
            // Slight downward settle on open (window slides from a few px above its rest spot).
            _windowRt.anchoredPosition = _windowPos + new Vector2(0f, SLIDE_PIXELS * (1f - eased));

            if (Mathf.Approximately(_openAnim, 1f))
            {
                _windowGroup.interactable = true;
                _windowGroup.blocksRaycasts = true;
                _windowRt.anchoredPosition = _windowPos;
            }
            else if (Mathf.Approximately(_openAnim, 0f))
            {
                _window.SetActive(false);
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

            // A narrower window fits fewer chips per line, so the wrap has to be redone — chips
            // stay clipped otherwise, which is the whole complaint the wrapping layout fixes.
            if (_filterRow != null && _filterRow.activeSelf &&
                !Mathf.Approximately(_lastChipLayoutWidth, _windowSize.x))
                LayoutFilterChips();
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

        void OnLogAppended(ConsoleLogEntry _) => _logView?.NotifyAppended();
        void OnLogCleared() => _logView?.NotifyCleared();
        void OnExecuteFailed() => _errorFlash = 1f;

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
                else if (insideConsole && !IsConsoleFieldSelected())
                {
                    // Don't steal selection from the filter search field — it's part of the
                    // console too; yanking focus to the command input would make it untypable.
                    EventSystem.current.SetSelectedGameObject(_input.gameObject);
                    _input.ActivateInputField();
                }
            }

            // Poll focus state. Touch mode uses the press-region flag (above); desktop uses the
            // EventSystem selection (command input OR filter search — both count as "typing into
            // the console" for pause/cursor purposes). A transition tells DevConsole to
            // apply/unwind pause + cursor + input-block side effects.
            bool focused = _touchMode ? _touchFocused : IsConsoleFieldSelected();
            if (focused != _lastFocusState)
            {
                _lastFocusState = focused;
                _console?.NotifyFocusChanged(focused);
                if (_inputFocusRing != null) _inputFocusRing.enabled = focused;
            }

            // Only consume keyboard when the COMMAND input has focus (not the search field —
            // Enter there must not execute a command). Otherwise the user is playing the game
            // with the console open as a monitor — keys pass through to gameplay.
            if (focused && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == _input.gameObject)
                HandleInputKeys();

            float dt = Time.unscaledDeltaTime;
            _logView?.Tick(dt);
            UpdateGhostPosition();
            UpdateErrorFlash(dt);
            UpdateJumpPill();
        }

        bool IsConsoleFieldSelected()
        {
            if (EventSystem.current == null) return false;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return false;
            return (_input != null && selected == _input.gameObject)
                || (_searchInput != null && selected == _searchInput.gameObject);
        }

        // Failed command → the input card and focus ring pulse toward the error accent, then
        // settle back. Complements the error line in the log, which is easy to miss mid-typing.
        void UpdateErrorFlash(float unscaledDeltaTime)
        {
            if (_errorFlash <= 0f || _inputBg == null) return;
            _errorFlash = Mathf.Max(0f, _errorFlash - unscaledDeltaTime * 2.5f);

            Color error = DevConsoleSettings.ErrorAccentColor;
            Color cardError = Color.Lerp(Theme.BgElevated, error, 0.35f);
            _inputBg.color = Color.Lerp(Theme.BgElevated, cardError, _errorFlash);
            if (_inputFocusRing != null)
                _inputFocusRing.color = Color.Lerp(Theme.Accent, error, _errorFlash);
        }

        // "↓ N new" pill — visible only when scrolled up while new lines arrive. Hidden while the
        // suggestion dropdown is up (they'd overlap; typing takes precedence).
        void UpdateJumpPill()
        {
            if (_jumpPill == null || _logView == null) return;
            bool show = _logView.UnseenCount > 0 && !_logView.AtBottom
                        && (_suggestionPanel == null || !_suggestionPanel.activeSelf);
            if (_jumpPill.activeSelf != show) _jumpPill.SetActive(show);
            // ASCII only — arrow glyphs (↓/▼) aren't guaranteed in the default TMP font atlas.
            if (show) _jumpPillLabel.text = $"{_logView.UnseenCount} new";
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
        /// Place the ghost suffix at the pen position of the input's OWN rendered text.
        /// <para>
        /// This is the third alignment approach, and the only correct one. A transparent-prefix
        /// spacer and an independent width measurement both re-derive TMP's layout from outside,
        /// and both drifted — TMP_InputField shifts its text component's rect to keep the caret
        /// in view (reserving margin + caret width), which no external measurement sees. Reading
        /// <c>textInfo.characterInfo[last].xAdvance</c> from the component itself is ground truth
        /// by construction: kerning, spacing and the field's scroll offset are already in it.
        /// Runs in LateUpdate; ForceMeshUpdate makes textInfo current for THIS frame's string
        /// (TMP normally defers mesh regeneration to the canvas update, which happens after
        /// LateUpdate), so the ghost lands correctly with no one-frame lag.
        /// </para>
        /// </summary>
        void UpdateGhostPosition()
        {
            if (_ghostText == null || _ghostRt == null || _inputTextComponent == null) return;
            if (string.IsNullOrEmpty(_ghostText.text)) return;

            RectTransform src = _inputTextComponent.rectTransform;

            // Mirror the input's text rect outright — anchors, pivot, size. Both are children of
            // the same viewport, so this gives the ghost an identical coordinate frame and it
            // inherits the field's VERTICAL placement rather than guessing at it. Only X is then
            // offset; leaving the ghost its own rect is what left it sitting too high.
            _ghostRt.anchorMin = src.anchorMin;
            _ghostRt.anchorMax = src.anchorMax;
            _ghostRt.pivot     = src.pivot;
            // Widening grows the rect around the PIVOT, so with the input text's centred pivot the
            // left edge — where MidlineLeft starts drawing — slides left by extra * pivot.x. That
            // pushed the whole ghost out of the viewport mask. Compensate below.
            _ghostRt.sizeDelta = src.sizeDelta + new Vector2(GHOST_EXTRA_WIDTH, 0f);

            // Pen position after the typed text, in that shared frame. Empty input starts exactly
            // where the input's own text starts.
            float penX = src.rect.xMin;
            string typed = _input != null ? _input.text : string.Empty;
            if (!string.IsNullOrEmpty(typed))
            {
                _inputTextComponent.ForceMeshUpdate();
                TMP_TextInfo info = _inputTextComponent.textInfo;
                int last = Mathf.Min(info.characterCount, typed.Length) - 1;
                if (last >= 0) penX = info.characterInfo[last].xAdvance;
            }

            _ghostRt.anchoredPosition = src.anchoredPosition
                + new Vector2(penX - src.rect.xMin + GHOST_EXTRA_WIDTH * src.pivot.x, 0f);
        }

        /// <summary>
        /// Update the ghost overlay's CONTENT. Its position is applied in LateUpdate by
        /// <see cref="UpdateGhostPosition"/> — TMP defers mesh regeneration to the canvas
        /// update, so at onValueChanged time textInfo still describes the previous string.
        /// </summary>
        void UpdateGhost(string text)
        {
            if (_ghostText == null) return;
            if (ConsoleAutocomplete.TryGetCompletion(text, DevConsole.Registry, out string suffix))
            {
                _ghostText.color = DevConsoleSettings.HintColor;
                _ghostText.text = suffix;
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

            // Text tint hexes derived from the theme so a retint carries through rich text too.
            string accentHex  = ColorUtility.ToHtmlStringRGB(Theme.Accent);
            string mutedHex   = ColorUtility.ToHtmlStringRGB(Theme.TextMuted);
            string primaryHex = ColorUtility.ToHtmlStringRGB(Theme.TextPrimary);
            string subtleHex  = ColorUtility.ToHtmlStringRGB(Theme.TextSubtle);

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

                // Tag colors: accent for CVar (value-bearing), muted gray for Cmd (action).
                string kind = m.IsCVar ? "VAR" : "CMD";
                string kindColor = m.IsCVar ? "#" + accentHex : "#" + mutedHex;
                string nameColor = selected ? "#FFFFFF" : "#" + primaryHex;
                string descColor = selected ? "#" + mutedHex : "#" + subtleHex;

                string body = $"<color={kindColor}><size=80%><b>{kind}</b></size></color>  " +
                              $"<color={nameColor}><b>{m.Name}</b></color>";
                if (!string.IsNullOrEmpty(m.Description))
                    body += $"  <color={descColor}>{m.Description}</color>";

                // Append "(n/total)" position indicator to the highlighted row when scrolling is
                // in play, so the user can see where they are in the full list.
                if (selected && _matchesBuf.Count > MAX_VISIBLE_SUGGESTIONS)
                    body += $"  <color=#{subtleHex}><size=80%>{_selectedSuggestion + 1}/{_matchesBuf.Count}</size></color>";

                _suggestionRows[i].text = body;

                // Selection chrome: tint background (hover beats zebra, selection beats hover),
                // light up the left accent stripe.
                var rowParent = _suggestionRows[i].transform.parent;
                var rowImage = rowParent.GetComponent<Image>();
                bool hovered = _suggestionRowHandlers[i] != null && _suggestionRowHandlers[i].Hovered;
                rowImage.color = selected ? Theme.RowSelected
                               : hovered ? Theme.BgHover
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
            BuildFilterRow();
            BuildInputRow();
            BuildSuggestionPanel();
            RefreshToolbarStates();

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
            _window = new GameObject("Window", typeof(Image), typeof(CanvasGroup));
            _window.transform.SetParent(_canvas.transform, false);
            _windowRt = (RectTransform)_window.transform;
            _windowGroup = _window.GetComponent<CanvasGroup>();
            _windowGroup.alpha = 0f; // faded in by the open tween
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
            label.font = Theme.Font;
            label.fontSize = DevConsoleSettings.FontSize - 1;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 4f;       // gives the uppercase title some breathing room
            label.color = Theme.TextMuted;
            label.text = "DEV CONSOLE";
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            // Never wrap: the toolbar eats into this label's width, and on a narrow window a
            // wrapped title spills out of the (fixed-height) title bar instead of shrinking.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
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
            closeLabel.font = Theme.Font;
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

            // Toolbar — compact text buttons stacked right-to-left from the close button.
            // Text labels rather than icons: arbitrary glyphs aren't guaranteed in the default
            // TMP font atlas, and at this size a word beats an ambiguous pictogram anyway.
            float edge = TitleBarHeight + 2f; // left edge of the close button
            edge += MakeTitleButton(titleGo.transform, "Clear", edge,
                () => _console?.ClearLog(), out _);
            edge += MakeTitleButton(titleGo.transform, "Copy", edge, CopyAllToClipboard, out _);
            edge += MakeTitleButton(titleGo.transform, "Filter", edge, ToggleFilterRow,
                out _filterButtonLabel);
            // Shrink the title label so it never runs under the buttons on a narrow window.
            labelRt.offsetMax = new Vector2(-(edge + 8f), 0f);

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

        /// <summary>
        /// Compact title-bar text button. Stacks right-to-left: <paramref name="rightEdge"/> is
        /// the distance from the bar's right edge to this button's right side. Returns the
        /// horizontal space consumed (width + gap) so the caller can accumulate.
        /// </summary>
        float MakeTitleButton(Transform titleBar, string label, float rightEdge,
                              UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
        {
            var go = new GameObject($"Toolbar{label}", typeof(Image), typeof(Button));
            go.transform.SetParent(titleBar, false);

            var text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(go.transform, false);
            text.font = Theme.Font;
            text.fontSize = Mathf.Max(9f, DevConsoleSettings.FontSize - 3);
            text.fontStyle = FontStyles.Bold;
            text.color = Theme.TextMuted;
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            float width = text.GetPreferredValues(label).x + 16f;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-rightEdge, 0f);
            rt.sizeDelta = new Vector2(width, TitleBarHeight - 10f);

            var img = go.GetComponent<Image>();
            img.sprite = Theme.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.5f;
            img.color = Color.white; // tinted by the ColorBlock

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor      = new Color(0f, 0f, 0f, 0f),
                highlightedColor = Theme.BgHover,
                pressedColor     = Theme.AccentSoft,
                selectedColor    = new Color(0f, 0f, 0f, 0f),
                disabledColor    = new Color(0f, 0f, 0f, 0f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f,
            };
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(onClick);

            labelText = text;
            return width + 4f;
        }

        void CopyAllToClipboard()
        {
            if (_logView == null) return;
            GUIUtility.systemCopyBuffer = _logView.BuildPlainText();
        }

        void ToggleFilterRow()
        {
            if (_filterRow == null) return;
            bool show = !_filterRow.activeSelf;
            _filterRow.SetActive(show);
            if (show)
            {
                RebuildFilterChips();
            }
            else
            {
                // Hiding the row clears its filters — hidden-but-active filtering would look
                // like lost log lines with no visible cause.
                if (_searchInput != null) _searchInput.text = string.Empty;
                _logView?.SetSearch(string.Empty);
            }
            ApplyLogScrollOffsets();
            RefreshToolbarStates();
        }

        // Active toggles read as accent-colored labels; inactive ones stay muted.
        void RefreshToolbarStates()
        {
            if (_filterButtonLabel != null)
                _filterButtonLabel.color =
                    _filterRow != null && _filterRow.activeSelf ? Theme.Accent : Theme.TextMuted;
        }

        const float CHIP_GAP = 4f;
        const float SEARCH_WIDTH = 150f;

        /// <summary>Height of one chip / the search box (font-scaled, like the other chrome rows).</summary>
        float ChipHeight => DevConsoleSettings.FontSize + 6f;

        /// <summary>
        /// Filter-row height. Grows with the number of lines the category chips wrapped onto, so
        /// every filter stays visible — the point of the row is seeing which categories exist, and
        /// clipping (or hiding behind a horizontal scroll) defeats that.
        /// </summary>
        float FilterRowHeight => CHIP_GAP + Mathf.Max(1, _filterChipLines) * (ChipHeight + CHIP_GAP);

        /// <summary>Width available to chips, derived from the window rather than a possibly-stale rect.</summary>
        float ChipStripWidth =>
            Mathf.Max(40f, _windowSize.x - PADDING * 2f - (SEARCH_WIDTH + 12f) - 5f);

        /// <summary>
        /// Position the log viewport between the title bar (plus the filter row when visible)
        /// and the input row. Re-run when the filter row is toggled.
        /// </summary>
        void ApplyLogScrollOffsets()
        {
            if (_scrollRt == null) return;
            float top = TitleBarHeight + 4f;
            if (_filterRow != null && _filterRow.activeSelf) top += FilterRowHeight + 4f;
            _scrollRt.offsetMin = new Vector2(PADDING + 2f, InputRowHeight + PADDING);
            _scrollRt.offsetMax = new Vector2(-(PADDING + 2f), -top);
        }

        void BuildLogScroll()
        {
            var scrollGo = new GameObject("LogScroll", typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(_window.transform, false);
            _scrollRt = (RectTransform)scrollGo.transform;
            _scrollRt.anchorMin = new Vector2(0f, 0f);
            _scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;
            _scroll.viewport = _scrollRt;
            ApplyLogScrollOffsets();

            // Content: an empty rect the virtualized view sizes to the summed row heights.
            // Pooled rows are its children, manually positioned — no layout groups (deliberate).
            var contentGo = new GameObject("LogContent", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            _scroll.content = contentRt;

            // Thin scrollbar — the affordance the old console lacked entirely. AutoHide (not
            // AutoHideAndExpandViewport) so its appearance never reflows the content width.
            var scrollbarGo = new GameObject("Scrollbar", typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollGo.transform, false);
            var scrollbarRt = (RectTransform)scrollbarGo.transform;
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot     = new Vector2(1f, 0.5f);
            scrollbarRt.anchoredPosition = new Vector2(-1f, 0f);
            scrollbarRt.sizeDelta = new Vector2(4f, -4f);
            scrollbarGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
            var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var handleGo = new GameObject("Handle", typeof(Image));
            handleGo.transform.SetParent(scrollbarGo.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.sprite = Theme.Rounded;
            handleImg.type = Image.Type.Sliced;
            handleImg.pixelsPerUnitMultiplier = 8f; // tiny radius at 4px width
            handleImg.color = new Color(1f, 1f, 1f, 0.14f);
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handleRt;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Hidden measurer TMP — same font settings as the pooled rows; used only for
            // GetPreferredValues so heights are measured without touching a live row.
            var measurerGo = new GameObject("Measurer", typeof(RectTransform));
            measurerGo.transform.SetParent(scrollGo.transform, false);
            var measurer = measurerGo.AddComponent<TextMeshProUGUI>();
            measurer.font = Theme.Font;
            measurer.fontSize = DevConsoleSettings.FontSize;
            measurer.richText = true;
            measurer.textWrappingMode = TextWrappingModes.Normal;
            measurer.raycastTarget = false;
            measurerGo.SetActive(false);

            _logView = new ConsoleLogView(_console, _scroll, _scrollRt, contentRt, measurer, () => Theme.Font);

            BuildJumpPill();
        }

        // Floating "N new" button, bottom-right above the input row. Appears when new lines
        // arrive while the user is scrolled up reading history; click jumps back to live tail.
        void BuildJumpPill()
        {
            _jumpPill = new GameObject("JumpPill", typeof(Image), typeof(Button));
            _jumpPill.transform.SetParent(_window.transform, false);
            var pillRt = (RectTransform)_jumpPill.transform;
            pillRt.anchorMin = new Vector2(1f, 0f);
            pillRt.anchorMax = new Vector2(1f, 0f);
            pillRt.pivot     = new Vector2(1f, 0f);
            pillRt.anchoredPosition = new Vector2(-(PADDING + 10f), InputRowHeight + PADDING + 8f);
            pillRt.sizeDelta = new Vector2(84f, DevConsoleSettings.FontSize + 12f);
            var pillImg = _jumpPill.GetComponent<Image>();
            pillImg.sprite = Theme.Rounded;
            pillImg.type = Image.Type.Sliced;
            pillImg.pixelsPerUnitMultiplier = 1.5f;
            pillImg.color = Color.white; // tinted by the ColorBlock
            var pillBtn = _jumpPill.GetComponent<Button>();
            pillBtn.transition = Selectable.Transition.ColorTint;
            pillBtn.colors = new ColorBlock
            {
                normalColor      = Theme.BgHover,
                highlightedColor = Color.Lerp(Theme.BgHover, Theme.Accent, 0.25f),
                pressedColor     = Color.Lerp(Theme.BgHover, Theme.Accent, 0.45f),
                selectedColor    = Theme.BgHover,
                disabledColor    = Theme.BgHover,
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f,
            };
            pillBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            pillBtn.onClick.AddListener(() => _logView?.ScrollToBottom());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_jumpPill.transform, false);
            _jumpPillLabel = labelGo.AddComponent<TextMeshProUGUI>();
            _jumpPillLabel.font = Theme.Font;
            _jumpPillLabel.fontSize = DevConsoleSettings.FontSize - 2;
            _jumpPillLabel.fontStyle = FontStyles.Bold;
            _jumpPillLabel.color = Theme.Accent;
            _jumpPillLabel.alignment = TextAlignmentOptions.Center;
            _jumpPillLabel.raycastTarget = false;
            var labelRt = _jumpPillLabel.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            _jumpPill.SetActive(false);
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
            _inputBg = inputBg; // error flash tints this card

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
            chevron.font = Theme.Font;
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
            _ghostText.font = Theme.Font;
            _ghostText.fontSize = DevConsoleSettings.FontSize;
            _ghostText.color = Color.white;
            _ghostText.textWrappingMode = TextWrappingModes.NoWrap;
            _ghostText.richText = true;
            _ghostText.raycastTarget = false;
            _ghostText.margin = Vector4.zero;
            // Left (= Middle vertically), NOT MidlineLeft (= Geometry). Geometry centres on the
            // rendered glyphs' bounding box, so a string with a descender sits at a different
            // height than one without — two texts showing different content can never line up.
            // Middle centres on the font's ascender/descender, which is identical for any string
            // in this font and size. See BuildInputRow for the matching setting on the typed text.
            _ghostText.alignment = TextAlignmentOptions.Left;
            // Rect is owned by UpdateGhostPosition, which copies it from the input's own text
            // component every frame — the values here only matter before the first update.
            _ghostRt = _ghostText.rectTransform;
            StretchToFill(_ghostRt);
            ghostGo.transform.SetAsFirstSibling(); // draw BEHIND the input's own text

            // Input's own text component (drawn on top of the ghost). TMP_InputField will
            // force this rect to fill the viewport — that's exactly what we want.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(viewportGo.transform, false);
            var inputText = textGo.AddComponent<TextMeshProUGUI>();
            _inputTextComponent = inputText;
            inputText.font = Theme.Font;
            inputText.fontSize = DevConsoleSettings.FontSize;
            inputText.color = Theme.TextPrimary;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            inputText.margin = Vector4.zero;
            // Must match the ghost's alignment — and Middle rather than Geometry for the reason
            // documented there. As a bonus this stops the typed text drifting vertically as you
            // type: Geometry re-centres the line the moment a descender ("p", "g") appears.
            inputText.alignment = TextAlignmentOptions.Left;
            var inputTextRt = inputText.rectTransform;
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = Vector2.zero;
            inputTextRt.offsetMax = Vector2.zero;

            _input.textViewport   = viewportRt;
            _input.textComponent  = inputText;
            _input.fontAsset      = Theme.Font;
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
            runLabel.font = Theme.Font;
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
        /// Filter row (hidden by default, toggled from the toolbar): a search box that narrows
        /// the log to matching lines, plus one chip per registered category to show/hide it.
        /// Chips drive the same enabled flag as the <c>log_filter</c> command.
        /// </summary>
        void BuildFilterRow()
        {
            _filterRow = new GameObject("FilterRow", typeof(Image));
            _filterRow.transform.SetParent(_window.transform, false);
            var rowRt = (RectTransform)_filterRow.transform;
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot     = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(PADDING, -(TitleBarHeight + 2f));
            rowRt.sizeDelta = new Vector2(-PADDING * 2f, FilterRowHeight);
            var rowImg = _filterRow.GetComponent<Image>();
            rowImg.sprite = Theme.Rounded;
            rowImg.type = Image.Type.Sliced;
            rowImg.pixelsPerUnitMultiplier = 1.5f;
            rowImg.color = Theme.BgElevated;

            // Search box — a darker inset card, pinned to the row's first line so it keeps its
            // height when the chips wrap the row taller.
            var searchGo = new GameObject("Search", typeof(Image));
            searchGo.transform.SetParent(_filterRow.transform, false);
            var searchRt = (RectTransform)searchGo.transform;
            searchRt.anchorMin = new Vector2(0f, 1f);
            searchRt.anchorMax = new Vector2(0f, 1f);
            searchRt.pivot     = new Vector2(0f, 1f);
            searchRt.anchoredPosition = new Vector2(5f, -CHIP_GAP);
            searchRt.sizeDelta = new Vector2(SEARCH_WIDTH, ChipHeight);
            var searchBg = searchGo.GetComponent<Image>();
            searchBg.sprite = Theme.Rounded;
            searchBg.type = Image.Type.Sliced;
            searchBg.pixelsPerUnitMultiplier = 1.5f;
            searchBg.color = Theme.BgWindow;

            var searchViewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            searchViewportGo.transform.SetParent(searchGo.transform, false);
            var searchViewportRt = (RectTransform)searchViewportGo.transform;
            searchViewportRt.anchorMin = Vector2.zero;
            searchViewportRt.anchorMax = Vector2.one;
            searchViewportRt.offsetMin = new Vector2(8f, 1f);
            searchViewportRt.offsetMax = new Vector2(-8f, -1f);

            var searchTextGo = new GameObject("Text", typeof(RectTransform));
            searchTextGo.transform.SetParent(searchViewportGo.transform, false);
            var searchText = searchTextGo.AddComponent<TextMeshProUGUI>();
            searchText.font = Theme.Font;
            searchText.fontSize = DevConsoleSettings.FontSize - 2;
            searchText.color = Theme.TextPrimary;
            searchText.textWrappingMode = TextWrappingModes.NoWrap;
            searchText.alignment = TextAlignmentOptions.Left;
            StretchToFill(searchText.rectTransform);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(searchViewportGo.transform, false);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.font = Theme.Font;
            placeholder.fontSize = DevConsoleSettings.FontSize - 2;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = Theme.TextSubtle;
            placeholder.text = "search";
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.raycastTarget = false;
            StretchToFill(placeholder.rectTransform);

            _searchInput = searchGo.AddComponent<TMP_InputField>();
            _searchInput.textViewport  = searchViewportRt;
            _searchInput.textComponent = searchText;
            _searchInput.placeholder   = placeholder;
            _searchInput.fontAsset     = Theme.Font;
            _searchInput.pointSize     = DevConsoleSettings.FontSize - 2;
            _searchInput.lineType      = TMP_InputField.LineType.SingleLine;
            _searchInput.richText      = false;
            _searchInput.caretColor    = Theme.Accent;
            _searchInput.customCaretColor = true;
            _searchInput.caretWidth    = 2;
            _searchInput.selectionColor = Theme.AccentSoft;
            _searchInput.navigation = new Navigation { mode = Navigation.Mode.None };
            _searchInput.onValueChanged.AddListener(v => _logView?.SetSearch(v));

            // Chip strip — everything right of the search box. Chips WRAP inside it rather than
            // scrolling: a filter you can't see is a filter you won't use. RectMask2D is only a
            // backstop for the degenerate case of one category name wider than the whole row.
            var stripGo = new GameObject("Chips", typeof(RectTransform), typeof(RectMask2D));
            stripGo.transform.SetParent(_filterRow.transform, false);
            _filterChipStrip = (RectTransform)stripGo.transform;
            _filterChipStrip.anchorMin = new Vector2(0f, 0f);
            _filterChipStrip.anchorMax = new Vector2(1f, 1f);
            _filterChipStrip.offsetMin = new Vector2(SEARCH_WIDTH + 12f, 0f);
            _filterChipStrip.offsetMax = new Vector2(-5f, 0f);

            _filterRow.SetActive(false);
        }

        static void StretchToFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Rebuild the category chip widgets from the live registry. Called when the filter row is
        /// shown, so categories registered since the last look appear (syncing while hidden would
        /// be wasted work). Creating widgets is separate from placing them — a window resize needs
        /// to re-place chips every drag frame, and destroying/recreating GameObjects that often is
        /// exactly the churn to avoid.
        /// </summary>
        void RebuildFilterChips()
        {
            if (_filterChipStrip == null) return;
            for (int i = _filterChipStrip.childCount - 1; i >= 0; i--)
                Destroy(_filterChipStrip.GetChild(i).gameObject);
            _filterChips.Clear();

            foreach (ConsoleLogCategory category in DevConsole.Registry.AllCategories())
            {
                ConsoleLogCategory cat = category; // capture per-iteration for the closures below

                var chipGo = new GameObject($"Chip_{cat.name}", typeof(Image), typeof(Button));
                chipGo.transform.SetParent(_filterChipStrip, false);

                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(chipGo.transform, false);
                label.font = Theme.Font;
                label.fontSize = Mathf.Max(9f, DevConsoleSettings.FontSize - 3);
                label.text = cat.name;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                StretchToFill(label.rectTransform);

                // Chips stack from the strip's top-left; LayoutFilterChips assigns the positions.
                var chipRt = (RectTransform)chipGo.transform;
                chipRt.anchorMin = new Vector2(0f, 1f);
                chipRt.anchorMax = new Vector2(0f, 1f);
                chipRt.pivot     = new Vector2(0f, 1f);

                var chipImg = chipGo.GetComponent<Image>();
                chipImg.sprite = Theme.Rounded;
                chipImg.type = Image.Type.Sliced;
                chipImg.pixelsPerUnitMultiplier = 1.5f;

                void Style()
                {
                    Color c = cat.color;
                    chipImg.color = cat.enabled ? new Color(c.r, c.g, c.b, 0.16f)
                                                : new Color(1f, 1f, 1f, 0.04f);
                    label.color = cat.enabled ? cat.color : Theme.TextSubtle;
                }
                Style();

                var btn = chipGo.GetComponent<Button>();
                btn.transition = Selectable.Transition.None; // enabled/disabled style IS the state
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() =>
                {
                    // Same flag log_filter drives: hides existing lines AND drops future appends.
                    cat.enabled = !cat.enabled;
                    Style();
                    _logView?.NotifyFilterChanged();
                });

                _filterChips.Add((chipRt, label.GetPreferredValues(cat.name).x + 18f));
            }

            LayoutFilterChips();
        }

        /// <summary>
        /// Place the chips, wrapping onto new lines when they run out of width, and grow the
        /// filter row to fit however many lines that took. Cheap enough to re-run while dragging
        /// a resize handle.
        /// </summary>
        void LayoutFilterChips()
        {
            if (_filterChipStrip == null || _filterRow == null) return;

            float available = ChipStripWidth;
            float x = 0f;
            float y = -CHIP_GAP;
            int lines = 1;

            foreach ((RectTransform rt, float width) chip in _filterChips)
            {
                // Never wrap a chip that is alone on its line — it would loop forever on a window
                // narrower than a single category name. RectMask2D clips that case instead.
                if (x > 0f && x + chip.width > available)
                {
                    x = 0f;
                    y -= ChipHeight + CHIP_GAP;
                    lines++;
                }
                chip.rt.anchoredPosition = new Vector2(x, y);
                chip.rt.sizeDelta = new Vector2(chip.width, ChipHeight);
                x += chip.width + CHIP_GAP;
            }

            _filterChipLines = lines;
            _lastChipLayoutWidth = _windowSize.x;
            ((RectTransform)_filterRow.transform).sizeDelta = new Vector2(-PADDING * 2f, FilterRowHeight);
            ApplyLogScrollOffsets();
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
                rowClick.HoverChanged = RefreshSuggestionRows;
                _suggestionRowHandlers[i] = rowClick;

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
                text.font = Theme.Font;
                text.fontSize = DevConsoleSettings.FontSize;
                text.color = Theme.TextPrimary;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.richText = true;
                text.alignment = TextAlignmentOptions.Left;
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
