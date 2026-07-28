using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TeekayUtils.EditorTools
{
    /// <summary>
    /// Property drawer for <c>[SerializeReference, SubclassSelector]</c> fields. Draws a type
    /// dropdown on the header row and the chosen instance's own fields underneath.
    /// <para>
    /// Children are walked by hand rather than via
    /// <c>EditorGUI.PropertyField(position, property, label, true)</c>: that call would resolve the
    /// handler for this same property, re-enter this drawer and recurse until the stack blows.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public sealed class SubclassSelectorDrawer : PropertyDrawer
    {
        const string MisuseMessage = "[SubclassSelector] needs [SerializeReference]";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            foreach (SerializedProperty child in VisibleChildren(property))
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Saying so beats drawing a field that looks fine but can never hold a value.
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label, new GUIContent(MisuseMessage));
                return;
            }

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            DrawHeader(headerRect, property, label);

            if (!property.isExpanded) return;

            EditorGUI.indentLevel++;
            float y = headerRect.yMax;
            foreach (SerializedProperty child in VisibleChildren(property))
            {
                float height = EditorGUI.GetPropertyHeight(child, true);
                y += EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                y += height;
            }
            EditorGUI.indentLevel--;
        }

        static void DrawHeader(Rect rect, SerializedProperty property, GUIContent label)
        {
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            var buttonRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y,
                                      Mathf.Max(0f, rect.width - EditorGUIUtility.labelWidth), rect.height);

            // Confined to the label column on purpose: a Foldout with toggleOnLabelClick swallows
            // mouse events across the WHOLE rect it is handed, so a full-width one would eat every
            // click meant for the dropdown — leaving the type unchangeable the moment the chosen
            // type has fields to fold out to.
            //
            // Only foldout when there is something to reveal; a parameterless implementation would
            // otherwise offer an arrow that opens onto nothing.
            if (HasVisibleChildren(property))
            {
                property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);
            }
            else
            {
                property.isExpanded = false;
                EditorGUI.LabelField(labelRect, label);
            }

            // The button sits in the value column, which a managed reference leaves empty. Indent
            // must be zeroed or the rect gets shifted a second time.
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(CurrentTypeLabel(property)), FocusType.Keyboard))
                ShowTypeMenu(buttonRect, property);
            EditorGUI.indentLevel = indent;
        }

        static string CurrentTypeLabel(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues) return "—";

            Type current = CurrentType(property);
            return current == null ? "None" : current.Name;
        }

        static Type CurrentType(SerializedProperty property)
        {
            return property.managedReferenceValue?.GetType();
        }

        static void ShowTypeMenu(Rect rect, SerializedProperty property)
        {
            Type fieldType = SubclassSelectorTypes.ResolveFieldType(property.managedReferenceFieldTypename);
            if (fieldType == null) return;

            List<Type> types = SubclassSelectorTypes.GetSelectable(fieldType);
            string[] labels = SubclassSelectorTypes.BuildMenuLabels(types);
            Type current = property.hasMultipleDifferentValues ? null : CurrentType(property);

            // Captured by path, not by SerializedProperty: menu callbacks run after OnGUI has
            // returned, and a SerializedProperty is not valid to hold onto past that point.
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), current == null, () => Assign(serializedObject, path, null));

            if (types.Count > 0) menu.AddSeparator(string.Empty);

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(labels[i]), current == type, () => Assign(serializedObject, path, type));
            }

            menu.DropDown(rect);
        }

        static void Assign(SerializedObject serializedObject, string path, Type type)
        {
            // Re-read: by the time the callback runs, the cached state may be stale.
            serializedObject.Update();

            SerializedProperty property = serializedObject.FindProperty(path);
            if (property == null) return;

            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.isExpanded = true;
            // Routing through the SerializedObject is what registers the undo entry.
            serializedObject.ApplyModifiedProperties();
        }

        static bool HasVisibleChildren(SerializedProperty property)
        {
            foreach (SerializedProperty _ in VisibleChildren(property)) return true;
            return false;
        }

        /// <summary>
        /// The property's own visible children, stopping at its sibling rather than running on
        /// through the rest of the SerializedObject.
        /// </summary>
        static IEnumerable<SerializedProperty> VisibleChildren(SerializedProperty property)
        {
            SerializedProperty end = property.GetEndProperty();
            SerializedProperty child = property.Copy();
            bool enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                yield return child.Copy();
            }
        }
    }
}
