using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TeekayUtils.DevConsole.EditorTools
{
    /// <summary>
    /// Tools &gt; DevConsole &gt; Config — a tabbed editor window for authoring
    /// <see cref="DevConsoleConfig"/> assets. Four tabs:
    ///
    ///   • Settings    — mirrors <c>DevConsoleSettings</c>; uses the [KeyPicker] dropdown
    ///   • Categories  — author log categories with colors
    ///   • Bridges     — manage bridge scaffold files under Assets/Scripts/DevConsoleGenerated/
    ///
    /// The Bridges tab is "scaffold-once": Generate writes a minimal MonoBehaviour file and
    /// then refuses to touch it. The row's button flips to Open afterwards so the row stays
    /// useful as a jump target into the user-owned file.
    /// </summary>
    public sealed class DevConsoleConfigWindow : EditorWindow
    {
        const string LAST_CONFIG_GUID_PREF = "DevConsole.LastConfigGuid";

        enum Tab { Settings, Categories, Bridges }

        DevConsoleConfig _config;
        SerializedObject _serialized;
        Tab _currentTab;
        Vector2 _settingsScroll;
        Vector2 _categoriesScroll;
        Vector2 _bridgesScroll;

        [MenuItem("Tools/DevConsole/Config")]
        public static void Open()
        {
            var window = GetWindow<DevConsoleConfigWindow>("DevConsole Config");
            window.minSize = new Vector2(420f, 340f);
            window.Show();
        }

        [MenuItem("Tools/DevConsole/Create Config")]
        public static void CreateAndOpen()
        {
            var window = GetWindow<DevConsoleConfigWindow>("DevConsole Config");
            window.minSize = new Vector2(420f, 340f);
            window.CreateNewConfigAsset();
        }

        void OnEnable() => TryLoadLastConfig();
        void OnFocus()  => TryLoadLastConfig();

        // ──────────────────────────────────────────────────────────────────
        //  Asset resolution
        // ──────────────────────────────────────────────────────────────────

        void TryLoadLastConfig()
        {
            // If the user already has a valid config selected, don't override it.
            if (_config != null && _serialized != null && _serialized.targetObject != null) return;

            // Restore the last-used asset from EditorPrefs.
            string guid = EditorPrefs.GetString(LAST_CONFIG_GUID_PREF, "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<DevConsoleConfig>(path);
                    if (asset != null) { SetConfig(asset); return; }
                }
            }

            // Otherwise, find any DevConsoleConfig in the project as a sensible default.
            var found = AssetDatabase.FindAssets("t:DevConsoleConfig");
            if (found.Length > 0)
            {
                var asset = AssetDatabase.LoadAssetAtPath<DevConsoleConfig>(
                    AssetDatabase.GUIDToAssetPath(found[0]));
                if (asset != null) SetConfig(asset);
            }
        }

        void SetConfig(DevConsoleConfig config)
        {
            _config = config;
            _serialized = config != null ? new SerializedObject(config) : null;
            if (config != null)
            {
                string path = AssetDatabase.GetAssetPath(config);
                EditorPrefs.SetString(LAST_CONFIG_GUID_PREF, AssetDatabase.AssetPathToGUID(path));
            }
        }

        // Fixed location so the runtime's Resources.Load<DevConsoleConfig>("Configs/DevConsoleConfig")
        // auto-loads it with zero scene setup. If the user wants the asset elsewhere they can
        // move it manually — but the default flow drops them straight into the zero-config path.
        const string DEFAULT_CONFIG_FOLDER = "Assets/Resources/Configs";
        const string DEFAULT_CONFIG_PATH   = "Assets/Resources/Configs/DevConsoleConfig.asset";

        void CreateNewConfigAsset()
        {
            // If one already exists at the canonical path, just load it instead of overwriting.
            var existing = AssetDatabase.LoadAssetAtPath<DevConsoleConfig>(DEFAULT_CONFIG_PATH);
            if (existing != null)
            {
                SetConfig(existing);
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("DevConsole Config",
                    $"A DevConsoleConfig already exists at:\n{DEFAULT_CONFIG_PATH}\n\nLoaded the existing asset.",
                    "OK");
                return;
            }

            // Ensure Assets/Resources/Configs/ exists (create each level as needed).
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(DEFAULT_CONFIG_FOLDER))
                AssetDatabase.CreateFolder("Assets/Resources", "Configs");

            var asset = CreateInstance<DevConsoleConfig>();
            AssetDatabase.CreateAsset(asset, DEFAULT_CONFIG_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetConfig(asset);
            EditorGUIUtility.PingObject(asset);
        }

        // ──────────────────────────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(4);

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "No DevConsoleConfig asset loaded. Assign one above, or click 'New' to create a fresh asset. " +
                    "Place the asset under a Resources/ folder named 'DevConsoleConfig' for zero-setup runtime auto-loading.",
                    MessageType.Info);
                return;
            }

            _serialized.Update();

            DrawTabBar();
            EditorGUILayout.Space(6);

            switch (_currentTab)
            {
                case Tab.Settings:   DrawSettingsTab(); break;
                case Tab.Categories: DrawCategoriesTab(); break;
                case Tab.Bridges:    DrawBridgesTab(); break;
            }

            _serialized.ApplyModifiedProperties();
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Config Asset:", GUILayout.Width(86));
                var newConfig = (DevConsoleConfig)EditorGUILayout.ObjectField(
                    _config, typeof(DevConsoleConfig), allowSceneObjects: false);
                if (newConfig != _config) SetConfig(newConfig);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(50)) && _config != null)
                    EditorGUIUtility.PingObject(_config);
                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    CreateNewConfigAsset();
            }
        }

        void DrawTabBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab,
                    new[] { "Settings", "Categories", "Bridges" });
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Tab: Settings
        // ──────────────────────────────────────────────────────────────────

        void DrawSettingsTab()
        {
            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);
            EditorGUILayout.Space(2);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160f;

            DrawSettingsCard("Access", "When the console can be OPENED. Production builds should stay locked — DevBuildsOnly auto-disables in release.",
                ("access", "Access"));

            DrawSettingsCard("Toggle", "When the console opens and closes (desktop keyboard).",
                ("toggleKey", "Toggle Key"));

            DrawSettingsCard("Mobile (Touch)", "Open on touch devices by tapping a screen corner repeatedly. Corner = None disables it.",
                ("mobileTapCorner", "Corner"),
                ("mobileTapCount", "Tap Count"),
                ("mobileTapTimeout", "Tap Timeout (s)"),
                ("mobileTapCornerSize", "Corner Size"));

            DrawSettingsCard("Focus Behavior", "What happens while the input field is focused.",
                ("pauseOnFocus", "Pause Game"),
                ("unlockCursorOnFocus", "Unlock Cursor"));

            DrawSettingsCard("Interface", "Panel sizing and text.",
                ("panelHeightRatio", "Panel Height"),
                ("fontSize", "Font Size"),
                ("fontAsset", "Font Asset"));

            DrawSettingsCard("Chrome Theme",
                "Window chrome colors. Hover/selection tints derive from the accent, so a retint " +
                "is usually just Accent plus the two surface colors.",
                ("chromeWindowColor", "Window"),
                ("chromeElevatedColor", "Elevated"),
                ("chromeHoverColor", "Hover"),
                ("chromeTextPrimary", "Text Primary"),
                ("chromeTextMuted", "Text Muted"),
                ("chromeTextSubtle", "Text Subtle"),
                ("accentColor", "Accent"),
                ("errorAccentColor", "Error Accent"));

            DrawSettingsCard("Buffers", "Ring-buffer limits — oldest entries drop when full.",
                ("maxLogEntries", "Max Log Entries"),
                ("maxHistoryEntries", "Max History Entries"));

            DrawSettingsCard("Built-in Log Colors", "Colors for the console's built-in channels.",
                ("defaultLogColor", "Default"),
                ("warningLogColor", "Warning"),
                ("errorLogColor", "Error"),
                ("commandEchoColor", "Command Echo"),
                ("hintColor", "Hint"));

            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUILayout.EndScrollView();
        }

        void DrawSettingsCard(string title, string subtitle, params (string prop, string label)[] fields)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, SettingsHeaderStyle);
                if (!string.IsNullOrEmpty(subtitle))
                    EditorGUILayout.LabelField(subtitle, SettingsSubtitleStyle);
                EditorGUILayout.Space(4);

                foreach ((string prop, string label) field in fields)
                {
                    SerializedProperty p = _serialized.FindProperty(field.prop);
                    if (p == null) continue;
                    EditorGUILayout.PropertyField(p, new GUIContent(field.label, p.tooltip));
                }
            }
            EditorGUILayout.Space(6);
        }

        static GUIStyle s_settingsHeaderStyle;
        static GUIStyle SettingsHeaderStyle
        {
            get
            {
                if (s_settingsHeaderStyle == null)
                    s_settingsHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12
                    };
                return s_settingsHeaderStyle;
            }
        }

        static GUIStyle s_settingsSubtitleStyle;
        static GUIStyle SettingsSubtitleStyle
        {
            get
            {
                if (s_settingsSubtitleStyle == null)
                    s_settingsSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        fontStyle = FontStyle.Italic
                    };
                return s_settingsSubtitleStyle;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Tab: Categories — list + ConsoleCategory.cs code generation
        // ──────────────────────────────────────────────────────────────────

        void DrawCategoriesTab()
        {
            EditorGUILayout.HelpBox(
                "Log categories — each name can be used as the first arg to DevConsole.Log.\n" +
                "Generate ConsoleCategory.cs to reference them in code as ConsoleCategory.<Name>.",
                MessageType.None);
            EditorGUILayout.Space(4);

            SerializedProperty list = _serialized.FindProperty("categories");
            if (list == null)
            {
                EditorGUILayout.HelpBox("SerializedProperty 'categories' not found on the config.", MessageType.Error);
                return;
            }

            _categoriesScroll = EditorGUILayout.BeginScrollView(_categoriesScroll, GUILayout.ExpandHeight(true));
            if (list.arraySize == 0)
                EditorGUILayout.LabelField("No categories yet — click \"+ Add category\" below.",
                    EditorStyles.centeredGreyMiniLabel);
            DrawCategoryRows(list);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add category", GUILayout.Width(120)))
                    AddCategory(list);
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.Space(4);

            // Reflect pending (un-applied) edits so the sync status sees the current list.
            _serialized.ApplyModifiedProperties();

            int count;
            ConsoleCategoryGenerator.SyncStatus status = ConsoleCategoryGenerator.GetStatus(_config, out count);

            switch (status)
            {
                case ConsoleCategoryGenerator.SyncStatus.Missing:
                    EditorGUILayout.HelpBox(
                        "ConsoleCategory.cs has not been generated yet. Click Generate.",
                        MessageType.Warning);
                    break;
                case ConsoleCategoryGenerator.SyncStatus.OutOfDate:
                    EditorGUILayout.HelpBox(
                        "ConsoleCategory.cs is out of date — the category list changed. Click Fetch to update.",
                        MessageType.Warning);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        $"ConsoleCategory.cs is up to date ({count} categor{(count == 1 ? "y" : "ies")}).",
                        MessageType.Info);
                    break;
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(status != ConsoleCategoryGenerator.SyncStatus.Missing))
                {
                    GUI.backgroundColor = status == ConsoleCategoryGenerator.SyncStatus.Missing
                        ? new Color(0.85f, 0.85f, 0.55f) : Color.white;
                    if (GUILayout.Button("Generate", GUILayout.Width(110)))
                        GenerateCategories();
                    GUI.backgroundColor = Color.white;
                }

                using (new EditorGUI.DisabledScope(status == ConsoleCategoryGenerator.SyncStatus.Missing))
                {
                    GUI.backgroundColor = status == ConsoleCategoryGenerator.SyncStatus.OutOfDate
                        ? new Color(0.85f, 0.85f, 0.55f) : Color.white;
                    if (GUILayout.Button("Fetch", GUILayout.Width(110)))
                        GenerateCategories();
                    GUI.backgroundColor = Color.white;
                }

                if (status != ConsoleCategoryGenerator.SyncStatus.Missing && GUILayout.Button("Open", GUILayout.Width(90)))
                    OpenCategoriesFile();
                GUILayout.FlexibleSpace();
            }
        }

        void GenerateCategories()
        {
            _serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            ConsoleCategoryGenerator.GenerateResult r = ConsoleCategoryGenerator.Generate(_config);
            DevConsoleEditorLog.Log(SeverityFor(r.Outcome), r.Message);
            AssetDatabase.Refresh();
        }

        static DevConsoleEditorLog.Severity SeverityFor(ConsoleCategoryGenerator.GenerateOutcome outcome)
        {
            switch (outcome)
            {
                case ConsoleCategoryGenerator.GenerateOutcome.Created:   return DevConsoleEditorLog.Severity.Success;
                case ConsoleCategoryGenerator.GenerateOutcome.Updated:   return DevConsoleEditorLog.Severity.Update;
                case ConsoleCategoryGenerator.GenerateOutcome.Empty:     return DevConsoleEditorLog.Severity.Warning;
                default:                                                 return DevConsoleEditorLog.Severity.Muted;
            }
        }

        void OpenCategoriesFile()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ConsoleCategoryGenerator.GetPath());
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        // Adds a blank, uniquely-named category — never a copy of the last one (which would surface
        // a duplicate identifier and warn on Generate).
        void AddCategory(SerializedProperty list)
        {
            int newIndex = list.arraySize;
            list.arraySize++;
            SerializedProperty elem = list.GetArrayElementAtIndex(newIndex);
            SerializedProperty nameProp = elem.FindPropertyRelative("name");
            SerializedProperty colorProp = elem.FindPropertyRelative("color");
            if (nameProp != null) nameProp.stringValue = MakeUniqueCategoryName(list, newIndex);
            if (colorProp != null) colorProp.colorValue = Color.white;
        }

        void DrawCategoryRows(SerializedProperty list)
        {
            int removeIndex = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty elem = list.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = elem.FindPropertyRelative("name");
                SerializedProperty colorProp = elem.FindPropertyRelative("color");
                if (nameProp == null) continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Name", GUILayout.Width(40));
                    nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

                    EditorGUILayout.LabelField("Color", GUILayout.Width(40));
                    if (colorProp != null)
                        colorProp.colorValue = EditorGUILayout.ColorField(colorProp.colorValue, GUILayout.Width(80));

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                        removeIndex = i;
                }
            }
            if (removeIndex >= 0)
            {
                list.DeleteArrayElementAtIndex(removeIndex);
                _serialized.ApplyModifiedProperties();
            }
        }

        static string MakeUniqueCategoryName(SerializedProperty list, int ignoreIndex)
        {
            const string BASE_NAME = "NewCategory";
            HashSet<string> existing = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                if (i == ignoreIndex) continue;
                SerializedProperty nameProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                    existing.Add(nameProp.stringValue);
            }
            if (!existing.Contains(BASE_NAME)) return BASE_NAME;
            int n = 1;
            while (existing.Contains($"{BASE_NAME}{n}")) n++;
            return $"{BASE_NAME}{n}";
        }

        // ──────────────────────────────────────────────────────────────────
        //  Tab: Bridges — scaffold-once file management
        // ──────────────────────────────────────────────────────────────────

        void DrawBridgesTab()
        {
            EditorGUILayout.HelpBox(
                "Each row is one bridge file under " + BridgeCodeGenerator.OUTPUT_FOLDER + "/.\n" +
                "• Generate writes a minimal MonoBehaviour scaffold; once it exists the button flips to Open.\n" +
                "• Status shows whether each entry is Generated, Missing, a Duplicate, or has an Invalid name.\n" +
                "• Fetch generates every missing entry and imports any bridge files on disk not yet listed here.",
                MessageType.None);
            EditorGUILayout.Space(4);

            var bridgesProp = _serialized.FindProperty("bridges");
            if (bridgesProp == null)
            {
                EditorGUILayout.HelpBox("SerializedProperty 'bridges' not found.", MessageType.Error);
                return;
            }

            _bridgesScroll = EditorGUILayout.BeginScrollView(_bridgesScroll, GUILayout.ExpandHeight(true));
            if (bridgesProp.arraySize == 0)
                EditorGUILayout.LabelField("No bridges yet — click \"+ Add bridge\" below.",
                    EditorStyles.centeredGreyMiniLabel);
            DrawBridgeRows(bridgesProp);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add bridge", GUILayout.Width(110)))
                {
                    bridgesProp.arraySize++;
                    // Set the new element's className to a unique default the user can edit, so a
                    // double-click doesn't immediately create a Duplicate row.
                    int newIndex = bridgesProp.arraySize - 1;
                    var nameProp = bridgesProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("className");
                    if (nameProp != null) nameProp.stringValue = MakeUniqueBridgeName(bridgesProp, newIndex);
                }
                if (GUILayout.Button(new GUIContent("Fetch",
                        "Generate every missing bridge and import existing bridge files not yet in this list."),
                        GUILayout.Width(110)))
                    FetchBridges();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Bridges Folder", GUILayout.Width(160)))
                    PingBridgesFolder();
            }
        }

        enum BridgeStatus { Generated, Missing, Duplicate, Invalid }

        static GUIStyle s_statusStyle;
        static GUIStyle StatusStyle
        {
            get
            {
                if (s_statusStyle == null)
                    s_statusStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        richText = true,
                        alignment = TextAnchor.MiddleLeft
                    };
                return s_statusStyle;
            }
        }

        void DrawBridgeRows(SerializedProperty bridgesProp)
        {
            int removeIndex = -1;
            for (int i = 0; i < bridgesProp.arraySize; i++)
            {
                var elem = bridgesProp.GetArrayElementAtIndex(i);
                var nameProp = elem.FindPropertyRelative("className");
                if (nameProp == null) continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Class", GUILayout.Width(40));
                    nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

                    // Apply pending edits so the status check sees the current class name when the
                    // user just typed and clicked in the same frame.
                    _serialized.ApplyModifiedProperties();

                    string tooltip;
                    BridgeStatus status = GetBridgeStatus(i, out tooltip);
                    DrawBridgeStatusChip(status, tooltip);
                    DrawBridgeActionButton(i, status);

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                        removeIndex = i;
                }
            }
            if (removeIndex >= 0)
            {
                bridgesProp.DeleteArrayElementAtIndex(removeIndex);
                _serialized.ApplyModifiedProperties();
            }
        }

        // Classifies one bridge row. Invalid (empty / bad name) takes priority, then Duplicate
        // (matches an earlier row), then on-disk existence (Generated vs Missing).
        BridgeStatus GetBridgeStatus(int index, out string tooltip)
        {
            string name = _config.bridges[index].className;
            name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

            if (name.Length == 0)
            {
                tooltip = "Bridge has no class name.";
                return BridgeStatus.Invalid;
            }
            if (!name.EndsWith("Bridge"))
            {
                tooltip = $"Class name '{name}' must end with 'Bridge'.";
                return BridgeStatus.Invalid;
            }
            for (int j = 0; j < index; j++)
            {
                string other = _config.bridges[j].className;
                other = string.IsNullOrWhiteSpace(other) ? string.Empty : other.Trim();
                if (other.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    tooltip = $"Duplicate of row {j + 1} ('{other}'). Remove one of them.";
                    return BridgeStatus.Duplicate;
                }
            }
            if (BridgeCodeGenerator.FileExists(_config.bridges[index]))
            {
                tooltip = $"{BridgeCodeGenerator.GetPath(_config.bridges[index])} exists.";
                return BridgeStatus.Generated;
            }
            tooltip = "File not generated yet — click Generate.";
            return BridgeStatus.Missing;
        }

        void DrawBridgeStatusChip(BridgeStatus status, string tooltip)
        {
            string label, color;
            switch (status)
            {
                case BridgeStatus.Generated: label = "✔ Generated"; color = "#7FD97F"; break;
                case BridgeStatus.Missing:   label = "● Missing";   color = "#E0C24C"; break;
                case BridgeStatus.Duplicate: label = "⚠ Duplicate"; color = "#E0A24C"; break;
                default:                     label = "✖ Invalid";   color = "#E06C6C"; break;
            }
            GUILayout.Label(new GUIContent($"<color={color}>{label}</color>", tooltip),
                StatusStyle, GUILayout.Width(96));
        }

        void DrawBridgeActionButton(int index, BridgeStatus status)
        {
            BridgeDefinition b = _config.bridges[index];
            switch (status)
            {
                case BridgeStatus.Generated:
                    GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                    if (GUILayout.Button("Open", GUILayout.Width(90))) OpenBridgeFile(b);
                    GUI.backgroundColor = Color.white;
                    break;
                case BridgeStatus.Missing:
                    GUI.backgroundColor = new Color(0.85f, 0.85f, 0.55f);
                    if (GUILayout.Button("Generate", GUILayout.Width(90))) GenerateOne(b);
                    GUI.backgroundColor = Color.white;
                    break;
                case BridgeStatus.Duplicate:
                    // A duplicate may still point at an existing file — let the user jump to it.
                    if (BridgeCodeGenerator.FileExists(b))
                    {
                        if (GUILayout.Button("Open", GUILayout.Width(90))) OpenBridgeFile(b);
                    }
                    else
                    {
                        DrawDisabledPlaceholderButton();
                    }
                    break;
                default: // Invalid — nothing actionable, keep the row aligned.
                    DrawDisabledPlaceholderButton();
                    break;
            }
        }

        static void DrawDisabledPlaceholderButton()
        {
            using (new EditorGUI.DisabledScope(true))
                GUILayout.Button("—", GUILayout.Width(90));
        }

        static string MakeUniqueBridgeName(SerializedProperty list, int ignoreIndex)
        {
            const string BASE_NAME = "NewBridge";
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.arraySize; i++)
            {
                if (i == ignoreIndex) continue;
                SerializedProperty nameProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("className");
                if (nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue))
                    existing.Add(nameProp.stringValue.Trim());
            }
            if (!existing.Contains(BASE_NAME)) return BASE_NAME;
            int n = 2;
            while (existing.Contains($"New{n}Bridge")) n++;
            return $"New{n}Bridge";
        }

        void OpenBridgeFile(BridgeDefinition b)
        {
            string path = BridgeCodeGenerator.GetPath(b);
            if (string.IsNullOrEmpty(path)) return;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        void GenerateOne(BridgeDefinition b)
        {
            _serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            var r = BridgeCodeGenerator.Generate(b);
            LogResult(r);
            AssetDatabase.Refresh();
        }

        // Two-way sync: pull in any bridge .cs files on disk that aren't listed yet, then generate
        // a scaffold for every listed entry still missing its file.
        void FetchBridges()
        {
            _serialized.ApplyModifiedProperties();

            int imported = ImportExistingBridges();

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();

            int created = 0, existed = 0, failed = 0;
            foreach (BridgeDefinition b in _config.bridges)
            {
                BridgeCodeGenerator.GenerateResult r = BridgeCodeGenerator.Generate(b);
                switch (r.Outcome)
                {
                    case BridgeCodeGenerator.GenerateOutcome.Created:       created++; DevConsoleEditorLog.Log(SeverityFor(r.Outcome), r.Message); break;
                    case BridgeCodeGenerator.GenerateOutcome.AlreadyExists: existed++; break;
                    default:                                                failed++;  DevConsoleEditorLog.Log(SeverityFor(r.Outcome), r.Message); break;
                }
            }
            AssetDatabase.Refresh();
            DevConsoleEditorLog.Severity summarySeverity = failed > 0
                ? DevConsoleEditorLog.Severity.Warning
                : DevConsoleEditorLog.Severity.Muted;
            DevConsoleEditorLog.Log(summarySeverity,
                $"Bridges — {imported} imported, {created} created, {existed} already existed, {failed} failed.");
        }

        // Adds a list entry for every *Bridge.cs in the output folder that isn't already listed.
        // Returns how many were imported. ConsoleCategory.cs is excluded by the *Bridge.cs filter.
        int ImportExistingBridges()
        {
            if (!AssetDatabase.IsValidFolder(BridgeCodeGenerator.OUTPUT_FOLDER))
                return 0;

            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BridgeDefinition b in _config.bridges)
            {
                if (b != null && !string.IsNullOrWhiteSpace(b.className))
                    known.Add(b.className.Trim());
            }

            string[] files = Directory.GetFiles(
                BridgeCodeGenerator.OUTPUT_FOLDER, "*Bridge.cs", SearchOption.TopDirectoryOnly);

            SerializedProperty bridges = _serialized.FindProperty("bridges");
            int imported = 0;
            foreach (string file in files)
            {
                string className = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(className) || known.Contains(className)) continue;

                bridges.arraySize++;
                SerializedProperty nameProp = bridges
                    .GetArrayElementAtIndex(bridges.arraySize - 1)
                    .FindPropertyRelative("className");
                if (nameProp != null) nameProp.stringValue = className;

                known.Add(className);
                imported++;
                DevConsoleEditorLog.Log(DevConsoleEditorLog.Severity.Update,
                    $"Imported existing bridge '{className}' into the list.");
            }
            if (imported > 0) _serialized.ApplyModifiedProperties();
            return imported;
        }

        void LogResult(BridgeCodeGenerator.GenerateResult r)
        {
            DevConsoleEditorLog.Log(SeverityFor(r.Outcome), r.Message);
        }

        static DevConsoleEditorLog.Severity SeverityFor(BridgeCodeGenerator.GenerateOutcome outcome)
        {
            switch (outcome)
            {
                case BridgeCodeGenerator.GenerateOutcome.Created:       return DevConsoleEditorLog.Severity.Success;
                case BridgeCodeGenerator.GenerateOutcome.AlreadyExists: return DevConsoleEditorLog.Severity.Muted;
                default:                                                return DevConsoleEditorLog.Severity.Error;
            }
        }

        void PingBridgesFolder()
        {
            if (!AssetDatabase.IsValidFolder(BridgeCodeGenerator.OUTPUT_FOLDER))
            {
                EditorUtility.DisplayDialog("DevConsole Bridges",
                    $"The bridges folder ({BridgeCodeGenerator.OUTPUT_FOLDER}) doesn't exist yet. " +
                    "Click Generate on any bridge to create it.",
                    "OK");
                return;
            }
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(BridgeCodeGenerator.OUTPUT_FOLDER);
            if (folder != null) EditorGUIUtility.PingObject(folder);
        }
    }
}
