// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    /// <summary>
    /// Provides methods for constructing <see
    /// cref="Yarn.Compiler.Declaration"/> objects.
    /// </summary>
    /// <remarks>
    /// To use this class, create an instance of it, and call the
    /// <c>With</c>-prefixed methods to set properties. When you're done, access
    /// the <see cref="Declaration"/> property to get the final, constructed
    /// <see cref="Yarn.Compiler.Declaration"/>.
    /// </remarks>
    public class DeclarationBuilder
    {
        /// <summary>
        /// Gets the <see cref="Declaration"/> instance constructed by this <see
        /// cref="DeclarationBuilder"/>.
        /// </summary>
        public Declaration Declaration { get; } = new Declaration { };

        /// <summary>
        /// Sets the <see cref="Declaration.Name"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="name">The name to apply to the Declaration.</param>
        /// <returns>The <see cref="DeclarationBuilder"/> instance that received
        /// this method call.</returns>
        public DeclarationBuilder WithName(string name)
        {
            this.Declaration.Name = name;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.DefaultValue"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="defaultValue">The default value to apply to the
        /// Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithDefaultValue(System.IConvertible defaultValue)
        {
            this.Declaration.DefaultValue = defaultValue;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.Description"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="description">The description to apply to the
        /// Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithDescription(string description)
        {
            this.Declaration.Description = description;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.SourceFileName"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="sourceFileName">The source file name to apply to the
        /// Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithSourceFileName(string sourceFileName)
        {
            this.Declaration.SourceFileName = sourceFileName;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.SourceNodeName"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="sourceNodeName">The source node name to apply to the
        /// Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithSourceNodeName(string sourceNodeName)
        {
            this.Declaration.SourceNodeName = sourceNodeName;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.Range"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="range">The range to apply to the Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithRange(Yarn.Compiler.Range range)
        {
            this.Declaration.Range = range;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.IsImplicit"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="isImplicit">The is-implicit value to apply to the
        /// Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithImplicit(bool isImplicit)
        {
            this.Declaration.IsImplicit = isImplicit;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Declaration.Type"/> of the <see
        /// cref="Declaration"/>.
        /// </summary>
        /// <param name="type">The type to apply to the Declaration.</param>
        /// <inheritdoc cref="WithName" path="/returns"/>
        public DeclarationBuilder WithType(IType type)
        {
            this.Declaration.Type = type;
            return this;
        }
    }
}
