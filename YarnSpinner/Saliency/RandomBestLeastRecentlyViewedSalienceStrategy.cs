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
    public class RandomBestLeastRecentlyViewedSalienceStrategy : IContentSaliencyStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="RandomBestLeastRecentlyViewedSalienceStrategy"/> class.
        /// </summary>
        /// <param name="storage">The variable storage to use when determining
        /// which content to show.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provided
        /// <paramref name="storage"/> argument is null.</exception>
        public RandomBestLeastRecentlyViewedSalienceStrategy(IVariableStorage storage)
        {
            this.VariableStorage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Gets the variable storage to use for storing information about how
        /// often we've seen content.
        /// </summary>
        private IVariableStorage VariableStorage { get; }

        /// <inheritdoc/>
        /// <remarks>This method increments the view count for <paramref
        /// name="content"/>, so that the next time QueryBestContent is run, it
        /// has an updated count of the number of times the content has been
        /// viewed.</remarks>
        public void ContentWasSelected(ContentSaliencyOption content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content), "Content cannot be null");
            }

            if (VariableStorage.TryGetValue<float>(content.ViewCountKey, out var ViewCount) == false)
            {
                ViewCount = 0;
            }

            ViewCount += 1;
            VariableStorage.SetValue(content.ViewCountKey, (int)ViewCount);
        }

        /// <inheritdoc/>
        public ContentSaliencyOption? QueryBestContent(IEnumerable<ContentSaliencyOption> content)
        {
            // First, filter out all content that has failed any conditions.
            content = content.Where(c => c.FailingConditionValueCount == 0).ToList();

            if (!content.Any())
            {
                // There's no content available.
                return null;
            }

            // For each of the options, calculate how many times we've seen it.
            var viewCountContent = content.Select(c =>
            {
                // Query the variable storage for how many times we've seen
                // content with this ID. If we don't have an answer, then assume
                // zero.
                string viewCountKey = c.ViewCountKey;

                if (!this.VariableStorage.TryGetValue<float>(viewCountKey, out var countAsFloat))
                {
                    countAsFloat = 0;
                }
                int count = (int)countAsFloat;
                return (ViewCount: count, Content: c);
            });

            // Get the group with the least views, and then from there get the
            // group with the best score. Choose a random item from this final
            // group.
            var (ViewCount, Content) = viewCountContent
                // Find the group of content items that have the least number of
                // views.
                .GroupBy(c => c.ViewCount)
                .OrderBy(c => c.Key)
                .First()

                // Now find the subgroup where the items have the highest
                // complexity score.
                .GroupBy(c => c.Content.ComplexityScore)
                .OrderByDescending(c => c.Key)
                .First()

                // Finally, pick a random element in that subgroup.
                .RandomElement();

            return Content;
        }
    }

    /// <summary>
    /// Contains extension methods for <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static class EnumerableRandomExtension
    {
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
            if (count <= 0)
            {
                throw new ArgumentException($"Sequence is empty");
            }
            if (count == 1)
            {
                return enumerable.Single();
            }
#pragma warning disable CA5394 // System.Random is insecure
            var selection = random.Next(count);
#pragma warning restore 5394
            return enumerable.ElementAt(selection);
        }
    }
}
