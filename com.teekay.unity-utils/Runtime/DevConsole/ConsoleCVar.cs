using System;
using System.Globalization;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Base class for typed console variables (CVars). A CVar binds a console name to a
    /// pair of getter/setter delegates, so the underlying value can live anywhere — a field,
    /// a ScriptableObject property, a static, etc. Subclasses handle parsing the user's input
    /// string into the right type.
    /// </summary>
    public abstract class ConsoleCVar
    {
        public readonly string Name;
        public readonly string Description;
        /// <summary>Human-readable type name shown in help (e.g., "float", "bool"). For UI/docs only.</summary>
        public abstract string TypeName { get; }
        /// <summary>Argument hint for the help command, e.g. "true/false" or "&lt;float&gt;".
        /// Defaults to "&lt;typeName&gt;"; bool/string/etc. override for friendlier output.</summary>
        public virtual string UsageHint => $"<{TypeName}>";
        /// <summary>If true, console edits to this CVar are NOT restored on shutdown — value persists
        /// across play sessions in editor. Set via the RegisterFloatPersistent / RegisterBoolPersistent /
        /// etc. overloads.</summary>
        public bool IsPersistent { get; internal set; }

        protected ConsoleCVar(string name, string description)
        {
            Name = name;
            Description = description ?? string.Empty;
        }

        /// <summary>Current value formatted for display (using invariant culture).</summary>
        public abstract string GetValueAsString();

        /// <summary>
        /// Try to set the value from a user-typed string. Returns false and writes a reason
        /// into <paramref name="error"/> on failure. Setter exceptions are caught and reported.
        /// </summary>
        public abstract bool TrySetFromString(string value, out string error);

        /// <summary>Capture the current value so it can be restored later (called at register time).</summary>
        public abstract void Snapshot();

        /// <summary>Restore the value captured by <see cref="Snapshot"/>. Setter exceptions are swallowed.</summary>
        public abstract void RestoreSnapshot();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Typed concrete CVars
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class FloatCVar : ConsoleCVar
    {
        public override string TypeName => "float";
        readonly Func<float> _get;
        readonly Action<float> _set;
        float _snapshot;

        public FloatCVar(string name, string description, Func<float> get, Action<float> set)
            : base(name, description)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public override string GetValueAsString() => _get().ToString("0.######", CultureInfo.InvariantCulture);

        public override bool TrySetFromString(string value, out string error)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                error = $"'{value}' is not a float";
                return false;
            }
            try { _set(v); error = null; return true; }
            catch (Exception e) { error = e.Message; return false; }
        }

        public override void Snapshot() => _snapshot = _get();
        public override void RestoreSnapshot() { try { _set(_snapshot); } catch { /* swallowed */ } }
    }

    public sealed class IntCVar : ConsoleCVar
    {
        public override string TypeName => "int";
        readonly Func<int> _get;
        readonly Action<int> _set;
        int _snapshot;

        public IntCVar(string name, string description, Func<int> get, Action<int> set)
            : base(name, description)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public override string GetValueAsString() => _get().ToString(CultureInfo.InvariantCulture);

        public override bool TrySetFromString(string value, out string error)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                error = $"'{value}' is not an int";
                return false;
            }
            try { _set(v); error = null; return true; }
            catch (Exception e) { error = e.Message; return false; }
        }

        public override void Snapshot() => _snapshot = _get();
        public override void RestoreSnapshot() { try { _set(_snapshot); } catch { /* swallowed */ } }
    }

    public sealed class BoolCVar : ConsoleCVar
    {
        public override string TypeName => "bool";
        public override string UsageHint => "true/false";
        readonly Func<bool> _get;
        readonly Action<bool> _set;
        bool _snapshot;

        public BoolCVar(string name, string description, Func<bool> get, Action<bool> set)
            : base(name, description)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public override string GetValueAsString() => _get() ? "true" : "false";

        public override bool TrySetFromString(string value, out string error)
        {
            bool? parsed = value?.ToLowerInvariant() switch
            {
                "1" or "true"  or "on"  or "yes" => true,
                "0" or "false" or "off" or "no"  => false,
                _ => null
            };
            if (parsed == null) { error = $"'{value}' is not a bool"; return false; }
            try { _set(parsed.Value); error = null; return true; }
            catch (Exception e) { error = e.Message; return false; }
        }

        public override void Snapshot() => _snapshot = _get();
        public override void RestoreSnapshot() { try { _set(_snapshot); } catch { /* swallowed */ } }
    }

    public sealed class StringCVar : ConsoleCVar
    {
        public override string TypeName => "string";
        readonly Func<string> _get;
        readonly Action<string> _set;
        string _snapshot;

        public StringCVar(string name, string description, Func<string> get, Action<string> set)
            : base(name, description)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public override string GetValueAsString() => _get() ?? string.Empty;

        public override bool TrySetFromString(string value, out string error)
        {
            try { _set(value); error = null; return true; }
            catch (Exception e) { error = e.Message; return false; }
        }

        public override void Snapshot() => _snapshot = _get();
        public override void RestoreSnapshot() { try { _set(_snapshot); } catch { /* swallowed */ } }
    }
}
