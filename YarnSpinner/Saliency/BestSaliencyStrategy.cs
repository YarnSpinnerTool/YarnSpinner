using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Saliency
{
    /// <summary>
    /// A content saliency strategy that returns the best of the provided
    /// options.
    /// </summary>
    /// <remarks>
    /// This strategy always returns the single best of the available items,
    /// regardless of whether it has been seen before. For a saliency strategy
    /// that takes into account how recently content has been seen, see <see
    /// cref="BestLeastRecentlyViewedSalienceStrategy"/>.
    /// </remarks>
    public class BestSaliencyStrategy : IContentSaliencyStrategy
    {
        /// <inheritdoc/>
        public TContent ChooseBestContent<TContent>(IEnumerable<TContent> options) where TContent : IContentSaliencyOption
        {
            return options.OrderByDescending(o => o.ConditionValueCount).First();
        }
    }
}
