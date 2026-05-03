using System.Collections.Generic;

namespace Yarn.Compiler
{
    /// <summary>
    /// Contains information about the compilation, for use in custom diagnostic
    /// providers.
    /// </summary>
    /// <seealso cref="ICustomDiagnosticProvider"/>
    public interface IBuildContext
    {
        /// <summary>
        /// The collection of line statements in the compilation.
        /// </summary>
        public IEnumerable<LineStatementSyntaxNode> LineStatements { get; }

        /// <summary>
        /// Emits a custom diagnostic for the compilation, given a syntax node
        /// to attach it to, the message for the diagnostic, and its severity.
        /// </summary>
        /// <param name="node">The syntax node responsible for this
        /// diagnostic.</param>
        /// <param name="message">The message for the diagnostic.</param>
        /// <param name="severity">The severity of the diagnostic.</param>
        public void EmitDiagnostic(SyntaxNode node, string message, Diagnostic.DiagnosticSeverity severity = Diagnostic.DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Provides custom diagnostics as part of a compilation.
    /// </summary>
    /// <remarks>Custom diagnostics providers allow developers to provide their
    /// own, project-specific diagnostics. To use them, create a class that
    /// implements this interface, and add an instance of it to the <see
    /// cref="CompilationJob.CustomDiagnosticProviders"/> list.</remarks>
    public interface ICustomDiagnosticProvider
    {
        /// <summary>
        /// Provides diagnostics to a build context.
        /// </summary>
        /// <remarks>This method is called by the compiler during compilation.
        /// To produce diagnostics using the information stored in the build
        /// context, call the <see
        /// cref="IBuildContext.EmitDiagnostic(SyntaxNode, string,
        /// Diagnostic.DiagnosticSeverity)"/> method and provide the syntax node
        /// that caused the problem, as well as a message and optional
        /// severity.</remarks>
        /// <param name="context"></param>
        public void ProvideDiagnostics(IBuildContext context);
    }
}
