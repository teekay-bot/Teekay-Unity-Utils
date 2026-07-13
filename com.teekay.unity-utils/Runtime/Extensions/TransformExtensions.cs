using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeekayUtils
{
    public static class TransformExtensions
    {
        /// <summary>
        /// True if the target is within <paramref name="maxDistance"/> and inside the
        /// horizontal FOV cone of <paramref name="maxAngle"/> degrees.
        /// NOTE: the direction is flattened onto the XZ plane (y is ignored) —
        /// intended for ground-based range/FOV checks.
        /// </summary>
        public static bool InRangeOf(this Transform source, Transform target, float maxDistance, float maxAngle = 360f)
        {
            Vector3 directionToTarget = (target.position - source.position).With(y: 0);
            return directionToTarget.sqrMagnitude <= maxDistance * maxDistance
                && Vector3.Angle(source.forward, directionToTarget) <= maxAngle * 0.5f;
        }

        /// <summary>All direct children as an IEnumerable, for LINQ.</summary>
        public static IEnumerable<Transform> Children(this Transform parent)
        {
            foreach (Transform child in parent)
            {
                yield return child;
            }
        }

        /// <summary>Resets localPosition, localRotation and localScale to defaults.</summary>
        public static void Reset(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>Destroys all child GameObjects (deferred, end of frame).</summary>
        public static void DestroyChildren(this Transform parent)
        {
            parent.ForEveryChild(child => Object.Destroy(child.gameObject));
        }

        /// <summary>Immediately destroys all child GameObjects (editor-safe).</summary>
        public static void DestroyChildrenImmediate(this Transform parent)
        {
            parent.ForEveryChild(child => Object.DestroyImmediate(child.gameObject));
        }

        /// <summary>Activates all child GameObjects.</summary>
        public static void EnableChildren(this Transform parent)
        {
            parent.ForEveryChild(child => child.gameObject.SetActive(true));
        }

        /// <summary>Deactivates all child GameObjects.</summary>
        public static void DisableChildren(this Transform parent)
        {
            parent.ForEveryChild(child => child.gameObject.SetActive(false));
        }

        /// <summary>
        /// Runs an action on every direct child, iterating in reverse so the
        /// action may safely destroy or reparent children.
        /// </summary>
        public static void ForEveryChild(this Transform parent, Action<Transform> action)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                action(parent.GetChild(i));
            }
        }
    }
}
