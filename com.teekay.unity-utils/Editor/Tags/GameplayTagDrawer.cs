using System.Collections.Generic;
using TeekayUtils.Tags;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace TeekayUtils.EditorTools
{
    /// <summary>
    /// Draws a <see cref="GameplayTagAttribute"/> string as a picker over the project's
    /// <see cref="GameplayTagCatalog"/>: a dropdown button opening a searchable, dot-hierarchy
    /// tree (Unreal's tag picker at repo scale), a "New tag…" entry that coins a path into the
    /// catalog on the spot, and a warning icon when the current value is not in the catalog —
    /// the typo that would otherwise fail silently as a valid-but-matchless tag.
    /// </summary>
    /// <remarks>
    /// Works unchanged inside [SerializeReference]/[SubclassSelector] blocks: attribute drawers
    /// apply per field (per element, for arrays) regardless of who draws the parent.
    /// </remarks>
    [CustomPropertyDrawer(typeof(GameplayTagAttribute))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                // The attribute is on something that is not a string — report instead of hiding the field.
                EditorGUI.LabelField(position, label.text, "[GameplayTag] only applies to strings.");
                return;
            }

            Rect field = EditorGUI.PrefixLabel(position, label);

            GameplayTagCatalog catalog = FindCatalog();
            if (catalog == null)
            {
                DrawCreateCatalogButton(field, property);
                return;
            }

            string value = property.stringValue;
            GUIContent buttonContent = BuildButtonContent(value, catalog);

            if (EditorGUI.DropdownButton(field, buttonContent, FocusType.Keyboard))
            {
                // The dropdown outlives this OnGUI call, so it gets the property's ADDRESS
                // (serializedObject + path), not the SerializedProperty itself — array element
                // properties go stale the moment the list is reordered under them.
                var dropdown = new TagPickerDropdown(
                    new AdvancedDropdownState(), catalog,
                    property.serializedObject, property.propertyPath);
                dropdown.Show(field);
            }
        }

        static GUIContent BuildButtonContent(string value, GameplayTagCatalog catalog)
        {
            if (string.IsNullOrEmpty(value)) return new GUIContent("(none)");

            if (!catalog.Contains(value))
                return new GUIContent(
                    " " + value,
                    EditorGUIUtility.IconContent("console.warnicon.sml").image,
                    "Not in the tag catalog — either a typo, or a tag coined elsewhere. " +
                    "Pick from the list or add this path via 'New tag…'.");

            return new GUIContent(value);
        }

        static void DrawCreateCatalogButton(Rect field, SerializedProperty property)
        {
            if (!GUI.Button(field, "No tag catalog — create one")) return;

            var catalog = ScriptableObject.CreateInstance<GameplayTagCatalog>();
            // Project root by default — move it wherever fits; the picker finds it by type.
            AssetDatabase.CreateAsset(catalog, "Assets/GameplayTagCatalog.asset");
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(catalog);
            // No property change — the button replaces the picker only until the catalog exists.
        }

        internal static GameplayTagCatalog FindCatalog()
        {
            // First hit wins — one catalog per project is the documented convention.
            string[] guids = AssetDatabase.FindAssets("t:GameplayTagCatalog");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<GameplayTagCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// The searchable tree. Dots become hierarchy; a catalog path that is also a prefix of
        /// deeper paths gets a "(this)" leaf so the parent stays pickable (AdvancedDropdown
        /// parents with children only expand). Special leaves: "(none)" clears, "New tag…" opens
        /// the coin-a-path prompt.
        /// </summary>
        class TagPickerDropdown : AdvancedDropdown
        {
            readonly GameplayTagCatalog _catalog;
            readonly SerializedObject _target;
            readonly string _propertyPath;
            readonly Dictionary<int, string> _pathsByItemId = new Dictionary<int, string>();

            // AdvancedDropdownItem derives its default id from the display NAME, so two leaves
            // sharing a name in different branches (two "(this)" entries, repeated segment words)
            // would collide in _pathsByItemId. Every item gets an explicit unique id instead.
            int _nextItemId = 1;

            const string NoneSentinel = "";
            const string NewTagSentinel = "\0new";

            public TagPickerDropdown(AdvancedDropdownState state, GameplayTagCatalog catalog,
                                     SerializedObject target, string propertyPath) : base(state)
            {
                _catalog = catalog;
                _target = target;
                _propertyPath = propertyPath;
                minimumSize = new Vector2(260f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Gameplay Tags");
                var nodes = new Dictionary<string, AdvancedDropdownItem> { [""] = root };

                // Sorted copy so siblings group predictably even if the asset was hand-edited.
                var sorted = new List<string>(_catalog.Paths);
                sorted.Sort(System.StringComparer.Ordinal);

                foreach (string path in sorted)
                {
                    if (!GameplayTag.IsValidPath(path, out _)) continue; // hand-edited garbage: skip, don't crash

                    AdvancedDropdownItem parent = EnsureNode(nodes, ParentOf(path));
                    string leafName = path.Substring(path.LastIndexOf('.') + 1);

                    // If this path already exists as a GROUPING node (a deeper path created it
                    // first), it cannot be clicked — give it a "(this)" leaf instead.
                    if (nodes.TryGetValue(path, out AdvancedDropdownItem grouping))
                    {
                        AddLeaf(grouping, leafName + " (this)", path);
                        continue;
                    }

                    // Peek whether anything deeper shares this prefix; if so, register a grouping
                    // node AND its "(this)" leaf now, keeping insertion order stable.
                    if (HasDescendant(sorted, path))
                    {
                        AdvancedDropdownItem node = EnsureNode(nodes, path);
                        AddLeaf(node, leafName + " (this)", path);
                    }
                    else
                    {
                        AddLeaf(parent, leafName, path);
                    }
                }

                root.AddSeparator();
                AddLeaf(root, "(none)", NoneSentinel);
                AddLeaf(root, "New tag…", NewTagSentinel);
                return root;

                AdvancedDropdownItem EnsureNode(Dictionary<string, AdvancedDropdownItem> map, string path)
                {
                    if (map.TryGetValue(path, out AdvancedDropdownItem existing)) return existing;

                    AdvancedDropdownItem parent = EnsureNode(map, ParentOf(path));
                    var node = new AdvancedDropdownItem(path.Substring(path.LastIndexOf('.') + 1))
                    {
                        id = _nextItemId++
                    };
                    parent.AddChild(node);
                    map[path] = node;
                    return node;
                }

                void AddLeaf(AdvancedDropdownItem parent, string name, string fullPath)
                {
                    var leaf = new AdvancedDropdownItem(name) { id = _nextItemId++ };
                    _pathsByItemId[leaf.id] = fullPath;
                    parent.AddChild(leaf);
                }
            }

            static string ParentOf(string path)
            {
                int lastDot = path.LastIndexOf('.');
                return lastDot < 0 ? "" : path.Substring(0, lastDot);
            }

            static bool HasDescendant(List<string> sortedPaths, string path)
            {
                string prefix = path + ".";
                foreach (string candidate in sortedPaths)
                    if (candidate.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
                return false;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (!_pathsByItemId.TryGetValue(item.id, out string picked)) return; // a grouping node

                if (picked == NewTagSentinel)
                {
                    NewTagPrompt.Show(_catalog, _target, _propertyPath);
                    return;
                }

                Apply(_target, _propertyPath, picked);
            }

            internal static void Apply(SerializedObject target, string propertyPath, string value)
            {
                // Re-resolved by path: see the drawer comment on stale array-element properties.
                target.Update();
                SerializedProperty property = target.FindProperty(propertyPath);
                if (property == null) return; // the element was removed while the dropdown was open
                property.stringValue = value;
                target.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// The "coin a new path" prompt: one text field, validated by the same rules the runtime
        /// enforces, added to the catalog (undo-able) and written into the field that asked.
        /// </summary>
        class NewTagPrompt : EditorWindow
        {
            GameplayTagCatalog _catalog;
            SerializedObject _target;
            string _propertyPath;
            string _input = "";

            public static void Show(GameplayTagCatalog catalog, SerializedObject target, string propertyPath)
            {
                var window = CreateInstance<NewTagPrompt>();
                window._catalog = catalog;
                window._target = target;
                window._propertyPath = propertyPath;
                window.titleContent = new GUIContent("New Gameplay Tag");
                window.minSize = window.maxSize = new Vector2(340f, 76f);
                window.ShowUtility();
            }

            void OnGUI()
            {
                GUI.SetNextControlName("path");
                _input = EditorGUILayout.TextField("Tag path", _input);
                EditorGUI.FocusTextInControl("path");

                bool valid = GameplayTag.IsValidPath(_input, out string error);
                bool duplicate = valid && _catalog.Contains(_input);
                if (!valid) EditorGUILayout.LabelField(error ?? "", EditorStyles.miniLabel);
                else if (duplicate) EditorGUILayout.LabelField("Already in the catalog — it will just be selected.", EditorStyles.miniLabel);
                else EditorGUILayout.LabelField(" ", EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(!valid))
                {
                    bool submitted = GUILayout.Button("Add & use") ||
                                     (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return);
                    if (!submitted) return;

                    if (!duplicate)
                    {
                        Undo.RecordObject(_catalog, "Add Gameplay Tag");
                        _catalog.Add(_input);
                        EditorUtility.SetDirty(_catalog);
                    }

                    TagPickerDropdown.Apply(_target, _propertyPath, _input);
                    Close();
                }
            }
        }
    }
}
