using UnityEditor;
using UnityEngine;

namespace TeekayUtils.DevConsole.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="DevConsoleConfig"/>. The full editing surface lives in
    /// <c>Tools → DevConsole → Config</c>; the inspector intentionally exposes only the read-only
    /// Script field and a button that opens that window. This keeps the asset from rendering
    /// two competing editors for the same data.
    /// </summary>
    [CustomEditor(typeof(DevConsoleConfig))]
    // Fully qualified: bare "Editor" resolves to the TeekayUtils.Editor namespace here.
    public sealed class DevConsoleConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the Script row Unity normally adds for ScriptableObjects (disabled, read-only).
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script",
                    MonoScript.FromScriptableObject((DevConsoleConfig)target),
                    typeof(MonoScript), false);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "All settings live in the dedicated window: Tools → DevConsole → Config.",
                MessageType.Info);

            if (GUILayout.Button("Open in DevConsole Window", GUILayout.Height(24)))
                DevConsoleConfigWindow.Open();
        }
    }
}
