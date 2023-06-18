namespace Yarn.Saliency
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains methods for choosing a piece of content from a collection of
    /// options.
    /// </summary>
    public interface IContentSaliencyStrategy {
        /// <summary>
        /// Chooses an item from content that is the most appropriate (or
        /// <i>salient</i>) for the user's current context.
        /// </summary>
        /// <param name="content">A collection of content items.</param>
        /// <returns>An item from content that should be displayed to the user.</returns>
        TContent ChooseBestContent<TContent>(IEnumerable<TContent> content) where TContent : IContentSaliencyOption;
    }
}
