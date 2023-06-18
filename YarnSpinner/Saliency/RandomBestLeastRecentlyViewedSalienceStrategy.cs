using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Saliency
{
    /// <summary>
    /// A content saliency strategy that returns a random choice of the best,
    /// least-recently seen choices from the provided options.
    /// </summary>
    /// <remarks>
    /// This strategy stores information about the number of times each piece of
    /// content has been seen in the provided <see cref="VariableStorage"/>.
    /// </remarks>
    class RandomBestLeastRecentlyViewedSalienceStrategy : IContentSaliencyStrategy
    {
        /// <summary>
        /// Gets the variable storage to use for storing information about how
        /// often we've seen content.
        /// </summary>
        private IVariableStorage VariableStorage { get; }

        public RandomBestLeastRecentlyViewedSalienceStrategy(IVariableStorage storage)
        {
            this.VariableStorage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Gets a unique variable name that can be used for tracking the view
        /// count of a specific piece of content.
        /// </summary>
        /// <param name="content">The content to generate a variable name
        /// for.</param>
        /// <returns>The generated variable name.</returns>
        private string GetViewCountKeyForContent(IContentSaliencyOption content)
        {
            return $"$Yarn.Internal.Content.ViewCount.{content.ContentID}";
        }

        /// <inheritdoc/>
        public TContent ChooseBestContent<TContent>(IEnumerable<TContent> options) where TContent : IContentSaliencyOption
        {
            // For each of the options, calculate how many times we've seen it,
            // and what variable name stores this information.
            var viewCountContent = options.Select(c =>
            {
                int count;
                if (c.ContentID == null)
                {
                    // This content represents the null choice. We won't have a
                    // view count key for this, so create a synthetic value
                    // where the view count is the highest possible (to push it
                    // to the bottom of the sorted list, since we want to avoid
                    // this option as much as we can).
                    return (ViewCount: int.MaxValue, ViewCountKey: null, Content: c);
                }
                else
                {
                    // Query the variable storage for how many times we've seen
                    // content with this ID. If we don't have an answer, then
                    // assume zero. Additionally, we'll keep the view count key
                    // for later, since we'll increment it when we're done.
                    string viewCountKey = GetViewCountKeyForContent(c);
                    if (this.VariableStorage.TryGetValue<float>(viewCountKey, out var countAsFloat))
                    {
                        countAsFloat = 0;
                    }
                    count = (int)countAsFloat;
                    return (ViewCount: count, ViewCountKey: viewCountKey, Content: c);
                }

            });

            // Get the group with the least views, and then from there get the
            // group with the best score. Choose a random item from this final
            // group.
            var (ViewCount, ViewCountKey, Content) = viewCountContent
                .GroupBy(c => c.ViewCount)
                .OrderBy(c => c.Key)
                .First()
                .GroupBy(c => c.Content.ConditionValueCount)
                .OrderByDescending(c => c.Key)
                .First()
                .RandomElement();

            if (ViewCountKey != null)
            {
                int bestViewCount = ViewCount + 1;
                VariableStorage.SetValue(ViewCountKey, bestViewCount);
            }

            return Content;
        }
    }

    /// <summary>
    /// Contains extension methods for <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static class EnumerableRandomExtension {
        static readonly Random random = new Random();

        /// <summary>
        /// Returns a random element from <paramref name="enumerable"/>.
        /// </summary>
        /// <remarks>
        /// This method uses System.Random to make a selection, which is
        /// cryptographically insecure. This means that this method should not
        /// be used for security-critical decisions.
        /// </remarks>
        /// <typeparam name="T">The type of element in <paramref
        /// name="enumerable"/>.</typeparam>
        /// <param name="enumerable">The collection to choose an item
        /// from.</param>
        /// <returns>A random element in <paramref
        /// name="enumerable"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref
        /// name="enumerable"/> contains no items.</exception>
        public static T RandomElement<T>(this IEnumerable<T> enumerable)
        {
            var count = enumerable.Count();
            if (count <= 0) {
                throw new ArgumentException($"Sequence is empty");
            }
#pragma warning disable CA5394 // System.Random is insecure
            var selection = random.Next(count);
#pragma warning restore 5394
            return enumerable.ElementAt(selection);
        }
    }
}
