namespace Yarn.Saliency
{
    /// <summary>
    /// Represents a piece of content that may be selected by an <see
    /// cref="IContentSaliencyStrategy"/>.
    /// </summary>
    public interface IContentSaliencyOption {
        /// <summary>
        /// Gets the number of values present in this item's condition.
        /// </summary>
        /// <remarks>
        /// If <see cref="ContentID"/> is <see langword="null"/>, then this
        /// property's value is unused.
        /// </remarks>
        int ConditionValueCount { get; }

        /// <summary>
        /// Gets a string that uniquely identifies this content. This value may
        /// be <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// This is generally a line ID. A <see langword="null"/> value
        /// indicates that no content should be displayed.
        /// </remarks>
        string? ContentID { get; }
    }
}
