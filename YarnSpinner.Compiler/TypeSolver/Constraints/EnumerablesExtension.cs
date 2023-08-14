#define DISALLOW_NULL_EQUATION_TERMS

using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{
    public static class EnumerablesExtension
    {
        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>
                (this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct =
                new[] { Enumerable.Empty<T>() };
            
            return sequences.Aggregate(
                    emptyProduct,
                    (accumulator, sequence) => 
                        accumulator.SelectMany(accumulatedSequence => 
                            sequence.Select(item => 
                                accumulatedSequence.Concat(new[] { item })
                            )
                        )
                    );
        }

        /// <summary>
        /// Returns an enumerator containing all items of <paramref
        /// name="sequence"/> that are not null.
        /// </summary>
        /// <typeparam name="T">The type of item in <paramref
        /// name="sequence"/>.</typeparam>
        /// <param name="sequence">The sequence.</param>
        /// <returns>A sequence containing non-null elements of <paramref
        /// name="sequence"/>.</returns>
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> sequence) where T : class {
            foreach (var item in sequence) {
                if (item != null) {
                    yield return item;
                }
            }
        }
    }

}
