using UnityEngine;

namespace TeekayUtils
{
    /// <summary>
    /// Marker attribute for a <c>[SerializeReference]</c> field: adds a type dropdown listing every
    /// concrete type assignable to the field, so which implementation to use is an authoring choice
    /// rather than a code one. The chosen instance's own serialized fields are drawn underneath.
    /// <para>
    /// Unity serializes managed references but ships no type picker for them, so without a drawer a
    /// <c>[SerializeReference]</c> field can only ever be assigned from code.
    /// </para>
    /// <example>
    /// <code>
    /// [SerializeReference, SubclassSelector] IDamageModifier _modifier = new FlatDamage();
    /// </code>
    /// </example>
    /// A type appears in the dropdown when it is concrete, is not a <see cref="Object"/>, carries
    /// <c>[Serializable]</c> and has a public parameterless constructor — the same conditions Unity
    /// places on a managed reference value. The drawer lives in the editor assembly, so this costs a
    /// build nothing.
    /// </summary>
    public sealed class SubclassSelectorAttribute : PropertyAttribute { }
}
