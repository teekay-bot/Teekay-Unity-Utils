using System.IO;
using UnityEditor;

namespace TeekayUtils.Editor
{
    /// <summary>
    /// Static helpers for editor file dialogs.
    /// </summary>
    public static class EditorFileUtils
    {
        /// <summary>
        /// Checks if a file exists at the specified path and prompts the user for confirmation to overwrite it.
        /// </summary>
        /// <param name="path">The file path to check.</param>
        /// <returns>True if the file does not exist or the user confirms the overwrite; otherwise, false.</returns>
        public static bool ConfirmOverwrite(string path)
        {
            if (File.Exists(path))
            {
                return EditorUtility.DisplayDialog
                (
                    "File Exists",
                    "The file already exists at the specified path. Do you want to overwrite it?",
                    "Yes",
                    "No"
                );
            }

            return true;
        }

        /// <summary>
        /// Opens a folder browser dialog and returns the selected folder path.
        /// </summary>
        /// <param name="defaultPath">The default path to open the folder browser at.</param>
        /// <returns>The selected folder path, or an empty string if the user cancels.</returns>
        public static string BrowseForFolder(string defaultPath)
        {
            return EditorUtility.SaveFolderPanel
            (
                "Choose Save Path",
                defaultPath,
                ""
            );
        }
    }
}
