using System.IO;
using UnityEditor;

namespace TeekayUtils.DevConsole.EditorTools
{
    /// <summary>
    /// Writes a minimal MonoBehaviour scaffold for a bridge entry. Strictly one-shot:
    /// once <c>Assets/Scripts/DevConsoleGenerated/{ClassName}.cs</c> exists, this generator
    /// refuses to touch it. From that point on the file is the user's, and the Bridges tab
    /// flips its button from "Generate" to "Open" so the row stays useful as a jump target.
    ///
    /// No region markers, no member-path emission, no reflection — the scaffold is pure
    /// boilerplate and the user fills in CVar/command registrations by hand.
    /// </summary>
    public static class BridgeCodeGenerator
    {
        public const string OUTPUT_FOLDER = "Assets/Scripts/DevConsoleGenerated";
        public const string NAMESPACE    = "ConsoleBridge";

        public enum GenerateOutcome { Created, AlreadyExists, Aborted }

        public struct GenerateResult
        {
            public GenerateOutcome Outcome;
            public string Path;
            public string Message;
        }

        public static string GetPath(BridgeDefinition bridge) =>
            (bridge == null || string.IsNullOrWhiteSpace(bridge.className))
                ? null
                : $"{OUTPUT_FOLDER}/{bridge.className.Trim()}.cs";

        public static bool FileExists(BridgeDefinition bridge)
        {
            string path = GetPath(bridge);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public static GenerateResult Generate(BridgeDefinition bridge)
        {
            var res = new GenerateResult();

            if (bridge == null || string.IsNullOrWhiteSpace(bridge.className))
            {
                res.Outcome = GenerateOutcome.Aborted;
                res.Message = "Bridge has no class name.";
                return res;
            }
            string className = bridge.className.Trim();
            if (!className.EndsWith("Bridge"))
            {
                res.Outcome = GenerateOutcome.Aborted;
                res.Message = $"Bridge class name '{className}' must end with 'Bridge'.";
                return res;
            }

            EnsureOutputFolder();
            string path = $"{OUTPUT_FOLDER}/{className}.cs";
            res.Path = path;

            if (File.Exists(path))
            {
                res.Outcome = GenerateOutcome.AlreadyExists;
                res.Message = $"{path} already exists — left untouched.";
                return res;
            }

            File.WriteAllText(path, BuildTemplate(className));
            AssetDatabase.ImportAsset(path);
            res.Outcome = GenerateOutcome.Created;
            res.Message = $"Created {path}";
            return res;
        }

        static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
                AssetDatabase.CreateFolder("Assets", "Scripts");
            if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
                AssetDatabase.CreateFolder("Assets/Scripts", "DevConsoleGenerated");
        }

        static string BuildTemplate(string className) =>
$@"using UnityEngine;
using TeekayUtils.DevConsole;

namespace {NAMESPACE}
{{
    /// <summary>
    /// Hand-edit this bridge to register CVars, commands, and log categories with the DevConsole.
    /// See Assets/Scripts/Developer/DevConsole/README.md for the full registration API.
    /// </summary>
    public class {className} : MonoBehaviour
    {{
        void OnEnable()
        {{
        }}

        void OnDisable()
        {{
        }}
    }}
}}
";
    }
}
