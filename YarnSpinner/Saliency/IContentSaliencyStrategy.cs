namespace Yarn.Saliency
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Contains methods for choosing a piece of content from a collection of
    /// options.
    /// </summary>
    public interface IContentSaliencyStrategy
    {
        /// <summary>
        /// Chooses an item from content that is the most appropriate (or
        /// <i>salient</i>) for the user's current context.
        /// </summary>
        /// <remarks>Implementations of this method should not modify any state
        /// - that is, they should be 'read-only' operations. If a strategy
        /// needs to record information about when a piece of content has been
        /// selected, it should do it in the <see cref="ContentWasSelected"/>
        /// method.</remarks>
        /// <param name="content">A collection of content items. This collection
        /// may be empty.</param>
        /// <returns>An item from <paramref name="content"/> that is the most
        /// appropriate for display, or <see langword="null"/> if no content
        /// should be displayed.</returns>
        ContentSaliencyOption? QueryBestContent(IEnumerable<ContentSaliencyOption> content);

        /// <summary>
        /// Called by Yarn Spinner to indicate that a piece of salient content
        /// has been selected, and this system should update any state related
        /// to how it selects content.
        /// </summary>
        /// <remarks>If a content saliency strategy does not need to keep track
        /// of any state, then this method can be empty.</remarks>
        /// <param name="content">The content that has been selected.</param>
        void ContentWasSelected(ContentSaliencyOption content);

    }
}

