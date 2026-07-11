using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TeekayUtils.DevConsole.UI;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Developer console — runtime command interpreter, CVar manager, and log overlay.
    /// PersistentSingleton: auto-creates on first access, survives scene loads.
    ///
    /// Usage:
    ///   DevConsole.Initialize();                                  // optional; happens on first API call
    ///   DevConsole.RegisterCommand("teleport", "...", args => ...);
    ///   DevConsole.RegisterFloat("player.speed", "...", () => speed, v => speed = v);
    ///   DevConsole.Log("Interaction", "Focused: Door1");
    ///
    /// Press the toggle key (F12) at runtime to open the console on desktop, or tap the configured
    /// screen corner repeatedly on touch devices. Whether it can open at all is gated by
    /// <see cref="DevConsoleSettings.ConsoleEnabled"/> (off in release builds by default).
    /// </summary>
    public sealed class DevConsole : PersistentSingleton<DevConsole>
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Internal state
        // ─────────────────────────────────────────────────────────────────────

        ConsoleRegistry _registry;
        ConsoleHistory _history;
        Queue<ConsoleLogEntry> _logBuffer;
        ConsoleUI _ui;
        ConsoleBindings _bindings;
        // All CVars we've registered. Tracked separately from the registry so we can
        // RestoreSnapshot() them on shutdown — keeps CVar edits play-session-only,
        // never persisting back to source ScriptableObjects.
        List<ConsoleCVar> _allCVars;
        bool _snapshotsRestored;

        // Once set, no static API will resurrect the singleton. Prevents the "DevConsole
        // Auto-Generated GameObject created during teardown" warning when scene components
        // like PlayerConsoleBindings call DevConsole.Unregister from their own OnDisable
        // AFTER our DontDestroyOnLoad object has already been destroyed.
        static bool s_shuttingDown;

        bool _isOpen;
        bool _isFocused;
        CursorLockMode _cachedLockMode;
        bool _wasCursorVisible;
        float _cachedTimeScale;

        // ─────────────────────────────────────────────────────────────────────
        //  Public read-only state
        // ─────────────────────────────────────────────────────────────────────

        public static ConsoleRegistry Registry => Instance._registry;
        public static ConsoleHistory History => Instance._history;
        public static bool IsOpen => HasInstance && instance._isOpen;
        /// <summary>True only while the input field is the active EventSystem selection.
        /// This is the signal external systems should gate on — when false, the console is
        /// either closed OR open-but-unfocused (mouse is in the game). Pause and input-block
        /// follow this state, not <see cref="IsOpen"/>.</summary>
        public static bool IsFocused => HasInstance && instance._isFocused;

        /// <summary>Fires whenever a new log entry is appended (UI subscribes to repaint).</summary>
        public event Action<ConsoleLogEntry> OnLogAppended;
        /// <summary>Fires when the log buffer is cleared.</summary>
        public event Action OnLogCleared;
        /// <summary>
        /// Fires whenever the console opens or closes (true = opened). Use for things tied
        /// to "is the panel visible" (e.g., show/hide other HUD elements). For input blocking
        /// or pause-on-focus, subscribe to <see cref="OnFocusChanged"/> instead.
        /// </summary>
        public static event Action<bool> OnVisibilityChanged;

        /// <summary>
        /// Fires whenever the console's input field gains or loses focus (true = focused).
        /// This is the signal gameplay systems should use to gate input — when the player
        /// clicks back into the game with the console still open, this fires false and the
        /// game resumes. Console can stay open as a live log monitor without pausing.
        /// </summary>
        public static event Action<bool> OnFocusChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Explicit init. Not required — any other static API will trigger lazy creation.</summary>
        public static void Initialize()
        {
            if (s_shuttingDown) return;
            _ = Instance;
        }

        /// <summary>
        /// Apply a <see cref="DevConsoleConfig"/> at runtime. Use this from a bootstrap script
        /// if you'd rather not put the asset in a Resources folder. Calling this BEFORE first
        /// access will set up the console with these values; calling AFTER first access applies
        /// the categories and bindings but leaves the existing settings/runtime state.
        /// </summary>
        public static void Configure(DevConsoleConfig config)
        {
            if (s_shuttingDown || config == null) return;
            DevConsoleSettings.ApplyFrom(config);
            Instance.ApplyConfigContent(config);
        }

        /// <summary>
        /// Register categories from the config. Called from Awake when auto-loading from
        /// Resources, or from <see cref="Configure"/>.
        /// </summary>
        void ApplyConfigContent(DevConsoleConfig config)
        {
            if (config == null) return;

            // Categories — register each one once. RegisterCategory is idempotent (replaces).
            if (config.categories != null)
            {
                foreach (var cat in config.categories)
                {
                    if (cat == null || string.IsNullOrWhiteSpace(cat.name)) continue;
                    _registry.RegisterCategory(new ConsoleLogCategory(cat.name, cat.color));
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (instance != this) return; // duplicate destroyed by base.InitializeSingleton

            // Auto-load a project-level config from Resources before anything reads settings.
            // This is the zero-scene-setup path: drop DevConsoleConfig.asset into a Resources
            // folder and its values apply automatically. Users who don't want a Resources
            // folder can call DevConsole.Configure(config) themselves from a bootstrap script.
            var autoConfig = Resources.Load<DevConsoleConfig>("Configs/DevConsoleConfig");
            if (autoConfig != null) DevConsoleSettings.ApplyFrom(autoConfig);

            _registry  = new ConsoleRegistry();
            _history   = new ConsoleHistory(DevConsoleSettings.MaxHistoryEntries);
            _logBuffer = new Queue<ConsoleLogEntry>(DevConsoleSettings.MaxLogEntries);
            _allCVars  = new List<ConsoleCVar>();
            _bindings  = new ConsoleBindings();
            _bindings.Load();

            // Apply config-driven categories (after _registry exists, before user code starts
            // registering its own).
            if (autoConfig != null) ApplyConfigContent(autoConfig);

            RegisterDefaultCategories();
            RegisterBuiltInCommands();

            // Capture Unity's Debug.Log/Warning/Error and route into the console buffer.
            Application.logMessageReceived += OnUnityLogReceived;

            // Build the UI only when the console may open in this build. When gated off (e.g. a
            // release build with Access = DevBuildsOnly) we still register commands/categories and
            // capture logs — bridges keep working — but never spend the canvas build cost or leave
            // an openable window around. Every _ui access below is null-conditional, so a null _ui
            // is safe; the open paths are gated by ConsoleEnabled anyway.
            if (DevConsoleSettings.ConsoleEnabled)
            {
                // Build UI on the same GameObject (component reuse).
                _ui = gameObject.GetComponent<ConsoleUI>();
                if (_ui == null) _ui = gameObject.AddComponent<ConsoleUI>();
                _ui.Bind(this);
                _ui.SetOpen(false);
            }
        }

        // Overrides the package Singleton base (which clears the static instance ref);
        // the upstream Teekay-Core base had no OnDestroy, so this was a plain method there.
        protected override void OnDestroy()
        {
            if (instance == this)
            {
                s_shuttingDown = true;
                Application.logMessageReceived -= OnUnityLogReceived;
                RestoreAllSnapshots();
            }
            base.OnDestroy();
        }

        // Editor-friendly backstop. OnDestroy doesn't always fire reliably on Play-mode exit
        // (and definitely doesn't fire BEFORE Unity's "save dirty assets on play stop" sweep
        // depending on context), so we also restore in OnApplicationQuit which fires earlier.
        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            s_shuttingDown = true;
            RestoreAllSnapshots();
        }

        /// <summary>
        /// Reset all registered CVars to the value they had at registration time. This is what
        /// keeps console edits play-session-only — when the editor stops Play mode, any CVar
        /// bound to a ScriptableObject field gets restored before Unity tries to save the asset.
        /// Idempotent — safe to call multiple times in one shutdown sequence.
        /// </summary>
        void RestoreAllSnapshots()
        {
            if (_snapshotsRestored || _allCVars == null) return;
            _snapshotsRestored = true;
            for (int i = 0; i < _allCVars.Count; i++)
            {
                // Skip persistent CVars — they OPT IN to surviving across sessions.
                if (_allCVars[i].IsPersistent) continue;
                _allCVars[i].RestoreSnapshot();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Static API — registration
        // ─────────────────────────────────────────────────────────────────────

        public static void RegisterCommand(string name, string description, Action<ConsoleArgs> handler)
        { if (s_shuttingDown) return; Instance._registry.Register(new ConsoleCommand(name, description, handler)); }

        /// <summary>
        /// RegisterCommand overload with autocomplete hints for each argument position.
        /// Pass <c>new[] { new[] { "true", "false" } }</c> for a bool-style toggle so the console's
        /// ghost-hint + suggestion dropdown can complete the value after the space.
        /// </summary>
        public static void RegisterCommand(string name, string description, Action<ConsoleArgs> handler,
            System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> argCompletions)
        { if (s_shuttingDown) return; Instance._registry.Register(new ConsoleCommand(name, description, handler, argCompletions)); }

        public static void RegisterFloat(string name, string description, Func<float> get, Action<float> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new FloatCVar(name, description, get, set)); }

        public static void RegisterInt(string name, string description, Func<int> get, Action<int> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new IntCVar(name, description, get, set)); }

        public static void RegisterBool(string name, string description, Func<bool> get, Action<bool> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new BoolCVar(name, description, get, set)); }

        public static void RegisterString(string name, string description, Func<string> get, Action<string> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new StringCVar(name, description, get, set)); }

        // ─── Persistent variants ───
        // Same registration, but the value is NOT restored on shutdown — console edits stick
        // across play sessions. Use for things like audio volume, keybindings — settings that
        // SHOULD survive between plays. Default (non-Persistent) is play-session-only.
        public static void RegisterFloatPersistent(string name, string description, Func<float> get, Action<float> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new FloatCVar(name, description, get, set) { IsPersistent = true }); }

        public static void RegisterIntPersistent(string name, string description, Func<int> get, Action<int> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new IntCVar(name, description, get, set) { IsPersistent = true }); }

        public static void RegisterBoolPersistent(string name, string description, Func<bool> get, Action<bool> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new BoolCVar(name, description, get, set) { IsPersistent = true }); }

        public static void RegisterStringPersistent(string name, string description, Func<string> get, Action<string> set)
        { if (s_shuttingDown) return; Instance.RegisterCVarInternal(new StringCVar(name, description, get, set) { IsPersistent = true }); }

        /// <summary>
        /// Snapshot the value at registration time (so we can restore on shutdown) and add to
        /// the registry. The snapshot is what makes console edits play-session-only — see
        /// RestoreAllSnapshots.
        /// </summary>
        void RegisterCVarInternal(ConsoleCVar cvar)
        {
            cvar.Snapshot();
            _allCVars.Add(cvar);
            _registry.Register(cvar);
        }

        public static void Unregister(string name)
        {
            // CRITICAL: do NOT resurrect the singleton during teardown. Scene components like
            // PlayerConsoleBindings call Unregister from OnDisable, which may run AFTER the
            // DontDestroyOnLoad DevConsole GameObject has already been destroyed. Touching
            // Instance there would auto-create a fresh GameObject and trigger Unity's
            // "objects not cleaned up" warning.
            if (s_shuttingDown || !HasInstance) return;
            var inst = instance;
            if (inst._registry.TryGetCVar(name, out var cvar))
                inst._allCVars.Remove(cvar);
            inst._registry.Unregister(name);
        }

        public static void RegisterCategory(string name, Color color)
        { if (s_shuttingDown) return; Instance._registry.RegisterCategory(new ConsoleLogCategory(name, color)); }

        // ─────────────────────────────────────────────────────────────────────
        //  Static API — logging
        // ─────────────────────────────────────────────────────────────────────

        public static void Log(string category, string message)
        {
            if (s_shuttingDown || !HasInstance) return;
            var inst = instance;
            if (!inst._registry.TryGetCategory(category, out var cat))
                cat = new ConsoleLogCategory(category, DevConsoleSettings.DefaultLogColor);
            if (!cat.enabled) return;
            inst.AppendLog(new ConsoleLogEntry(category, message, cat.color, Time.realtimeSinceStartup));
        }

        public static void Log(string message)
            => Log(DevConsoleSettings.CATEGORY_DEFAULT, message);

        void AppendLog(ConsoleLogEntry entry)
        {
            if (_logBuffer.Count >= DevConsoleSettings.MaxLogEntries) _logBuffer.Dequeue();
            _logBuffer.Enqueue(entry);
            OnLogAppended?.Invoke(entry);
        }

        public IEnumerable<ConsoleLogEntry> EnumerateLog() => _logBuffer;

        public void ClearLog()
        {
            _logBuffer.Clear();
            OnLogCleared?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Static API — control
        // ─────────────────────────────────────────────────────────────────────

        // Open/Toggle are gated by ConsoleEnabled so nothing — gameplay code, a bound key, the
        // mobile gesture — can surface the console when it's disabled for this build. Close is NOT
        // gated: closing must always work (defensive, e.g. if the policy somehow changed mid-session).
        public static void Toggle()
        { if (s_shuttingDown || !HasInstance || !DevConsoleSettings.ConsoleEnabled) return; instance.SetOpen(!instance._isOpen); }
        public static void Open()
        { if (s_shuttingDown || !HasInstance || !DevConsoleSettings.ConsoleEnabled) return; instance.SetOpen(true); }
        public static void Close()
        { if (s_shuttingDown || !HasInstance) return; instance.SetOpen(false); }

        void SetOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;
            _ui?.SetOpen(open);
            // Closing always clears focus, so any pause / cursor / input-block side-effects
            // unwind correctly. Opening doesn't auto-focus here — the UI focuses the input
            // field on its own, and the focus-poll then triggers NotifyFocusChanged(true).
            if (!open && _isFocused) NotifyFocusChanged(false);
            OnVisibilityChanged?.Invoke(open);
        }

        /// <summary>
        /// Called by ConsoleUI when the input field gains or loses EventSystem focus.
        /// Pause + cursor changes happen here (not in SetOpen) so the console can be left
        /// open as a live monitor while the user is interacting with the game.
        /// </summary>
        internal void NotifyFocusChanged(bool focused)
        {
            if (_isFocused == focused) return;
            _isFocused = focused;

            if (focused)
            {
                if (DevConsoleSettings.PauseOnFocus)
                {
                    _cachedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                if (DevConsoleSettings.UnlockCursorOnFocus)
                {
                    _cachedLockMode = Cursor.lockState;
                    _wasCursorVisible = Cursor.visible;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                if (DevConsoleSettings.PauseOnFocus)
                    Time.timeScale = _cachedTimeScale;
                if (DevConsoleSettings.UnlockCursorOnFocus)
                {
                    Cursor.lockState = _cachedLockMode;
                    Cursor.visible = _wasCursorVisible;
                }
            }

            OnFocusChanged?.Invoke(focused);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Execution
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Execute a raw command line (as if the user typed it and pressed Enter).</summary>
        public static void Execute(string commandLine)
        {
            if (s_shuttingDown) return;
            if (string.IsNullOrWhiteSpace(commandLine)) return;
            Instance.ExecuteInternal(commandLine);
        }

        void ExecuteInternal(string line)
        {
            AppendLog(new ConsoleLogEntry(
                DevConsoleSettings.CATEGORY_COMMAND, "> " + line,
                DevConsoleSettings.CommandEchoColor, Time.realtimeSinceStartup));

            _history.Add(line);

            string[] tokens = ConsoleParser.Tokenize(line);
            if (tokens.Length == 0) return;

            string name = tokens[0];

            // CVar path: zero extra args = print value; one extra arg = set value.
            if (_registry.TryGetCVar(name, out var cvar))
            {
                if (tokens.Length == 1)
                {
                    Log(DevConsoleSettings.CATEGORY_DEFAULT, $"{cvar.Name} = {cvar.GetValueAsString()}  ({cvar.TypeName})");
                }
                else
                {
                    // Join the rest as a single value (so quoted strings work for StringCVar).
                    string value = tokens.Length == 2 ? tokens[1] : string.Join(" ", tokens, 1, tokens.Length - 1);
                    if (cvar.TrySetFromString(value, out string err))
                        Log(DevConsoleSettings.CATEGORY_DEFAULT, $"{cvar.Name} = {cvar.GetValueAsString()}");
                    else
                        Log(DevConsoleSettings.CATEGORY_ERROR, $"set {cvar.Name}: {err}");
                }
                return;
            }

            if (_registry.TryGetCommand(name, out var cmd))
            {
                var args = new string[tokens.Length - 1];
                Array.Copy(tokens, 1, args, 0, args.Length);
                try { cmd.Handler(new ConsoleArgs(args)); }
                catch (Exception e)
                {
                    Log(DevConsoleSettings.CATEGORY_ERROR, $"{cmd.Name} threw: {e.Message}");
                    Debug.LogException(e);
                }
                return;
            }

            Log(DevConsoleSettings.CATEGORY_ERROR, $"Unknown command: '{name}'. Type 'help' for a list.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Toggle polling — keyboard, mobile gesture, key bindings
        // ─────────────────────────────────────────────────────────────────────

        // Mobile corner-tap streak state. _cornerTapStreak counts consecutive taps landing in the
        // configured corner; it resets when a tap lands elsewhere or the inter-tap timeout elapses.
        int _cornerTapStreak;
        float _lastCornerTapTime;

        void Update()
        {
            // Hard gate: when the console can't open in this build, no key, gesture, or binding may
            // surface it or fire a bound (potentially cheat) command. Checked here AND in
            // Open()/Toggle() so every path is covered.
            if (!DevConsoleSettings.ConsoleEnabled) return;

            PollToggleKey();
            PollMobileCornerTap();
            PollKeyBindings();
        }

        // Desktop open path. Split out of Update so the old top-level `Keyboard.current == null`
        // early-return no longer also kills the touch path (Keyboard.current is null on most phones).
        void PollToggleKey()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Three-state toggle key behavior:
            //   1. Closed              → open + focus
            //   2. Open + focused      → close
            //   3. Open + unfocused    → re-focus (don't close)
            //
            // Case 3 exists because once focus is lost (user clicked into the game), the
            // cursor relocks to gameplay — there's no way to mouse-click back onto the
            // console. The toggle key gives a keyboard-only path to bring focus back.
            // To close from the unfocused state, press it twice (re-focus, then close).
            if (kb[DevConsoleSettings.ToggleKey].wasPressedThisFrame)
            {
                if (!_isOpen)            SetOpen(true);            // opens; ConsoleUI auto-focuses
                else if (_isFocused)     SetOpen(false);           // close
                else                     _ui?.RefocusInput();      // re-focus without closing
            }
        }

        // Mobile open gesture: tap the configured screen corner MobileTapCount times, each within
        // MobileTapTimeout of the last. The hot-zone is invisible, so normal players don't stumble
        // onto it — fitting for a cheat surface. It's a toggle, so the same gesture closes the
        // console. Unscaled time so it still works while the console pauses the game on focus.
        void PollMobileCornerTap()
        {
            ScreenCorner corner = DevConsoleSettings.MobileTapCorner;
            if (corner == ScreenCorner.None) return;

            Touchscreen ts = Touchscreen.current;
            if (ts == null) return;

            int touchCount = ts.touches.Count;
            for (int i = 0; i < touchCount; i++)
            {
                TouchControl touch = ts.touches[i];
                if (!touch.press.wasPressedThisFrame) continue; // only count finger-down events

                if (!IsInsideCorner(touch.position.ReadValue(), corner))
                {
                    _cornerTapStreak = 0; // a tap outside the corner breaks the streak
                    continue;
                }

                float now = Time.unscaledTime;
                if (now - _lastCornerTapTime > DevConsoleSettings.MobileTapTimeout)
                    _cornerTapStreak = 0; // too slow — this tap starts a fresh streak

                _cornerTapStreak++;
                _lastCornerTapTime = now;

                if (_cornerTapStreak >= DevConsoleSettings.MobileTapCount)
                {
                    _cornerTapStreak = 0;
                    SetOpen(!_isOpen);
                }
            }
        }

        // Is a screen-space point (origin bottom-left, pixels) within the square hot-zone of the
        // given corner? Zone side = MobileTapCornerSize × the shorter screen dimension, so it stays
        // a sensible physical size across aspect ratios.
        static bool IsInsideCorner(Vector2 screenPos, ScreenCorner corner)
        {
            float zone   = Mathf.Min(Screen.width, Screen.height) * DevConsoleSettings.MobileTapCornerSize;
            bool  left   = screenPos.x <= zone;
            bool  right  = screenPos.x >= Screen.width  - zone;
            bool  bottom = screenPos.y <= zone;
            bool  top    = screenPos.y >= Screen.height - zone;
            switch (corner)
            {
                case ScreenCorner.TopLeft:     return left  && top;
                case ScreenCorner.TopRight:    return right && top;
                case ScreenCorner.BottomLeft:  return left  && bottom;
                case ScreenCorner.BottomRight: return right && bottom;
                default:                       return false;
            }
        }

        void PollKeyBindings()
        {
            // Fire any bound commands whose keys were just pressed — whenever the console isn't
            // capturing text input (closed, or open but unfocused). Skipped only while focused,
            // otherwise typing a bound key while editing a command would re-trigger the binding.
            // Closing always clears focus, so !_isFocused covers the closed case too.
            // ConsumePressedThisFrame() null-guards Keyboard.current internally.
            if (!_isFocused && _bindings != null)
            {
                string boundCmd = _bindings.ConsumePressedThisFrame();
                if (boundCmd != null) ExecuteInternal(boundCmd);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity log capture
        // ─────────────────────────────────────────────────────────────────────

        void OnUnityLogReceived(string condition, string stackTrace, LogType type)
        {
            string category;
            Color color;
            switch (type)
            {
                case LogType.Warning:
                    category = DevConsoleSettings.CATEGORY_WARNING;
                    color = DevConsoleSettings.WarningLogColor;
                    break;
                case LogType.Error: case LogType.Exception: case LogType.Assert:
                    category = DevConsoleSettings.CATEGORY_ERROR;
                    color = DevConsoleSettings.ErrorLogColor;
                    break;
                default:
                    category = DevConsoleSettings.CATEGORY_DEFAULT;
                    color = DevConsoleSettings.DefaultLogColor;
                    break;
            }

            if (_registry.TryGetCategory(category, out var cat) && !cat.enabled) return;

            AppendLog(new ConsoleLogEntry(category, condition, color, Time.realtimeSinceStartup));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Built-ins
        // ─────────────────────────────────────────────────────────────────────

        void RegisterDefaultCategories()
        {
            _registry.RegisterCategory(new ConsoleLogCategory(DevConsoleSettings.CATEGORY_DEFAULT, DevConsoleSettings.DefaultLogColor));
            _registry.RegisterCategory(new ConsoleLogCategory(DevConsoleSettings.CATEGORY_WARNING, DevConsoleSettings.WarningLogColor));
            _registry.RegisterCategory(new ConsoleLogCategory(DevConsoleSettings.CATEGORY_ERROR,   DevConsoleSettings.ErrorLogColor));
            _registry.RegisterCategory(new ConsoleLogCategory(DevConsoleSettings.CATEGORY_COMMAND, DevConsoleSettings.CommandEchoColor));
        }

        void RegisterBuiltInCommands()
        {
            _registry.Register(new ConsoleCommand("help", "List commands and CVars, or describe one by name. Usage: help [name]", HelpCommand));
            _registry.Register(new ConsoleCommand("clear", "Clear the console log.", _ => ClearLog()));
            _registry.Register(new ConsoleCommand("quit", "Exit the application.", _ => Application.Quit()));
            _registry.Register(new ConsoleCommand("log_filter",
                "List categories with their enabled state, or toggle one. Usage: log_filter [<category> [on|off|toggle]]",
                LogFilterCommand));
            _registry.Register(new ConsoleCommand("bind",
                "Bind a key to a command. Usage: bind <KeyName> \"<command line>\"  (e.g. bind F3 \"player.teleport 0 5 0\")",
                BindCommand));
            _registry.Register(new ConsoleCommand("unbind",
                "Remove a key binding. Usage: unbind <KeyName>",
                UnbindCommand));
            _registry.Register(new ConsoleCommand("binds",
                "List all current key bindings.",
                _ => ListBindings()));
        }

        void BindCommand(ConsoleArgs args)
        {
            if (args.Count < 2)
            { Log(DevConsoleSettings.CATEGORY_ERROR, "bind: usage: bind <KeyName> \"<command>\""); return; }
            if (!ConsoleBindings.TryParseKey(args[0], out Key key))
            { Log(DevConsoleSettings.CATEGORY_ERROR, $"bind: '{args[0]}' is not a valid Key. Examples: F3, Backquote, Semicolon, Numpad7."); return; }

            // Re-join the args after the key into a single command line so the user can write
            // either: bind F3 noclip   OR   bind F3 "player.teleport 0 5 0"
            string command = args.Count == 2 ? args[1] : string.Join(" ", _bindRejoinBuf(args));
            _bindings.Set(key, command);
            Log(DevConsoleSettings.CATEGORY_DEFAULT, $"bind {key} = {command}");
        }

        // Tiny helper to materialize args[1..] as a string[] for string.Join.
        static string[] _bindRejoinBuf(ConsoleArgs args)
        {
            var arr = new string[args.Count - 1];
            for (int i = 0; i < arr.Length; i++) arr[i] = args[i + 1];
            return arr;
        }

        void UnbindCommand(ConsoleArgs args)
        {
            if (args.Count < 1)
            { Log(DevConsoleSettings.CATEGORY_ERROR, "unbind: usage: unbind <KeyName>"); return; }
            if (!ConsoleBindings.TryParseKey(args[0], out Key key))
            { Log(DevConsoleSettings.CATEGORY_ERROR, $"unbind: '{args[0]}' is not a valid Key."); return; }
            if (_bindings.Remove(key)) Log(DevConsoleSettings.CATEGORY_DEFAULT, $"unbind {key}");
            else Log(DevConsoleSettings.CATEGORY_DEFAULT, $"unbind: no binding for {key}");
        }

        void ListBindings()
        {
            var sb = new StringBuilder();
            if (_bindings.All.Count == 0) { Log(DevConsoleSettings.CATEGORY_DEFAULT, "No key bindings."); return; }
            sb.AppendLine("Key bindings:");
            foreach (var pair in _bindings.All) sb.AppendLine($"  {pair.Key,-12} -> {pair.Value}");
            Log(DevConsoleSettings.CATEGORY_DEFAULT, sb.ToString().TrimEnd());
        }

        void LogFilterCommand(ConsoleArgs args)
        {
            // 0 args → list everything with its on/off state.
            if (args.Count == 0)
            {
                var sb = new StringBuilder("Log categories:\n");
                foreach (var cat in _registry.AllCategories())
                    sb.AppendLine($"  {cat.name,-16} {(cat.enabled ? "on" : "off")}");
                Log(DevConsoleSettings.CATEGORY_DEFAULT, sb.ToString().TrimEnd());
                return;
            }

            string name = args[0];
            if (!_registry.TryGetCategory(name, out var category))
            { Log(DevConsoleSettings.CATEGORY_ERROR, $"No log category '{name}'."); return; }

            // 1 arg → toggle. 2 args → explicit on/off/toggle.
            bool newState;
            if (args.Count == 1) newState = !category.enabled;
            else
            {
                switch ((args[1] ?? string.Empty).ToLowerInvariant())
                {
                    case "on":     newState = true;  break;
                    case "off":    newState = false; break;
                    case "toggle": newState = !category.enabled; break;
                    default:
                        Log(DevConsoleSettings.CATEGORY_ERROR, $"log_filter: '{args[1]}' must be on|off|toggle.");
                        return;
                }
            }
            category.enabled = newState;
            Log(DevConsoleSettings.CATEGORY_DEFAULT, $"log_filter: {category.name} = {(newState ? "on" : "off")}");
        }

        // Help output palette — picked to match the rest of the DevConsole UI theme.
        // Inner TMP color tags override the outer category color the log writer wraps around
        // each line. The "general" bucket holds names without a dotted prefix and is rendered
        // last so the alphabetic prefix groups appear first.
        const string HELP_HEADER_COLOR   = "#22D3EE"; // cyan accent
        const string HELP_GROUP_COLOR    = "#7C8390"; // muted blue-gray
        const string HELP_NAME_COLOR     = "#E8EBEF"; // near-white
        const string HELP_TYPE_COLOR     = "#6B7280"; // dim gray (for "(float) = 7")
        const string HELP_DESC_COLOR     = "#9CA3AF"; // light gray
        const string HELP_GENERAL_BUCKET = "general";

        void HelpCommand(ConsoleArgs args)
        {
            if (args.Count == 0)
            {
                var sb = new StringBuilder(1024);

                sb.Append("\n<size=115%><b><color=").Append(HELP_HEADER_COLOR).Append(">COMMANDS</color></b></size>\n\n");
                var cmds = new List<ConsoleCommand>(_registry.AllCommands());
                cmds.Sort(CompareByGroupThenName);
                AppendGroupedHelp(sb, cmds, c => c.Name, c => c.Description,
                    c => BuildCommandUsage(c.ArgCompletions), _ => null);

                sb.Append("\n<size=115%><b><color=").Append(HELP_HEADER_COLOR).Append(">CVARS</color></b></size>\n\n");
                var cvars = new List<ConsoleCVar>(_registry.AllCVars());
                cvars.Sort(CompareByGroupThenNameCVar);
                AppendGroupedHelp(sb, cvars, v => v.Name, v => v.Description,
                    v => $"[{v.UsageHint}]",
                    v => $"= {v.GetValueAsString()}");

                sb.Append("\n<size=85%><color=").Append(HELP_DESC_COLOR)
                  .Append(">Tip: type </color><color=").Append(HELP_NAME_COLOR)
                  .Append("><b>help &lt;name&gt;</b></color><color=").Append(HELP_DESC_COLOR)
                  .Append("> for details on one entry.</color>");

                Log(DevConsoleSettings.CATEGORY_DEFAULT, sb.ToString());
                return;
            }

            string name = args[0];
            if (_registry.TryGetCommand(name, out var cmd))
            { Log(DevConsoleSettings.CATEGORY_DEFAULT,
                FormatHelpEntry(cmd.Name, BuildCommandUsage(cmd.ArgCompletions), null, cmd.Description)); return; }
            if (_registry.TryGetCVar(name, out var cvar))
            { Log(DevConsoleSettings.CATEGORY_DEFAULT,
                FormatHelpEntry(cvar.Name, $"[{cvar.UsageHint}]", $"= {cvar.GetValueAsString()}", cvar.Description)); return; }

            Log(DevConsoleSettings.CATEGORY_ERROR, $"No command or CVar named '{name}'.");
        }

        // Append a list (already sorted by group → name) as bucketed entries:
        //   [bucket]
        //     name   [usage]   (suffix, e.g. current value)
        //       description
        static void AppendGroupedHelp<T>(StringBuilder sb, List<T> items,
            Func<T, string> nameOf, Func<T, string> descOf,
            Func<T, string> usageOf, Func<T, string> suffixOf)
        {
            string lastGroup = null;
            foreach (var item in items)
            {
                string name   = nameOf(item);
                string desc   = descOf(item);
                string usage  = usageOf(item);
                string suffix = suffixOf(item);
                string group  = GroupOf(name);

                if (group != lastGroup)
                {
                    if (lastGroup != null) sb.Append('\n');
                    sb.Append("<color=").Append(HELP_GROUP_COLOR).Append("><b>[")
                      .Append(group).Append("]</b></color>\n");
                    lastGroup = group;
                }

                sb.Append("  <color=").Append(HELP_NAME_COLOR).Append("><b>")
                  .Append(name).Append("</b></color>");
                if (!string.IsNullOrEmpty(usage))
                    sb.Append("  <color=").Append(HELP_TYPE_COLOR).Append('>').Append(usage).Append("</color>");
                if (!string.IsNullOrEmpty(suffix))
                    sb.Append("  <color=").Append(HELP_TYPE_COLOR).Append('>').Append(suffix).Append("</color>");
                sb.Append('\n');

                if (!string.IsNullOrEmpty(desc))
                    sb.Append("      <color=").Append(HELP_DESC_COLOR).Append('>').Append(desc).Append("</color>\n");
            }
        }

        static string FormatHelpEntry(string name, string usage, string suffix, string desc)
        {
            var sb = new StringBuilder(160);
            sb.Append("<color=").Append(HELP_NAME_COLOR).Append("><b>").Append(name).Append("</b></color>");
            if (!string.IsNullOrEmpty(usage))
                sb.Append("  <color=").Append(HELP_TYPE_COLOR).Append('>').Append(usage).Append("</color>");
            if (!string.IsNullOrEmpty(suffix))
                sb.Append("  <color=").Append(HELP_TYPE_COLOR).Append('>').Append(suffix).Append("</color>");
            if (!string.IsNullOrEmpty(desc))
                sb.Append("\n  <color=").Append(HELP_DESC_COLOR).Append('>').Append(desc).Append("</color>");
            return sb.ToString();
        }

        // Render ArgCompletions as a CLI-style signature, e.g. [true/false] [low/med/high].
        // Each arg position becomes one bracketed token; "/" separates alternatives. Returns
        // null when no completions are declared (no usage line shown then).
        static string BuildCommandUsage(System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> argCompletions)
        {
            if (argCompletions == null || argCompletions.Count == 0) return null;
            var sb = new StringBuilder(32);
            for (int i = 0; i < argCompletions.Count; i++)
            {
                var candidates = argCompletions[i];
                if (candidates == null || candidates.Count == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append('[');
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (j > 0) sb.Append('/');
                    sb.Append(candidates[j]);
                }
                sb.Append(']');
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // Group key = chars before the first dot, or "general" if there's no dot.
        static string GroupOf(string name)
        {
            int dot = name.IndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : HELP_GENERAL_BUCKET;
        }

        // Sort: prefix groups alphabetically, "general" bucket last; ties broken by full name.
        static int CompareByGroupThenName(ConsoleCommand a, ConsoleCommand b) =>
            CompareGroupNames(GroupOf(a.Name), a.Name, GroupOf(b.Name), b.Name);

        static int CompareByGroupThenNameCVar(ConsoleCVar a, ConsoleCVar b) =>
            CompareGroupNames(GroupOf(a.Name), a.Name, GroupOf(b.Name), b.Name);

        static int CompareGroupNames(string ga, string na, string gb, string nb)
        {
            bool aGen = ga == HELP_GENERAL_BUCKET;
            bool bGen = gb == HELP_GENERAL_BUCKET;
            if (aGen != bGen) return aGen ? 1 : -1;
            int g = string.CompareOrdinal(ga, gb);
            return g != 0 ? g : string.CompareOrdinal(na, nb);
        }
    }
}
