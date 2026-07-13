using System;
using System.Collections.Generic;

namespace TeekayUtils
{
    public static class CollectionExtensions
    {
        /// <summary>True if the list is null or has no elements (no allocation).</summary>
        public static bool IsNullOrEmpty<T>(this IList<T> list)
        {
            return list == null || list.Count == 0;
        }

        /// <summary>Swaps the elements at the two indices.</summary>
        public static void Swap<T>(this IList<T> list, int indexA, int indexB)
        {
            (list[indexA], list[indexB]) = (list[indexB], list[indexA]);
        }

        /// <summary>
        /// Shuffles the list in place (Fisher-Yates) using UnityEngine.Random,
        /// and returns it for chaining.
        /// </summary>
        public static IList<T> Shuffle<T>(this IList<T> list)
        {
            int count = list.Count;
            while (count > 1)
            {
                --count;
                int index = UnityEngine.Random.Range(0, count + 1);
                (list[index], list[count]) = (list[count], list[index]);
            }

            return list;
        }

        /// <summary>Performs an action on each element in the sequence.</summary>
        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (var item in sequence)
            {
                action(item);
            }
        }

        /// <summary>
        /// Returns a uniformly random element. O(1) for lists; reservoir sampling
        /// for lazy sequences. Throws on empty sequences.
        /// </summary>
        public static T Random<T>(this IEnumerable<T> sequence)
        {
            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            if (sequence is IList<T> list)
            {
                if (list.Count == 0)
                {
                    throw new InvalidOperationException("Cannot get a random element from an empty collection.");
                }

                return list[UnityEngine.Random.Range(0, list.Count)];
            }

            using var enumerator = sequence.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException("Cannot get a random element from an empty collection.");
            }

            T result = enumerator.Current;
            int count = 1;
            while (enumerator.MoveNext())
            {
                if (UnityEngine.Random.Range(0, ++count) == 0)
                {
                    result = enumerator.Current;
                }
            }

            return result;
        }
    }
}
