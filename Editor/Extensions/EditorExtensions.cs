using UnityEditor;
using UnityEngine;

namespace TeekayUtils.Editor
{
    /// <summary>
    /// Extension methods for editor-side asset workflows.
    /// </summary>
    public static class EditorExtensions
    {
        /// <summary>
        /// Pings and selects the specified asset in the Unity Editor.
        /// </summary>
        /// <param name="asset">The asset to ping and select.</param>
        public static void PingAndSelect(this Object asset)
        {
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
