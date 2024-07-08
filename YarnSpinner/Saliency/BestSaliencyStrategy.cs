using System.Collections.Generic;
using System.Linq;

namespace Yarn.Saliency
{
    /// <summary>
    /// A content saliency strategy that returns the best of the provided
    /// options.
    /// </summary>
    /// <remarks>
    /// This strategy always selects the single best of the available items,
    /// regardless of how many times it has been seen before. For a saliency
    /// strategy that takes into account how recently content has been seen, see
    /// <see cref="BestLeastRecentlyViewedSalienceStrategy"/>.
    /// </remarks>
    public class BestSaliencyStrategy : IContentSaliencyStrategy
    {
        /// <inheritdoc/>
        public void ContentWasSelected(ContentSaliencyOption content)
        {
            // This strategy does not need need to track any state, so this
            // method takes no action.
        }

        /// <inheritdoc/>
        public ContentSaliencyOption? QueryBestContent(IEnumerable<ContentSaliencyOption> content)
        {
            // Filter out any content that has a failing condition, and select
            // the one that has the highest complexity.
            return content
                .Where(o => o.FailingConditionValueCount == 0)
                .OrderByDescending(o => o.ComplexityScore)
                .First();
        }

        
    }
}
