using System;
using System.Collections.Generic;
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
        /// <see cref="GetSelectable"/> minus every type that will not exist in a player build. This is
        /// what the dropdown offers for a field on an object that itself ships.
        /// </summary>
        /// <remarks>
        /// A test double satisfies every rule <see cref="IsSelectable"/> knows, so nothing there can
        /// keep it out — and picking one writes <c>asm: Something.Tests</c> into the scene, an assembly
        /// no player build contains, so the field reads back null in the build and nowhere else. That
        /// happened: a fixture sat beside the real implementations for a few hours on 2026-08-11, and
        /// the only thing that had been keeping it out was the author remembering to give it a
        /// non-public constructor.
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
                if (!ShipsInBuild(types[i])) types.RemoveAt(i);
            }

            return types;
        }

        /// <summary>
        /// Whether <paramref name="type"/> is compiled into an assembly that a player build contains.
        /// False for test assemblies and for editor-only ones alike.
        /// </summary>
        /// <remarks>
        /// ⚠️ This asks the build question directly rather than trying to recognise a test assembly by
        /// its references, and the difference was measured on 2026-08-19 rather than reasoned: a rule
        /// of "references UnityEngine.TestRunner or nunit" classifies almost everything as a test
        /// assembly, because Unity auto-references the TestRunner into EVERY editor assembly and
        /// <c>nunit.framework.dll</c> is an auto-referenced precompiled assembly present on 159 of 326
        /// compile lines in one ordinary project — runtime assemblies included. Shipping that rule
        /// emptied a working type list.
        /// <para>
        /// The honest cost of asking the build question: an EDITOR-only implementor is excluded too,
        /// which is right for a field on a component and wrong for one on an editor-only object. The
        /// drawer resolves that where the information exists — it filters only when the object being
        /// edited is itself something a build contains.
        /// </para>
        /// </remarks>
        public static bool ShipsInBuild(Type type)
        {
            if (type == null) return false;

            // Cached because a dropdown opens repeatedly and this walks the compilation graph. A static
            // field is the right lifetime: changing an asmdef reloads the domain, which clears it.
            s_playerAssemblyNames ??= CollectPlayerAssemblyNames();
            return s_playerAssemblyNames.Contains(type.Assembly.GetName().Name);
        }

        static HashSet<string> s_playerAssemblyNames;

        static HashSet<string> CollectPlayerAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            // WithoutTestAssemblies, so a PlayMode suite does not count as shipping either.
            foreach (CompilationAssembly assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                names.Add(assembly.name);
            }

            return names;
        }

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
