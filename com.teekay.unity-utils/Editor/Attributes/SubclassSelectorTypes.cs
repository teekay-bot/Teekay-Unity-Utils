using System;
using System.Collections.Generic;
using UnityEditor;

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
