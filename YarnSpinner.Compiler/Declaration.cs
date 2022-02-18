namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a range of text in a multi-line string.
    /// </summary>
    [System.Serializable]
    public class Range
    {
        /// <summary>
        /// Gets or sets the start position of this range.
        /// </summary>
        public Position Start { get; set; } = new Position();

        /// <summary>
        /// Gets or sets the end position of this range.
        /// </summary>
        public Position End { get; set; } = new Position();

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Range range &&
                   EqualityComparer<Position>.Default.Equals(this.Start, range.Start) &&
                   EqualityComparer<Position>.Default.Equals(this.End, range.End);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = -1676728671;
            hashCode = (hashCode * -1521134295) + EqualityComparer<Position>.Default.GetHashCode(this.Start);
            hashCode = (hashCode * -1521134295) + EqualityComparer<Position>.Default.GetHashCode(this.End);
            return hashCode;
        }
    }

    /// <summary>
    /// Represents a position in a multi-line string.
    /// </summary>
    public class Position
    {

        /// <summary>
        /// Gets or sets the zero-indexed line of this position.
        /// </summary>
        public int Line { get; set; } = -1;

        /// <summary>
        /// Gets or sets the zero-indexed character number of this position.
        /// </summary>
        public int Character { get; set; } = -1;

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Position position &&
                   this.Line == position.Line &&
                   this.Character == position.Character;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1927683087;
            hashCode = (hashCode * -1521134295) + this.Line.GetHashCode();
            hashCode = (hashCode * -1521134295) + this.Character.GetHashCode();
            return hashCode;
        }
    }
    
    [System.Serializable]
    public class Declaration
    {
        /// <summary>
        /// Gets the name of this Declaration.
        /// </summary>
        public string Name { get => name; internal set => name = value; }

        /// <summary>
        /// Creates a new instance of the <see cref="Declaration"/> class,
        /// using the given <paramref name="name"/> and default value. The
        /// <see cref="ReturnType"/> of the new instance will be configured
        /// based on the type of <paramref name="defaultValue"/>, and the
        /// <see cref="DeclarationType"/> will be <see
        /// cref="Type.Variable"/>. All other properties will be their
        /// default values.
        /// </summary>
        /// <param name="name">The name of the new declaration.</param>
        /// <param name="defaultValue">The default value of the
        /// declaration. This must be a string, a number (integer or
        /// floating-point), or boolean value.</param>
        /// <param name="description">The description of the new
        /// declaration.</param>
        /// <returns>A new instance of the <see cref="Declaration"/>
        /// class.</returns>
        public static Declaration CreateVariable(string name, Yarn.IType type, IConvertible defaultValue, string description = null)
        {

            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
            }

            if (defaultValue is null)
            {
                throw new ArgumentNullException(nameof(defaultValue));
            }

            // What type of default value did we get?
            System.Type defaultValueType = defaultValue.GetType();

            // We're all good to create the new declaration.
            var decl = new Declaration
            {
                Name = name,
                DefaultValue = defaultValue,
                Type = type,
                Description = description,
            };

            return decl;
        }

        /// <summary>
        /// Gets the default value of this <see cref="Declaration"/>, if no
        /// value has been specified in code or is available from a <see
        /// cref="Dialogue"/>'s <see cref="IVariableStorage"/>.
        /// </summary>
        public IConvertible DefaultValue { get => defaultValue; internal set => defaultValue = value; }

        /// <summary>
        /// Gets a string describing the purpose of this <see
        /// cref="Declaration"/>.
        /// </summary>
        public string Description { get => description; internal set => description = value; }

        /// <summary>
        /// Gets the name of the file in which this Declaration was found.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Declaration"/> was not found in a Yarn
        /// source file, this will be <see cref="ExternalDeclaration"/>.
        /// </remarks>
        public string SourceFileName { get => sourceFileName; internal set => sourceFileName = value; }

        /// <summary>
        /// Gets the name of the node in which this Declaration was found.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Declaration"/> was not found in a Yarn
        /// source file, this will be <see langword="null"/>.
        /// </remarks>
        public string SourceNodeName { get => sourceNodeName; internal set => sourceNodeName = value; }

        /// <summary>
        /// The line number at which this Declaration was found in the
        /// source file.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Declaration"/> was not found in a Yarn
        /// source file, this will be -1.
        /// </remarks>
        public int SourceFileLine => Range.Start.Line;

        /// <summary>
        /// Get or sets a value indicating whether this Declaration was implicitly
        /// inferred from usage.
        /// </summary>
        /// <value>If <see langword="true"/>, this Declaration was
        /// implicitly inferred from usage. If <see langword="false"/>,
        /// this Declaration appears in the source code.</value>
        public bool IsImplicit { get; internal set; }

        public Yarn.IType Type {get;internal set;}

        /// <summary>
        /// The string used for <see cref="SourceFileName"/> if the
        /// Declaration was found outside of a Yarn source file.
        /// </summary>
        public const string ExternalDeclaration = "(External)";

        /// <summary>
        /// Gets the range of text at which this declaration occurs.
        /// </summary>
        /// <remarks>
        /// This range refers to the declaration of the symbol itself, and not
        /// any syntax surrounding it. For example, the declaration
        /// <c>&lt;&lt;declare $x = 1&gt;&gt;</c> would have a Range referring
        /// to the <c>$x</c> symbol.
        /// </remarks>
        public Range Range { get; internal set; } = new Range();

        private string name;
        private IConvertible defaultValue;
        private Yarn.IType type;
        private string description;
        private string sourceFileName;
        private string sourceNodeName;
        
        public Declaration()
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            string result;
            
            result = $"{Name} : {Type} = {DefaultValue}";
            
            if (string.IsNullOrEmpty(Description))
            {
                return result;
            }
            else
            {
                return result + $" (\"{Description}\")";
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Declaration otherDecl))
            {
                return false;
            }

            return this.Name == otherDecl.Name &&
                this.Type == otherDecl.Type &&
                this.DefaultValue == otherDecl.DefaultValue &&
                this.Description == otherDecl.Description;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            
            return this.Name.GetHashCode()
                ^ this.Type.GetHashCode()
                ^ this.DefaultValue.GetHashCode()
                ^ (this.Description ?? string.Empty).GetHashCode();
        }

    }
}
