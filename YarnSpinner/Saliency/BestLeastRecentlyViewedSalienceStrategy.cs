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
        /// Gets the variable storage to use for storing information about how
        /// often we've seen content.
        /// </summary>
        private IVariableStorage VariableStorage { get; }

        /// <summary>
        /// Initalises a new instance of the <see
        /// cref="BestLeastRecentlyViewedSalienceStrategy"/> class.
        /// </summary>
        /// <param name="storage">The variable storage to use when determining
        /// which content to show.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public BestLeastRecentlyViewedSalienceStrategy(IVariableStorage storage)
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
        public TContent ChooseBestContent<TContent>(IEnumerable<TContent> content) where TContent : IContentSaliencyOption
        {
            // For each of the options, calculate how many times we've seen it,
            // and what variable name stores this information.
            var viewCountContent = content.Select(c =>
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
                    return (ViewCount: count, ViewCountKey: viewCountKey ?? null, Content: c);
                }

            });

            // Sort by view count, then by descending condition value count, to
            // get the best of the least-recently-seen items. OrderBy and
            // ThenByDescending perform a stable sort, which is helpful for
            // writers who might want a specific order of delivery when this
            // collection of options is run multiple times.
            var (ViewCount, ViewCountKey, Content) = viewCountContent
                .OrderBy(c => c.ViewCount)
                .ThenByDescending(c => c.Content.ConditionValueCount)
                .First();

            if (ViewCountKey != null)
            {
                int bestViewCount = ViewCount + 1;
                VariableStorage.SetValue(ViewCountKey, bestViewCount);
            }

            return Content;
        }
    }
}
