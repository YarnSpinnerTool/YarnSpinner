using System.Collections.Generic;
using System.Linq;

namespace Yarn.Saliency
{
    /// <summary>
    /// A content saliency strategy that always returns the first non-failing
    /// item in the list of available options.
    /// </summary>
    /// <remarks>
    /// This saliency strategy is used when a <see cref="Dialogue"/> has no
    /// provided saliency strategy, but is required to make a decision.
    /// </remarks>
    public class FirstSaliencyStrategy : IContentSaliencyStrategy
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
            return content
                .Where(c => c.FailingConditionValueCount == 0)
                .FirstOrDefault();
        }
    }
}
