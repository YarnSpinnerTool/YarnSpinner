using System.Collections.Generic;

namespace Yarn.Compiler
{
    public interface IBuildContext
    {
        public IEnumerable<SyntaxTree> SyntaxTrees { get; }
        public void EmitDiagnostic(SyntaxNode node, string message, Diagnostic.DiagnosticSeverity severity = Diagnostic.DiagnosticSeverity.Warning);
    }

    public interface ICustomDiagnosticProvider
    {
        public void ProvideDiagnostics(IBuildContext context);
    }
}
