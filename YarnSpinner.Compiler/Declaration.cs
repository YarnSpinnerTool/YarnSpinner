// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

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
        /// Represents the default value for a Range.
        /// </summary>
        public static readonly Range InvalidRange = new(-1, -1, -1, -1);

        /// <summary>
        /// Initializes a new instance of the <see cref="Range"/> class, given
        /// start and end information.
        /// </summary>
        /// <param name="startLine">The zero-indexed line number of the start of
        /// the range.</param>
        /// <param name="startCharacter">The zero-indexed character number of
        /// the start of the range.</param>
        /// <param name="endLine">The zero-indexed line number of the end of the
        /// range.</param>
        /// <param name="endCharacter">The zero-indexed character number of the
        /// end of the range.</param>
        public Range(int startLine, int startCharacter, int endLine, int endCharacter)
        {
            this.Start = new Position(startLine, startCharacter);
            this.End = new Position(endLine, endCharacter);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Range"/> class, given
        /// start and end information.
        /// </summary>
        /// <param name="startPosition">The position at the start of
        /// the range.</param>
        /// <param name="endPosition">The position at the end of
        /// the range.</param>
        public Range(Position startPosition, Position endPosition)
        {
            this.Start = startPosition;
            this.End = endPosition;
        }

        /// <summary>
        /// Gets or sets the start position of this range.
        /// </summary>
        public Position Start { get; private set; }

        /// <summary>
        /// Gets or sets the end position of this range.
        /// </summary>
        public Position End { get; private set; }

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

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.Start}-{this.End}";
        }

        /// <summary>
        /// Gets a value indicating whether this range is valid.
        /// </summary>
        /// <remarks>
        /// A range is valid when its start and end positions are both valid,
        /// and the start position is not after the end position.
        /// </remarks>
        public bool IsValid
        {
            get
            {
                return this.Start.IsValid && this.End.IsValid && this.End >= this.Start;
            }
        }
    }

    /// <summary>
    /// Represents a position in a multi-line string.
    /// </summary>
    [System.Serializable]
    public class Position
    {
        /// <summary>
        /// Gets or sets the zero-indexed line of this position.
        /// </summary>
        public int Line { get; private set; } = -1;

        /// <summary>
        /// Gets or sets the zero-indexed character number of this position.
        /// </summary>
        public int Character { get; private set; } = -1;

        /// <summary>
        /// Gets a value indicating whether this position has a zero or positive
        /// line and character number.
        /// </summary>
        public bool IsValid => this.Line >= 0 && this.Character >= 0;

        /// <summary>
        /// Represents the default value for a Position.
        /// </summary>
        public static readonly Position InvalidPosition = new(-1, -1);

        /// <summary>
        /// Initialises a new instance of the Position class with the specified
        /// line and character.
        /// </summary>
        /// <param name="line">The zero-indexed line number.</param>
        /// <param name="character">The zero-index character number.</param>
        public Position(int line, int character)
        {
            this.Line = line;
            this.Character = character;
        }

        /// <summary>
        /// Initialises a new instance of the Position class.
        /// </summary>
        public Position()
        {
            this.Line = InvalidPosition.Line;
            this.Character = InvalidPosition.Character;
        }

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

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.Line}:{this.Character}";
        }

        /// <summary>
        /// Compares two positions and returns true if <paramref name="a"/> is
        /// equal to or after <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first position.</param>
        /// <param name="b">The second position.</param>
        /// <returns>true if a is after or equal to b; false
        /// otherwise.</returns>
        public static bool operator >=(Position a, Position b)
        {
            return a.Line >= b.Line && a.Character >= b.Character;
        }
        /// <summary>
        /// Compares two positions and returns true if <paramref name="a"/> is
        /// equal to or before <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first position.</param>
        /// <param name="b">The second position.</param>
        /// <returns>true if a is before or equal to b; false
        /// otherwise.</returns>
        public static bool operator <=(Position a, Position b)
        {
            return a.Line <= b.Line && a.Character <= b.Character;
        }

        /// <summary>
        /// Adds two positions by component and returns the result.
        /// </summary>
        /// <param name="a">The first position.</param>
        /// <param name="b">The second position.</param>
        /// <returns>The resulting combined position.</returns>
        public static Position operator +(Position a, Position b)
        {
            return new Position(a.Line + b.Line, a.Character + b.Character);
        }
    }

    /// <summary>
    /// Represents a variable declaration
    /// </summary>
    [Serializable]
    public class Declaration
    {
        /// <summary>
        /// Gets the name of this Declaration.
        /// </summary>
        public string Name { get; internal set; } = "<unknown>";

        /// <summary>
        /// Creates a new instance of the <see cref="Declaration"/> class,
        /// using the given name, type and default value.
        /// </summary>
        /// <param name="name">The name of the new declaration.</param>
        /// <param name="type">The type of the declaration.</param>
        /// <param name="defaultValue">The default value of the
        /// declaration. This must be a string, a number (integer or
        /// floating-point), or boolean value.</param>
        /// <param name="description">The description of the new
        /// declaration.</param>
        /// <returns>A new instance of the <see cref="Declaration"/>
        /// class.</returns>
        public static Declaration CreateVariable(string name, IType type, IConvertible defaultValue, string? description = null)
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

            // We're all good to create the new declaration.
            return new Declaration
            {
                Name = name,
                DefaultValue = defaultValue,
                Type = type,
                Description = description,
            };
        }

        /// <summary>
        /// Gets the default value of this <see cref="Declaration"/>, if no
        /// value has been specified in code or is available from a <see
        /// cref="Dialogue"/>'s <see cref="IVariableStorage"/>.
        /// </summary>
        public IConvertible? DefaultValue { get; internal set; }

        /// <summary>
        /// Gets a string describing the purpose of this <see
        /// cref="Declaration"/>.
        /// </summary>
        public string? Description { get; internal set; }

        /// <summary>
        /// Gets the name of the file in which this Declaration was found.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Declaration"/> was not found in a Yarn
        /// source file, this will be <see cref="ExternalDeclaration"/>.
        /// </remarks>
        public string SourceFileName { get; internal set; } = ExternalDeclaration;

        /// <summary>
        /// Gets the name of the node in which this Declaration was found.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Declaration"/> was not found in a Yarn
        /// source file, this will be <see langword="null"/>.
        /// </remarks>
        public string? SourceNodeName { get; internal set; }

        /// <summary>
        /// Gets the line number at which this Declaration was found in the
        /// source file.
        /// </summary>
        /// <remarks>
        /// If this <see cref="Declaration"/> was not found in a Yarn
        /// source file, this will be -1.
        /// </remarks>
        public int SourceFileLine => this.Range.Start.Line;

        /// <summary>
        /// Gets a value indicating that this Declaration does not refer to a
        /// stored variable, and instead represents an inline-expanded
        /// expression (a 'smart variable').
        /// </summary>
        public bool IsInlineExpansion { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this Declaration was implicitly
        /// inferred from usage.
        /// </summary>
        /// <value>If <see langword="true"/>, this Declaration was implicitly
        /// inferred from usage. If <see langword="false"/>, this Declaration
        /// appears in the source code.</value>
        public bool IsImplicit { get; internal set; }

        /// <summary>
        /// Gets the type of the variable, as represented by an object that
        /// implements <see cref="IType"/>.
        /// </summary>
        public IType Type { get; internal set; } = Types.Error;

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
        public Range Range { get; internal set; } = Range.InvalidRange;

        /// <summary>
        /// Gets or sets the parser context for the initial value provided in
        /// this variable's 'declare' statement, if any. This is only valid for
        /// variable declarations, not functions (because functions don't have a
        /// value.)
        /// </summary>
        public YarnSpinnerParser.ExpressionContext? InitialValueParserContext { get; set; }

        /// <summary>
        /// Gets the collection of <see cref="Declaration"/> objects whose value
        /// depends upon this <see cref="Declaration"/>.
        /// </summary>
        public IEnumerable<Declaration> Dependents { get; internal set; } = Array.Empty<Declaration>();

        /// <summary>
        /// Gets the collection of <see cref="Declaration"/> objects that this
        /// <see cref="Declaration"/> depends upon the value of.
        /// </summary>
        public IEnumerable<Declaration> Dependencies { get; internal set; } = Array.Empty<Declaration>();

        /// <summary>
        /// Gets a value indicating whether this Declaration represents a
        /// variable, and not a function.
        /// </summary>
        public bool IsVariable => !(this.Type is FunctionType);

        /// <inheritdoc/>
        public override string ToString()
        {
            string result = $"{this.Name} : {this.Type}";
            if (this.IsVariable)
            {
                result += " = ";
                if (IsInlineExpansion)
                {
                    result += this.InitialValueParserContext?.GetText() ?? "missing parser context!";
                }
                else
                {

                    result += this.DefaultValue;
                }
            }

            if (string.IsNullOrEmpty(this.Description))
            {
                return result;
            }
            else
            {
                return result + $" (\"{this.Description}\")";
            }
        }
    }
}
