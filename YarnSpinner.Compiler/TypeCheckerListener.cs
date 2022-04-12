// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Collections.Generic;

namespace Yarn.Compiler
{
    internal class TypeCheckerListener : YarnSpinnerParserBaseListener
    {
        private string name;
        private CommonTokenStream tokens;
        private IParseTree tree;
        private List<IType> typeDeclarations;

        public TypeCheckerListener(string name, CommonTokenStream tokens, IParseTree tree, ref List<IType> typeDeclarations)
        {
            this.name = name;
            this.tokens = tokens;
            this.tree = tree;
            this.typeDeclarations = typeDeclarations;
        }

        public IEnumerable<Diagnostic> Diagnostics { get; internal set; }
    }
}
