using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TeekayUtils.DevConsole.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/Scripts/DevConsoleGenerated/ConsoleCategory.cs</c> — a static class of
    /// string constants, one per category authored in a <see cref="DevConsoleConfig"/>. Code then
    /// logs with <c>DevConsole.Log(ConsoleCategory.MyCategory, ...)</c> instead of a magic string,
    /// and the category color lives entirely in the config (no code-side RegisterCategory).
    ///
    /// Unlike the bridge scaffold, this file is fully owned by the generator: re-running rewrites
    /// it, but only when the content actually changed (so an unchanged config is a no-op).
    /// </summary>
    public static class ConsoleCategoryGenerator
    {
        public const string OUTPUT_PATH = BridgeCodeGenerator.OUTPUT_FOLDER + "/ConsoleCategory.cs";
        const string NAMESPACE  = "TeekayUtils.DevConsole";
        const string CLASS_NAME = "ConsoleCategory";

        public enum GenerateOutcome { Created, Updated, Unchanged, Empty }

        public enum SyncStatus { Missing, OutOfDate, UpToDate }

        public struct GenerateResult
        {
            public GenerateOutcome Outcome;
            public string Path;
            public string Message;
        }

        public static string GetPath() => OUTPUT_PATH;

        public static bool FileExists() => File.Exists(OUTPUT_PATH);

        // Compares the on-disk file against what the config would generate right now, so the UI
        // can warn when the category list has drifted from ConsoleCategory.cs.
        public static SyncStatus GetStatus(DevConsoleConfig config, out int count)
        {
            string content = BuildContent(config, out count);
            if (!File.Exists(OUTPUT_PATH)) return SyncStatus.Missing;
            return File.ReadAllText(OUTPUT_PATH) == content ? SyncStatus.UpToDate : SyncStatus.OutOfDate;
        }

        public static GenerateResult Generate(DevConsoleConfig config)
        {
            GenerateResult res = new GenerateResult { Path = OUTPUT_PATH };

            int count;
            string content = BuildContent(config, out count);

            bool existed = File.Exists(OUTPUT_PATH);
            if (existed && File.ReadAllText(OUTPUT_PATH) == content)
            {
                res.Outcome = GenerateOutcome.Unchanged;
                res.Message = $"{OUTPUT_PATH} already up to date ({count} categor{(count == 1 ? "y" : "ies")}).";
                return res;
            }

            EnsureOutputFolder();
            File.WriteAllText(OUTPUT_PATH, content);
            AssetDatabase.ImportAsset(OUTPUT_PATH);

            if (count == 0)
            {
                res.Outcome = GenerateOutcome.Empty;
                res.Message = $"{OUTPUT_PATH} written with no categories — add some in the Categories tab.";
            }
            else
            {
                res.Outcome = existed ? GenerateOutcome.Updated : GenerateOutcome.Created;
                res.Message = $"{(existed ? "Updated" : "Created")} {OUTPUT_PATH} ({count} categor{(count == 1 ? "y" : "ies")}).";
            }
            return res;
        }

        static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
                AssetDatabase.CreateFolder("Assets", "Scripts");
            if (!AssetDatabase.IsValidFolder(BridgeCodeGenerator.OUTPUT_FOLDER))
                AssetDatabase.CreateFolder("Assets/Scripts", "DevConsoleGenerated");
        }

        // Builds the full file text. `count` returns how many constants were emitted.
        static string BuildContent(DevConsoleConfig config, out int count)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED — Do not edit manually.");
            sb.AppendLine("// Re-generated via Tools > DevConsole > Config (Categories tab).");
            sb.AppendLine();
            sb.AppendLine($"namespace {NAMESPACE}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Log category names authored in DevConsoleConfig. First arg to DevConsole.Log.</summary>");
            sb.AppendLine($"    public static class {CLASS_NAME}");
            sb.AppendLine("    {");

            count = 0;
            if (config != null && config.categories != null)
            {
                HashSet<string> seen = new HashSet<string>();
                foreach (CategoryEntry cat in config.categories)
                {
                    if (cat == null || string.IsNullOrWhiteSpace(cat.name)) continue;
                    string identifier = ToIdentifier(cat.name);
                    if (string.IsNullOrEmpty(identifier)) continue;
                    if (!seen.Add(identifier))
                    {
                        DevConsoleEditorLog.Log(DevConsoleEditorLog.Severity.Warning,
                            $"Category '{cat.name}' maps to duplicate identifier '{identifier}' — skipped. " +
                            "Rename it to get a unique constant.");
                        continue;
                    }
                    sb.AppendLine($"        public const string {identifier} = \"{Escape(cat.name)}\";");
                    count++;
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Mirrors SceneNameGenerator.ToIdentifier: split on non-alphanumeric, PascalCase each
        // part, prefix '_' if the result starts with a digit.
        static string ToIdentifier(string name)
        {
            string[] parts = Regex.Split(name, @"[^a-zA-Z0-9]+");
            StringBuilder sb = new StringBuilder();
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1) sb.Append(part[1..]);
            }
            if (sb.Length > 0 && char.IsDigit(sb[0]))
                sb.Insert(0, '_');
            return sb.ToString();
        }

        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
