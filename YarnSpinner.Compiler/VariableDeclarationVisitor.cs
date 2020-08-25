namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;

    internal class VariableDeclarationVisitor : YarnSpinnerParserBaseVisitor<int> {

        private IEnumerable<VariableDeclaration> ExistingVariableDeclarations;
        public ICollection<VariableDeclaration> NewVariableDeclarations { get; private set; }

        private IEnumerable<VariableDeclaration> AllDeclarations
        {
            get
            {
                foreach (var decl in ExistingVariableDeclarations)
                {
                    yield return decl;
                }
                foreach (var decl in NewVariableDeclarations)
                {
                    yield return decl;
                }
            }
        }

        public VariableDeclarationVisitor(IEnumerable<VariableDeclaration> existingDeclarations)
        {
            this.ExistingVariableDeclarations = existingDeclarations;
            this.NewVariableDeclarations = new List<VariableDeclaration>();
        }

        public override int VisitDeclare_statement(YarnSpinnerParser.Declare_statementContext context) {

            string variableName = context.variable().GetText();
            
            // Does this variable name already exist in our declarations?
            foreach (var decl in AllDeclarations) {
                if (decl.name == variableName) {
                    throw new TypeException($"{decl.name} has already been declared");
                }
            }

            // Figure out the type of the value
            var expressionVisitor = new ExpressionTypeVisitor(null, null, true);
            var type = expressionVisitor.Visit(context.value());
            
            // Figure out the value itself
            var constantValueVisitor = new ConstantValueVisitor();
            var value = constantValueVisitor.Visit(context.value());

            var declaration = new VariableDeclaration
            {
                name = variableName,
                type = type,
                defaultValue = value,
            };

            this.NewVariableDeclarations.Add(declaration);

            return 0;
        }
    }
}
