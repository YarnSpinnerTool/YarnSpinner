using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TypeChecker;

namespace Yarn.Compiler
{
    internal class UserDefinedTypesListener : InterruptableListener
    {
        private readonly string sourceFileName;
        private readonly CommonTokenStream tokens;
        private readonly List<TypeBase> knownTypes;
        private readonly List<Diagnostic> diagnostics = new();
        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        internal UserDefinedTypesListener(string sourceFileName, CommonTokenStream tokens, List<TypeBase> knownTypes, CancellationToken cancellationToken) : base(cancellationToken)
        {
            this.sourceFileName = sourceFileName;
            this.tokens = tokens;
            this.knownTypes = knownTypes;
        }

        public override void ExitEnum_statement([NotNull] YarnSpinnerParser.Enum_statementContext context)
        {
            // We've just finished walking an enum statement! We're almost ready
            // to add its declaration.

            if (context.exception != null)
            {
                // A parse exception exists in this context. Don't attempt to
                // create a type.
                return;
            }

            // First: are there any types with the same name as this?
            if (this.knownTypes.Any(t => t.Name == context.name.Text))
            {
                // There is! That's not allowed. Issue a diagnostic and return
                // without registering the new type.
                this.diagnostics.Add(DiagnosticDescriptor.RedeclarationOfExistingType.Create(this.sourceFileName, context, context.name.Text));

                return;
            }

            // Get its description, if any
            var description = Compiler.GetDocumentComments(this.tokens, context, false);

            // Get the collection of case values that this enum can have.
            foreach (var caseStatement in context.enum_case_statement())
            {
                var caseDescription = Compiler.GetDocumentComments(this.tokens, caseStatement);

                caseStatement.Description = caseDescription;

                // Did the case statement have a raw value syntax node?
                if (caseStatement.rawValue == null)
                {
                    // No raw value in this case statement.
                    caseStatement.RawValue = null;
                }
                else
                {
                    // This case statement has a raw value. Parse it.
                    Value value = new LiteralValueVisitor(context, this.sourceFileName, this.diagnostics).Visit(caseStatement.rawValue);

                    caseStatement.RawValue = value;
                }
            }

            // Decide on the raw type of this enum.
            var allRawTypes = context
                .enum_case_statement()
                .Select(c => c.RawValue?.Type)
                .NotNull()
                .Distinct();

            IType rawType;

            if (allRawTypes.Count() == 0)
            {
                // No case had a raw type. Default the raw type to Number, and
                // set them all to be monotonically increasing values (0, 1, 2,
                // etc)
                rawType = Types.Number;

                int index = 0;
                foreach (var @case in context.enum_case_statement())
                {
                    @case.RawValue = new Value(Types.Number, index);
                    index += 1;
                }
            }
            else if (allRawTypes.Count() > 1)
            {
                // More than one raw value type is present. That's not allowed!

                // Issue a diagnostic and return without registering the new
                // type.
                this.diagnostics.Add(DiagnosticDescriptor.EnumDeclarationError.Create(this.sourceFileName, context.name, $"Enum member raw values may only be of a single type (they can't be a combination of {string.Join(" and ", allRawTypes)})"));
                return;
            }
            else if (allRawTypes.Count() == 1)
            {
                // We only saw one raw value type.
                rawType = allRawTypes.Single();

                var permittedRawTypes = new TypeBase[]
                {
                    (TypeBase)Types.String,
                    (TypeBase)Types.Number,
                };

                var typeIsAllowed = false;
                foreach (var permittedType in permittedRawTypes)
                {
                    if (rawType is TypeBase literal)
                    {
                        if (literal.IsConvertibleTo(permittedType))
                        {
                            typeIsAllowed = true;
                            break;
                        }
                    }
                }

                if (typeIsAllowed == false)
                {
                    // The type is not one of our allowed ones! That's not
                    // allowed. Generate a diagnostic and return without
                    // registering the type.
                    this.diagnostics.Add(DiagnosticDescriptor.EnumDeclarationError.Create(this.sourceFileName, context, $"Enum raw values must be {string.Join(" or ", (IEnumerable<TypeBase?>)permittedRawTypes)}, not {rawType}"));
                    return;
                }

                // Next: if one case has a raw value, they all need to.

                // Do we have any cases that are missing values?
                var casesMissingValues = context.enum_case_statement().Where(c => c.RawValue == null);

                if (casesMissingValues.Any())
                {
                    // Some cases don't have raw values. That's not allowed -
                    // either none of them have raw values, or they all do.

                    // Generate diagnostics for all cases that don't have a
                    // value and return without registering the type.
                    foreach (var @case in casesMissingValues)
                    {
                        this.diagnostics.Add(DiagnosticDescriptor.EnumDeclarationError.Create(this.sourceFileName, @case, $"Enum case {@case.name.Text} must also have a raw value (if any cases have a value, then they all must have one)"));
                    }

                    return;
                }

                // Next: all cases must have a unique value.
                var valueGroups = context.enum_case_statement().GroupBy(c => c.RawValue?.InternalValue);

                var anyDuplicateValues = false;

                foreach (var group in valueGroups)
                {
                    if (group.Count() > 1)
                    {
                        // Two or more cases have the same name! That's not
                        // allowed.
                        anyDuplicateValues = true;
                        foreach (var duplicateCase in group)
                        {
                            this.diagnostics.Add(DiagnosticDescriptor.EnumDeclarationError.Create(this.sourceFileName, duplicateCase, $"Enum case {@duplicateCase.name.Text} must have a unique raw value ({duplicateCase.RawValue?.InternalValue} is used by {group.Count() - 1} other case(s).)"));
                        }
                    }
                }

                if (anyDuplicateValues)
                {
                    // Return; we'll have already generated the appropriate
                    // diagnostics.
                    return;
                }

                // If the raw type is number, we require that all values be integers.
                if (rawType == Types.Number)
                {
                    foreach (var @case in context.enum_case_statement())
                    {
                        float number = Convert.ToSingle(@case.RawValue!.InternalValue);
                        if ((int)(number) != number)
                        {
                            // Not an integer!
                            this.diagnostics.Add(DiagnosticDescriptor.EnumDeclarationError.Create(this.sourceFileName, context, $"Number raw values on enum cases must be integers"));
                        }
                    }
                }


            }
            else
            {
                throw new InvalidOperationException("Internal error");
            }

            // Last check: regardless of any raw values (or lack thereof), all
            // cases must have unique names.
            var nameGroups = context.enum_case_statement().GroupBy(c => c.name.Text);
            bool anyDuplicateNames = false;
            foreach (var group in nameGroups)
            {
                if (group.Count() > 1)
                {
                    // Two or more cases have the same name! That's not allowed.
                    anyDuplicateNames = true;

                    // Produce an error for the duplicates, skipping the first
                    // one
                    foreach (var duplicateCase in group.Skip(1))
                    {
                        this.diagnostics.Add(DiagnosticDescriptor.EnumDeclarationError.Create(this.sourceFileName, duplicateCase, $"Enum case {@duplicateCase.name.Text} must have a unique name."));
                    }
                }
            }

            if (anyDuplicateNames)
            {
                // Return; we'll have already generated the appropriate
                // diagnostics.
                return;
            }

            // Create the new type.
            var newEnumType = new EnumType(context.name.Text, description ?? string.Empty, (TypeBase)rawType);

            // Register the cases for this enum.
            foreach (var @case in context.enum_case_statement())
            {
                newEnumType.AddMember(
                    @case.name.Text,
                    new ConstantTypeProperty(
                        rawType,
                        @case.RawValue?.InternalValue ?? 0,
                        @case.Description ?? string.Empty));
            }

            // Finally, register the type!
            this.knownTypes.Add(newEnumType);

            base.ExitEnum_statement(context);
        }
    }
}
