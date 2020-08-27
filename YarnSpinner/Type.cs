namespace Yarn
{
    
    /// <summary>
    /// The type of a <see cref="Value"/>.
    /// </summary>
    public enum Type
    {
        /// <summary>A number.</summary>
        Number,

#pragma warning disable CA1720 // Identifier contains type name
        /// <summary>A string.</summary>
        String,
#pragma warning restore CA1720 // Identifier contains type name

        /// <summary>A boolean value.</summary>
        Bool,

        /// <summary>A value of undefined type.</summary>
        Undefined,

    }
    
}
