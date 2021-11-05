namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;

    /// <summary>
    /// A listener that, when used with a <see cref="ParseTreeWalker"/>,
    /// populates a <see cref="List{T}"/> of <see cref="IType"/> objects
    /// that represent any new types that were declared in the parse tree.
    /// These new types can then be used for values elsewhere in the
    /// script.
    /// </summary>
    internal class TypeDeclarationListener : YarnSpinnerParserBaseListener
    {
        public IEnumerable<Diagnostic> Diagnostics { get => this.diagnostics; }

        private readonly string sourceFileName;
        private readonly CommonTokenStream tokens;
        private readonly IParseTree tree;
        private readonly List<IType> typeDeclarations;
        private List<Diagnostic> diagnostics = new List<Diagnostic>();

        public TypeDeclarationListener(string sourceFileName, CommonTokenStream tokens, IParseTree tree, ref List<IType> typeDeclarations)
        {
            this.sourceFileName = sourceFileName;
            this.tokens = tokens;
            this.tree = tree;
            this.typeDeclarations = typeDeclarations;
        }

        public override void ExitEnum_statement([NotNull] YarnSpinnerParser.Enum_statementContext context)
        {
            // We've just finished walking an enum statement! We're almost
            // ready to add its declaration.

            // First: are there any types with the same name as this?
            if (this.typeDeclarations.Any(t => t.Name == context.name.Text))
            {
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, $"Cannot declare new enum {context.name.Text}: a type with this name already exists"));
                return;
            }

            // Get its description, if any
            var description = Compiler.GetDocumentComments(this.tokens, context, false);

            // Create the new type.
            var enumType = new EnumType(context.name.Text, description);

            // What is the type of this enum's raw values?
            var permittedRawValueTypes = new[]
            {
                BuiltinTypes.Number,
                BuiltinTypes.String,
            };

            // The type of the raw values this enum is using.
            IType typeOfRawValues = null;

            foreach (var caseStatement in context.enum_case_statement())
            {
                if (caseStatement.rawValue == null)
                {
                    // No raw value in this case statement.
                    caseStatement.RawValue = null;
                }
                else
                {
                    // This case statement has a raw value. Parse it.
                    Value value = new ConstantValueVisitor(context, this.sourceFileName, this.typeDeclarations, ref this.diagnostics).Visit(caseStatement.rawValue);

                    caseStatement.RawValue = value;

                    if (typeOfRawValues == null)
                    {
                        // This is the first raw value we've seen; set the
                        // raw type of the enum to this type.
                        typeOfRawValues = value.Type;
                    }
                    else if (TypeUtil.IsSubType(typeOfRawValues, value.Type) == false)
                    {
                        // We already had a raw type, and this case
                        // statement uses an incompatible type. Report an error.
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, caseStatement, $"Enum member raw values may only be of a single type (they can't be {typeOfRawValues.Name} and {value.Type.Name})"));
                        return;
                    }

                    // Report an error if this value isn't an allowable type.
                    if (permittedRawValueTypes.Contains(value.Type) == false)
                    {
                        this.diagnostics.Add(new Diagnostic(
                            this.sourceFileName,
                            caseStatement,
                            $"Invalid type: enum raw values cannot be {value.Type?.Name ?? "undefined"} (they must be of type {string.Join(" or ", permittedRawValueTypes.Select(t => t.Name))})"));

                        return;
                    }
                }
            }

            if (typeOfRawValues == null)
            {
                // We never saw a raw value, so default it to number.
                typeOfRawValues = BuiltinTypes.Number;
            }

            // If typeOfRawValues is BuiltinTypes.Number, we will use this
            // value to automatically assign a number to each successive
            // one.
            int numberIncrement = 0;

            // The hash codes of the raw values we've assigned
            var rawValueHashes = new HashSet<int>();

            // Now walk through the list of case statements, generating
            // EnumMembers for each one.
            for (int i = 0; i < context.enum_case_statement().Length; i++)
            {
                var @case = context.enum_case_statement(i);

                // Report an error if we have a duplicate member
                if (enumType.Members.Any(existingMember => existingMember.Name == @case.name.Text))
                {
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, @case, $"Enum {enumType.Name} already has a case called {@case.name.Text}"));
                    return;
                }

                // Get the documentation comments for this case, if any
                var caseDescription = Compiler.GetDocumentComments(this.tokens, @case);

                // Does this case statement have a raw value?
                Value rawValue;

                if (typeOfRawValues == BuiltinTypes.Number)
                {
                    if (@case.RawValue != null)
                    {
                        rawValue = @case.RawValue;

                        // Start incrementing from this point
                        numberIncrement = @case.RawValue.ConvertTo<int>();
                    }
                    else
                    {
                        rawValue = new Value(BuiltinTypes.Number, numberIncrement);
                    }

                    numberIncrement += 1;
                }
                else if (typeOfRawValues == BuiltinTypes.String)
                {
                    if (@case.RawValue != null)
                    {
                        rawValue = @case.RawValue;
                    }
                    else
                    {
                        // We don't have a default we can use!
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, @case, "All enum cases must have a value, if strings are used"));
                        return;
                    }
                }
                else
                {
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, @case, $"Internal error: invalid enum case raw value type {typeOfRawValues.Name}"));
                    return;
                }

                // Check to see if we've assigned this raw value already
                var hash = rawValue.InternalValue.GetHashCode();

                if (rawValueHashes.Contains(hash))
                {
                    // They're not allowed to be the same!
                    this.diagnostics.Add(new Diagnostic(
                        this.sourceFileName,
                        @case,
                        $"Enum member raw values must be unique"
                    ));
                    return;
                }

                rawValueHashes.Add(hash);

                var member = new EnumMember
                {
                    Name = @case.name.Text,
                    RawValue = rawValue,
                    Description = caseDescription,
                };

                enumType.AddMember(member);
            }

            this.typeDeclarations.Add(enumType);
        }
    }
}
