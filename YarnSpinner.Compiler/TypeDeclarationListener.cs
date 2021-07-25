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
        private readonly string sourceFileName;
        private readonly CommonTokenStream tokens;
        private readonly IParseTree tree;
        private readonly List<IType> typeDeclarations;

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
                throw new TypeException(context, $"Cannot declare new enum {context.name.Text}: a type with this name already exists", this.sourceFileName);
            }

            // Get its description, if any
            var description = Compiler.GetDocumentComments(this.tokens, context, false);

            // Create the new type.
            var enumType = new EnumType(context.name.Text, description);

            // Now walk through the list of case statements, generating
            // EnumMembers for each one.
            for (int i = 0; i < context.enum_case_statement().Length; i++)
            {
                var @case = context.enum_case_statement(i);

                // Get the documentation comments for this case, if any
                var caseDescription = Compiler.GetDocumentComments(this.tokens, @case);

                var member = new EnumMember
                {
                    Name = @case.name.Text,
                    InternalRepresentation = i,
                    Description = caseDescription,
                };

                enumType.AddMember(member);
            }

            this.typeDeclarations.Add(enumType);
        }
    }
}
