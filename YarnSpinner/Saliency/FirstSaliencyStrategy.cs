using System.Collections.Generic;
using System.Linq;

namespace Yarn.Saliency
{
    /// <summary>
    /// A content saliency strategy that always returns the first item in the
    /// list of available options.
    /// </summary>
    /// <remarks>
    /// This saliency strategy is used when a <see cref="Dialogue"/> has no
    /// provided saliency strategy, but is required to make a decision.
    /// </remarks>
    class FirstSaliencyStrategy : IContentSaliencyStrategy
    {
        /// <inheritdoc/>
        public TContent ChooseBestContent<TContent>(IEnumerable<TContent> options) where TContent : IContentSaliencyOption
        {
            // Returns the first item in the list.
            return options.First();
        }
    }
}
