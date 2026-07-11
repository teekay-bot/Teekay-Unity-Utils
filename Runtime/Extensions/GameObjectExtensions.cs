using System.Linq;
using UnityEngine;

namespace TeekayUtils
{
    public static class GameObjectExtensions
    {
        /// <summary>Hides the GameObject in the Hierarchy view.</summary>
        public static void HideInHierarchy(this GameObject gameObject)
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        /// <summary>Gets the component of type T, adding one if it does not exist.</summary>
        public static T GetOrAdd<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (!component) component = gameObject.AddComponent<T>();

            return component;
        }

        /// <summary>
        /// Converts Unity's fake-null (destroyed object) into a real null so the
        /// result can be used with ?. and ?? operators.
        /// </summary>
        public static T OrNull<T>(this T obj) where T : Object => obj ? obj : null;

        /// <summary>Destroys all children of the GameObject (deferred).</summary>
        public static void DestroyChildren(this GameObject gameObject)
        {
            gameObject.transform.DestroyChildren();
        }

        /// <summary>Immediately destroys all children of the GameObject.</summary>
        public static void DestroyChildrenImmediate(this GameObject gameObject)
        {
            gameObject.transform.DestroyChildrenImmediate();
        }

        /// <summary>Activates all child GameObjects.</summary>
        public static void EnableChildren(this GameObject gameObject)
        {
            gameObject.transform.EnableChildren();
        }

        /// <summary>Deactivates all child GameObjects.</summary>
        public static void DisableChildren(this GameObject gameObject)
        {
            gameObject.transform.DisableChildren();
        }

        /// <summary>Resets the transform's local position, rotation and scale.</summary>
        public static void ResetTransformation(this GameObject gameObject)
        {
            gameObject.transform.Reset();
        }

        /// <summary>
        /// Hierarchy path of the GameObject's PARENT, e.g. "/Root/Enemies".
        /// Returns "/" for root objects. Works on inactive objects.
        /// </summary>
        public static string Path(this GameObject gameObject)
        {
            Transform parent = gameObject.transform.parent;
            return parent == null ? "/" : parent.gameObject.PathFull();
        }

        /// <summary>
        /// Full hierarchy path including the GameObject itself,
        /// e.g. "/Root/Enemies/Goblin". Works on inactive objects.
        /// </summary>
        public static string PathFull(this GameObject gameObject)
        {
            return "/" + string.Join("/",
                gameObject.GetComponentsInParent<Transform>(true).Select(t => t.name).Reverse());
        }

        /// <summary>Sets the layer on the GameObject and all of its descendants.</summary>
        public static void SetLayersRecursively(this GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            gameObject.transform.ForEveryChild(child => child.gameObject.SetLayersRecursively(layer));
        }

        /// <summary>True if the GameObject's layer is contained in the given mask.</summary>
        public static bool IsInLayerMask(this GameObject gameObject, LayerMask mask)
        {
            return mask.Contains(gameObject.layer);
        }
    }
}
