using UnityEngine;

namespace TeekayUtils
{
    public static class LayerMaskExtensions
    {
        /// <summary>True if the given layer number is contained in the mask.</summary>
        public static bool Contains(this LayerMask mask, int layerNumber)
        {
            return mask == (mask | (1 << layerNumber));
        }
    }
}
