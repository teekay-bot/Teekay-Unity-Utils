using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace TeekayUtils.EditorTools
{
    /// <summary>
    /// The type discovery and naming behind <see cref="SubclassSelectorDrawer"/>, kept apart from the
    /// drawer so it can be unit-tested without IMGUI.
    /// </summary>
    public static class SubclassSelectorTypes
    {
        /// <summary>
        /// Resolves the type a managed-reference field was declared as, from
        /// <c>SerializedProperty.managedReferenceFieldTypename</c>. Unity formats that as
        /// <c>"&lt;assembly&gt; &lt;namespace-qualified type&gt;"</c> — space separated, which is not
        /// the assembly-qualified form <see cref="Type.GetType(string)"/> expects.
        /// Returns null for anything malformed or no longer loadable.
        /// </summary>
        public static Type ResolveFieldType(string managedReferenceFieldTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceFieldTypename)) return null;

            int split = managedReferenceFieldTypename.IndexOf(' ');
            if (split <= 0 || split == managedReferenceFieldTypename.Length - 1) return null;

            string assembly = managedReferenceFieldTypename.Substring(0, split);
            string typeName = managedReferenceFieldTypename.Substring(split + 1);
            return Type.GetType($"{typeName}, {assembly}");
        }

        /// <summary>
        /// Whether Unity can store an instance of <paramref name="type"/> in a managed-reference
        /// field. Offering a type that fails any of these would produce a field that silently
        /// refuses to serialize, so the dropdown filters by exactly the same conditions.
        /// </summary>
        public static bool IsSelectable(Type type)
        {
            return type != null
                && !type.IsAbstract
                && !type.IsInterface
                && !type.IsValueType
                && !type.IsGenericTypeDefinition
                && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && Attribute.IsDefined(type, typeof(SerializableAttribute), inherit: false)
                && type.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>
        /// Every selectable type assignable to <paramref name="fieldType"/>, sorted by name so menu
        /// order does not drift with assembly load order.
        /// </summary>
        public static List<Type> GetSelectable(Type fieldType)
        {
            var results = new List<Type>();
            if (fieldType == null) return results;

            // TypeCache excludes the queried type itself, which matters when a field is declared as
            // a concrete base class rather than an interface — that base is a valid choice too.
            if (IsSelectable(fieldType)) results.Add(fieldType);

            foreach (Type type in TypeCache.GetTypesDerivedFrom(fieldType))
            {
                if (IsSelectable(type)) results.Add(type);
            }

            results.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return results;
        }

        /// <summary>
        /// Every shippable type assignable to <paramref name="fieldType"/> — <see cref="GetSelectable"/>
        /// minus anything declared in a test assembly. This is what the dropdown offers.
        /// </summary>
        /// <remarks>
        /// A test double is storable by every rule <see cref="IsSelectable"/> knows, so nothing there
        /// can keep it out of the menu — and picking one writes <c>asm: Something.Tests</c> into the
        /// scene, an assembly no player build contains, so the field reads back null in the build and
        /// nowhere else. That happened: a fixture sat beside the real implementations for a few hours
        /// on 2026-08-11, and the only thing that had been keeping it out was the author remembering
        /// to give it a non-public constructor.
        /// <para>
        /// <see cref="GetSelectable"/> stays unfiltered on purpose. It answers "what can Unity store",
        /// which is a property of the type alone, and this package's own tests declare their fixtures
        /// in a test assembly — filtering there would leave the function untestable by construction.
        /// </para>
        /// </remarks>
        public static List<Type> GetShippable(Type fieldType)
        {
            List<Type> types = GetSelectable(fieldType);

            for (int i = types.Count - 1; i >= 0; i--)
            {
                if (IsFromTestAssembly(types[i])) types.RemoveAt(i);
            }

            return types;
        }

        /// <summary>
        /// Whether <paramref name="type"/> is declared in a test assembly, i.e. one that references
        /// the test runner or NUnit.
        /// </summary>
        /// <remarks>
        /// Asking "does it reference the test runner" rather than taking
        /// <c>AssembliesType.PlayerWithoutTestAssemblies</c> and treating the remainder as tests: that
        /// set also excludes every ordinary EDITOR assembly, so a <c>[SubclassSelector]</c> field on an
        /// editor-only object would lose all of its choices — a silent regression traded for four
        /// fewer lines. This rule removes what it means to remove.
        /// </remarks>
        public static bool IsFromTestAssembly(Type type)
        {
            if (type == null) return false;

            // Cached because a dropdown opens repeatedly and this walks the whole compilation graph.
            // A static field is the right lifetime: changing an asmdef reloads the domain, which
            // clears it.
            s_testAssemblyNames ??= CollectTestAssemblyNames();
            return s_testAssemblyNames.Contains(type.Assembly.GetName().Name);
        }

        static HashSet<string> s_testAssemblyNames;

        static HashSet<string> CollectTestAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            // Both sets: an EditMode suite is an editor assembly, a PlayMode suite is a player one.
            AddTestAssemblies(CompilationPipeline.GetAssemblies(AssembliesType.Editor), names);
            AddTestAssemblies(CompilationPipeline.GetAssemblies(AssembliesType.Player), names);

            return names;
        }

        static void AddTestAssemblies(CompilationAssembly[] assemblies, HashSet<string> names)
        {
            foreach (CompilationAssembly assembly in assemblies)
            {
                if (ReferencesTestRunner(assembly)) names.Add(assembly.name);
            }
        }

        static bool ReferencesTestRunner(CompilationAssembly assembly)
        {
            // Two lists, because the two halves of a test asmdef arrive by different routes: the
            // TestRunner assemblies are compiled from a package (assemblyReferences), while
            // nunit.framework.dll is a precompiled reference (compiledAssemblyReferences, full paths).
            foreach (CompilationAssembly reference in assembly.assemblyReferences)
            {
                if (IsTestRunnerName(reference.name)) return true;
            }

            foreach (string path in assembly.compiledAssemblyReferences)
            {
                if (IsTestRunnerName(Path.GetFileNameWithoutExtension(path))) return true;
            }

            return false;
        }

        static bool IsTestRunnerName(string name) =>
            name == "UnityEngine.TestRunner"
            || name == "UnityEditor.TestRunner"
            || name == "nunit.framework";

        /// <summary>
        /// Menu labels parallel to <paramref name="types"/>. Two implementations in different
        /// namespaces can share a short name, and GenericMenu silently merges entries whose labels
        /// collide — so a name that is not unique gets its namespace appended.
        /// </summary>
        public static string[] BuildMenuLabels(IReadOnlyList<Type> types)
        {
            if (types == null) return Array.Empty<string>();

            var nameCounts = new Dictionary<string, int>(types.Count);
            for (int i = 0; i < types.Count; i++)
            {
                nameCounts.TryGetValue(types[i].Name, out int count);
                nameCounts[types[i].Name] = count + 1;
            }

            var labels = new string[types.Count];
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                labels[i] = nameCounts[type.Name] > 1
                    ? $"{type.Name} ({type.Namespace})"
                    : type.Name;
            }

            return labels;
        }
    }
}
