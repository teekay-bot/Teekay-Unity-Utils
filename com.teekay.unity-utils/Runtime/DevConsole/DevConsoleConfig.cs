using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Configuration asset for the DevConsole — moves what used to be hard-coded
    /// <see cref="DevConsoleSettings"/> defaults plus per-project log categories out of code and
    /// into an inspector-editable ScriptableObject.
    ///
    /// Two ways to apply it at runtime:
    ///   1. Place the asset at <c>Assets/Resources/Configs/DevConsoleConfig.asset</c> — the console
    ///      will auto-load and apply it on first access. Zero scene setup required.
    ///   2. Call <c>DevConsole.Configure(myConfig)</c> from your own bootstrap code if you
    ///      prefer to avoid the Resources folder.
    ///
    /// The <see cref="bridges"/> list drives the Bridges editor tab — each entry corresponds to
    /// a hand-editable MonoBehaviour scaffold under <c>Assets/Scripts/DevConsoleGenerated/</c>.
    /// The scaffold is generated ONCE; after that the file belongs to the user.
    /// </summary>
    [CreateAssetMenu(fileName = "DevConsoleConfig", menuName = "DevConsole/Config", order = 0)]
    public sealed class DevConsoleConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────
        //  Access — WHEN the console may be opened
        // ─────────────────────────────────────────────────────────────

        [Header("Access")]
        [Tooltip("Controls when the console can be OPENED. It is a cheat surface (free command/CVar " +
                 "access), so production builds should leave it locked. DevBuildsOnly (default) " +
                 "auto-disables in release via Debug.isDebugBuild — you can't forget to turn it off. " +
                 "Logging and CVar registration still work when the console can't open; only the " +
                 "openable UI, toggle key, mobile gesture, and bound keys are gated.")]
        public ConsoleAccess access = ConsoleAccess.DevBuildsOnly;

        // ─────────────────────────────────────────────────────────────
        //  Toggle / focus behavior
        // ─────────────────────────────────────────────────────────────

        [Tooltip("Key that opens/closes the console.")]
        [KeyPicker]
        public Key toggleKey = Key.F12;

        [Tooltip("If true, sets Time.timeScale = 0 while the console input field is focused.")]
        public bool pauseOnFocus = true;
        [Tooltip("If true, unlocks Cursor.lockState while focused and restores on focus loss.")]
        public bool unlockCursorOnFocus = true;

        // ─────────────────────────────────────────────────────────────
        //  Mobile / touch — open by tapping a screen corner repeatedly
        // ─────────────────────────────────────────────────────────────

        [Header("Mobile — open by tapping a screen corner repeatedly")]
        [Tooltip("Which screen corner to tap to open the console on touch devices (no keyboard). " +
                 "None disables the touch gesture. The hot-zone is invisible, so players won't find it by accident.")]
        public ScreenCorner mobileTapCorner = ScreenCorner.TopLeft;
        [Tooltip("How many taps inside the corner toggle the console.")]
        [Range(2, 10)]
        public int mobileTapCount = 5;
        [Tooltip("Max seconds allowed between taps. The streak resets if you pause longer than this.")]
        [Range(0.2f, 5f)]
        public float mobileTapTimeout = 1.5f;
        [Tooltip("Size of the square hot-zone in the corner, as a fraction of the shorter screen side.")]
        [Range(0.05f, 0.4f)]
        public float mobileTapCornerSize = 0.2f;

        // ─────────────────────────────────────────────────────────────
        //  UI sizing & font
        // ─────────────────────────────────────────────────────────────

        [Range(0.1f, 1.0f)]
        [Tooltip("Legacy default panel height as a fraction of the screen (used only before window resize is touched).")]
        public float panelHeightRatio = 0.5f;
        [Tooltip("Font size for log + input text.")]
        public int fontSize = 14;

        // ─────────────────────────────────────────────────────────────
        //  Buffers
        // ─────────────────────────────────────────────────────────────

        [Tooltip("Max log entries kept in the ring buffer (oldest dropped when full).")]
        public int maxLogEntries = 500;
        [Tooltip("Max history entries (up/down arrow recall).")]
        public int maxHistoryEntries = 50;

        // ─────────────────────────────────────────────────────────────
        //  Default category colors (built-in categories only)
        // ─────────────────────────────────────────────────────────────

        [Tooltip("Color for ordinary DevConsole.Log output with no category color.")]
        public Color defaultLogColor = Color.white;
        [Tooltip("Color for DevConsole.LogWarning output.")]
        public Color warningLogColor = new(1f, 0.85f, 0.3f);
        [Tooltip("Color for DevConsole.LogError output.")]
        public Color errorLogColor   = new(1f, 0.4f, 0.4f);
        [Tooltip("Color of the echoed command line after you run a command.")]
        public Color commandEchoColor = new(0.6f, 0.85f, 1f);
        [Tooltip("Color of dimmed hint text (e.g. autocomplete suggestions).")]
        public Color hintColor = new(1f, 1f, 1f, 0.4f);

        // ─────────────────────────────────────────────────────────────
        //  Project-specific categories (auto-registered on init)
        // ─────────────────────────────────────────────────────────────

        [Header("Log Categories — auto-registered on init")]
        [Tooltip("Each entry becomes a DevConsole log category at startup. Use these names as the first arg to DevConsole.Log.")]
        public List<CategoryEntry> categories = new();

        // ─────────────────────────────────────────────────────────────
        //  Bridge scaffolds (one .cs file per entry)
        // ─────────────────────────────────────────────────────────────

        [Header("Bridges — hand-editable MonoBehaviour scaffolds under Assets/Scripts/DevConsoleGenerated/")]
        [Tooltip("Each entry corresponds to one .cs file. Click Generate to create a starter file; after that the file is fully user-owned (the generator refuses to overwrite it).")]
        public List<BridgeDefinition> bridges = new();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Enums — console access policy + mobile gesture vocabulary
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the developer console is allowed to OPEN. The console grants free command/CVar access,
    /// so it's a cheat surface that should not ship enabled. <see cref="DevConsoleSettings.ConsoleEnabled"/>
    /// resolves this against the current build type.
    /// </summary>
    public enum ConsoleAccess
    {
        /// <summary>Never openable — in any build, including the editor.</summary>
        Disabled,
        /// <summary>Openable only inside the Unity Editor.</summary>
        EditorOnly,
        /// <summary>Openable in the editor and in development builds (Debug.isDebugBuild); OFF in release. Default.</summary>
        DevBuildsOnly,
        /// <summary>Always openable, including release builds. Use only if the console is an intended player feature.</summary>
        Always,
    }

    /// <summary>A screen corner, or <see cref="None"/> to disable the mobile tap-to-open gesture.</summary>
    public enum ScreenCorner
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    // ─────────────────────────────────────────────────────────────────
    //  Entry types — serialized via the lists above
    // ─────────────────────────────────────────────────────────────────

    [Serializable]
    public class CategoryEntry
    {
        [Tooltip("Category name — use this string as the first arg to DevConsole.Log(...)")]
        public string name;
        public Color color = Color.white;
    }

    /// <summary>
    /// One entry → one bridge .cs file at <c>Assets/Scripts/DevConsoleGenerated/{className}.cs</c>.
    /// The class is generated once as a minimal MonoBehaviour scaffold; subsequent edits are
    /// the user's responsibility — the generator never overwrites an existing file.
    /// </summary>
    [Serializable]
    public class BridgeDefinition
    {
        [Tooltip("Bridge class name. Must end with 'Bridge'. The file lives at Assets/Scripts/DevConsoleGenerated/{ClassName}.cs.")]
        public string className = "PlayerBridge";
    }
}
