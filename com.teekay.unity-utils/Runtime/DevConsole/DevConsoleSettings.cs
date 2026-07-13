using UnityEngine;
using UnityEngine.InputSystem;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Static configuration for the developer console. Host projects can mutate these BEFORE
    /// calling <see cref="DevConsole.Initialize"/> to customize behavior. All defaults are
    /// chosen for a typical FPS / action game.
    /// </summary>
    public static class DevConsoleSettings
    {
        // — Access —
        /// <summary>When the console may be OPENED. Resolved against the current build type by
        /// <see cref="ConsoleEnabled"/>. Default <see cref="ConsoleAccess.DevBuildsOnly"/> — off in
        /// release builds so the cheat surface never ships enabled.</summary>
        public static ConsoleAccess Access = ConsoleAccess.DevBuildsOnly;

        /// <summary>
        /// Whether the console is allowed to open in the CURRENT build, resolving <see cref="Access"/>
        /// against the runtime build type. This is the single gate every open path checks — the
        /// toggle key, the mobile gesture, bound keys, and <c>Open()</c>/<c>Toggle()</c>. When false,
        /// logging and CVar registration still work; only the openable UI is suppressed.
        /// </summary>
        public static bool ConsoleEnabled
        {
            get
            {
                switch (Access)
                {
                    case ConsoleAccess.Always:        return true;
                    // Debug.isDebugBuild is true in the editor and in any "Development Build" player.
                    case ConsoleAccess.DevBuildsOnly: return Application.isEditor || Debug.isDebugBuild;
                    case ConsoleAccess.EditorOnly:    return Application.isEditor;
                    default:                          return false; // Disabled
                }
            }
        }

        // — Mobile / touch open gesture (tap a screen corner N times) —
        /// <summary>Corner to tap to open the console on touch devices; None disables the gesture.</summary>
        public static ScreenCorner MobileTapCorner = ScreenCorner.TopLeft;
        /// <summary>Number of taps inside the corner that toggle the console.</summary>
        public static int MobileTapCount = 5;
        /// <summary>Max seconds allowed between taps before the streak resets.</summary>
        public static float MobileTapTimeout = 1.5f;
        /// <summary>Square corner hot-zone size, as a fraction of the shorter screen side.</summary>
        public static float MobileTapCornerSize = 0.2f;

        // — Toggle —
        /// <summary>Key that opens/closes the console. Default F12 — function keys don't emit
        /// characters, so they never accidentally land in the input field.</summary>
        public static Key ToggleKey = Key.F12;

        // — Game control while console has FOCUS —
        // Pause/cursor changes only apply when the console's input field is focused. While
        // the console is open but unfocused (mouse clicked back into the scene), the game
        // runs normally — useful for watching live logs during gameplay.
        /// <summary>If true, sets Time.timeScale = 0 while the console input field is focused.</summary>
        public static bool PauseOnFocus = true;
        /// <summary>If true, unlocks Cursor.lockState while focused and restores on focus loss.</summary>
        public static bool UnlockCursorOnFocus = true;

        // — UI sizing —
        /// <summary>Vertical fraction of the screen the console panel occupies (0..1).</summary>
        public static float PanelHeightRatio = 0.5f;
        /// <summary>Font size for log + input.</summary>
        public static int FontSize = 14;

        // — Buffers —
        /// <summary>Max log entries kept in the ring buffer (oldest dropped when full).</summary>
        public static int MaxLogEntries = 500;
        /// <summary>Max history entries (up/down recall).</summary>
        public static int MaxHistoryEntries = 50;

        // — Default category colors —
        public static Color DefaultLogColor = Color.white;
        public static Color WarningLogColor = new(1f, 0.85f, 0.3f);
        public static Color ErrorLogColor   = new(1f, 0.4f, 0.4f);
        public static Color CommandEchoColor = new(0.6f, 0.85f, 1f);
        public static Color HintColor = new(1f, 1f, 1f, 0.4f);

        // — Built-in category names (use these constants when logging via DevConsole.Log) —
        public const string CATEGORY_DEFAULT = "Default";
        public const string CATEGORY_WARNING = "Warning";
        public const string CATEGORY_ERROR = "Error";
        public const string CATEGORY_COMMAND = "Command";

        /// <summary>
        /// Copy values from a <see cref="DevConsoleConfig"/> ScriptableObject into the static
        /// settings. Called automatically when the console finds a config asset under
        /// <c>Resources/DevConsoleConfig</c>, or manually via <c>DevConsole.Configure(...)</c>.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        public static void ApplyFrom(DevConsoleConfig config)
        {
            if (config == null) return;

            Access               = config.access;
            MobileTapCorner      = config.mobileTapCorner;
            MobileTapCount       = config.mobileTapCount;
            MobileTapTimeout     = config.mobileTapTimeout;
            MobileTapCornerSize  = config.mobileTapCornerSize;
            ToggleKey            = config.toggleKey;
            PauseOnFocus         = config.pauseOnFocus;
            UnlockCursorOnFocus  = config.unlockCursorOnFocus;
            PanelHeightRatio     = config.panelHeightRatio;
            FontSize             = config.fontSize;
            MaxLogEntries        = config.maxLogEntries;
            MaxHistoryEntries    = config.maxHistoryEntries;
            DefaultLogColor      = config.defaultLogColor;
            WarningLogColor      = config.warningLogColor;
            ErrorLogColor        = config.errorLogColor;
            CommandEchoColor     = config.commandEchoColor;
            HintColor            = config.hintColor;
        }
    }
}
