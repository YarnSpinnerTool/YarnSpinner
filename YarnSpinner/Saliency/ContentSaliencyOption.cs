namespace Yarn.Saliency
{
    /// <summary>
    /// Indicates what type of content a <see cref="ContentSaliencyOption"/>
    /// represents.
    /// </summary>
    /// <seealso cref="ContentSaliencyOption.ContentType"/>
    public enum ContentSaliencyContentType
    {
        /// <summary>
        /// The content represents a node in a node group.
        /// </summary>
        Node,

        /// <summary>
        /// The content represents a line in a line group.
        /// </summary>
        Line,
    }

    /// <summary>
    /// Represents a piece of content that may be selected by an <see
    /// cref="IContentSaliencyStrategy"/>.
    /// </summary>
    public sealed class ContentSaliencyOption
    {
        /// <summary>
        /// Initializes a new instance of the ContentSaliencyOption class with
        /// the specified content ID.
        /// </summary>
        /// <param name="id">A string representing the unique identifier for the
        /// content.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the
        /// provided ID is null.</exception>
        public ContentSaliencyOption(string id)
        {
            this.ContentID = id ?? throw new System.ArgumentNullException(nameof(id), "The content ID cannot be null.");
        }

        /// <summary>
        /// Gets the number of conditions that passed for this piece of content.
        /// </summary>
        public int PassingConditionValueCount { get; set; }

        /// <summary>
        /// Get the number of conditions that failed for this piece of content.
        /// </summary>
        public int FailingConditionValueCount { get; set; }

        /// <summary>
        /// Gets a string that uniquely identifies this content.
        /// </summary>
        public string ContentID { get; }

        /// <summary>
        /// Gets the complexity score of this option.
        /// </summary>
        public int ComplexityScore { get; set; }

        /// <summary>
        /// Gets the type of content that this option represents.
        /// </summary>
        /// <remarks>
        /// This information may be used by custom <see
        /// cref="IContentSaliencyStrategy"/> classes to allow them to have
        /// different behaviour depending on the type of the content.
        /// </remarks> 
        public ContentSaliencyContentType ContentType { get; set; }

        /// <summary>
        /// Gets a unique variable name that can be used for tracking the view
        /// count of a specific piece of content. This value is <see
        /// langword="null"/> if <see cref="ContentID"/> is <see
        /// langword="null"/> or empty.
        /// </summary>
        public string ViewCountKey => string.IsNullOrEmpty(ContentID)
            ? throw new System.InvalidOperationException($"Internal error: content has a null or empty {nameof(ContentID)}")
            : $"$Yarn.Internal.Content.ViewCount.{this.ContentID}";

        /// <summary>
        /// The destination instruction that the virtual machine will jump to if
        /// this option is selected.
        /// </summary>
        /// <remarks>This property is internal to Yarn Spinner, and is used by
        /// the <see cref="VirtualMachine"/> class.</remarks>
        internal int Destination { get; set; }
    }
}
