using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Saliency
{
    /// <summary>
    /// A content saliency strategy that returns the first of the best,
    /// least-recently seen choices from the provided options.
    /// </summary>
    /// <remarks>
    /// This strategy stores information about the number of times each piece of
    /// content has been seen in the provided <see cref="VariableStorage"/>.
    /// </remarks>
    public class BestLeastRecentlyViewedSalienceStrategy : IContentSaliencyStrategy
    {
        /// <summary>
        /// Initalises a new instance of the <see
        /// cref="BestLeastRecentlyViewedSalienceStrategy"/> class.
        /// </summary>
        /// <param name="storage">The variable storage to use when determining
        /// which content to show.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provided
        /// <paramref name="storage"/> argument is null.</exception>
        public BestLeastRecentlyViewedSalienceStrategy(IVariableStorage storage)
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
                if (this.VariableStorage.TryGetValue<float>(c.ViewCountKey, out var countAsFloat) == false)
                {
                    countAsFloat = 0;
                }

                int count = (int)countAsFloat;
                return (ViewCount: count, Content: c);
            });

            // Sort by view count, then by descending complexity score, to
            // get the best of the least-recently-seen items. OrderBy and
            // ThenByDescending perform a stable sort, which is helpful for
            // writers who might want a specific order of delivery when this
            // collection of options is run multiple times.
            var (ViewCount, Content) = viewCountContent
                .OrderBy(c => c.ViewCount)
                .ThenByDescending(c => c.Content.ComplexityScore)
                .First();

            return Content;
        }
    }
}
