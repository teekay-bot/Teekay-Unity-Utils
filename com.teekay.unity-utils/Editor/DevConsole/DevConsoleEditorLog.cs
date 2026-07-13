using UnityEditor;
using UnityEngine;

namespace TeekayUtils.DevConsole.EditorTools
{
    /// <summary>
    /// Single styling point for all DevConsole editor-tool console output. Wraps a message in a
    /// bold colored "[DevConsoleConfig]" badge plus a severity-based status icon and color, then routes
    /// it to the matching Unity log sink. Generators keep emitting plain-text messages; the badge
    /// and rich-text wrapper are added here at log time.
    ///
    /// Logging is deferred across domain reloads: generators write code and call AssetDatabase
    /// .Refresh(), which triggers a script recompile + domain reload that would otherwise wipe a
    /// log emitted moments earlier. Each entry is parked in <see cref="SessionState"/> (which
    /// survives the reload) and flushed once the editor is stable again — after the reload if one
    /// happens, or on the next editor tick if it doesn't.
    /// </summary>
    [InitializeOnLoad]
    public static class DevConsoleEditorLog
    {
        public enum Severity { Success, Update, Muted, Warning, Error }

        const string BADGE = "<b><color=#5C9EFF>[DevConsoleConfig]</color></b>";

        const string COLOR_SUCCESS = "#7FD97F";
        const string COLOR_UPDATE  = "#5CD0E0";
        const string COLOR_MUTED   = "#9E9E9E";
        const string COLOR_WARNING = "#E0C24C";
        const string COLOR_ERROR   = "#E06C6C";

        // SessionState survives domain reloads. Each record is one line: the first char is the
        // severity digit (enum has <10 values), the rest is the single-line message. Generator
        // messages never contain newlines, so a newline join is a safe record separator.
        const string QUEUE_KEY = "DevConsole.EditorLog.Queue";

        // Guards against stacking duplicate delayCall registrations while we poll for the editor
        // to go idle. Resets to false on every domain reload (static-field default).
        static bool s_flushQueued;

        // Runs after every domain reload (including the one a generation triggers). Flush there so
        // entries parked just before the recompile reappear once the console is alive again.
        static DevConsoleEditorLog()
        {
            ScheduleFlush();
        }

        public static void Log(Severity severity, string message)
        {
            Enqueue(severity, message);
            // If no recompile follows, the static ctor won't fire again — flush on the next tick.
            ScheduleFlush();
        }

        static void ScheduleFlush()
        {
            if (s_flushQueued) return;
            s_flushQueued = true;
            EditorApplication.delayCall += Flush;
        }

        static void Enqueue(Severity severity, string message)
        {
            string oneLine = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            string record = (int)severity + oneLine;

            string existing = SessionState.GetString(QUEUE_KEY, string.Empty);
            string updated = string.IsNullOrEmpty(existing) ? record : existing + "\n" + record;
            SessionState.SetString(QUEUE_KEY, updated);
        }

        static void Flush()
        {
            s_flushQueued = false;

            // A pending recompile/asset import will reload the domain and wipe the console moments
            // from now — logging here would lose the message. Wait until the editor is idle; after
            // a reload the static ctor reschedules us, and on a compile error (no reload) this poll
            // resolves once isCompiling clears.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleFlush();
                return;
            }

            string queued = SessionState.GetString(QUEUE_KEY, string.Empty);
            if (string.IsNullOrEmpty(queued)) return;

            // Clear first so a reload mid-flush can't replay the same entries.
            SessionState.EraseString(QUEUE_KEY);

            string[] records = queued.Split('\n');
            foreach (string record in records)
            {
                if (string.IsNullOrEmpty(record)) continue;
                if (!int.TryParse(record.Substring(0, 1), out int severityValue)) continue;
                Emit((Severity)severityValue, record.Substring(1));
            }
        }

        static void Emit(Severity severity, string message)
        {
            string color = ColorFor(severity);
            string icon  = IconFor(severity);
            string styled = $"{BADGE} <color={color}>{icon} {message}</color>";

            switch (severity)
            {
                case Severity.Error:   Debug.LogError(styled);   break;
                case Severity.Warning: Debug.LogWarning(styled); break;
                default:               Debug.Log(styled);        break;
            }
        }

        static string ColorFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Success: return COLOR_SUCCESS;
                case Severity.Update:  return COLOR_UPDATE;
                case Severity.Warning: return COLOR_WARNING;
                case Severity.Error:   return COLOR_ERROR;
                default:               return COLOR_MUTED;
            }
        }

        static string IconFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Success: return "✔";
                case Severity.Update:  return "↻";
                case Severity.Warning: return "⚠";
                case Severity.Error:   return "✖";
                default:               return "=";
            }
        }
    }
}
