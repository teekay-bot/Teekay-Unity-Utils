using System.IO;
using TeekayUtils.Editor;
using UnityEditor;
using UnityEngine;

/// Manual usage demos for the package's editor utilities.
/// Lives in the dev project only — never ships with the package.
public static class TeekayUtilsDemo
{
    [MenuItem("Teekay/Demo/Ping Package Manifest")]
    static void PingManifest()
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>(
            "Packages/com.teekay.unity-utils/package.json");
        asset.PingAndSelect();
    }

    [MenuItem("Teekay/Demo/Save Text With Confirm")]
    static void SaveWithConfirm()
    {
        string folder = EditorFileUtils.BrowseForFolder(Application.dataPath);
        if (string.IsNullOrEmpty(folder)) return;

        string path = Path.Combine(folder, "demo.txt");
        if (EditorFileUtils.ConfirmOverwrite(path))
        {
            File.WriteAllText(path, "hello from TeekayUtils");
            Debug.Log($"Wrote {path}");
        }
    }
}
