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

        public IEnumerable<Problem> Problems { get => this.problems; }
        private List<Problem> problems = new List<Problem>();
    }
}
