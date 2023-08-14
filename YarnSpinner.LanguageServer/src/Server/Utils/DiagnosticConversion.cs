using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LSPDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using YarnDiagnostic = Yarn.Compiler.Diagnostic;

public static class DiagnosticConversionExtension
{
    /// <summary>
    /// Converts a <see cref="Yarn.Compiler.Diagnostic"/> object to a <see
    /// cref="Diagnostic"/>.
    /// </summary>
    /// <param name="diagnostic">The <see cref="Yarn.Compiler.Diagnostic"/> to
    /// convert.</param>
    /// <returns>The converted <see cref="Diagnostic"/>.</returns>
    public static LSPDiagnostic AsLSPDiagnostic(this YarnDiagnostic diagnostic)
    {
        return new LSPDiagnostic
        {
            Range = new Range(
                    diagnostic.Range.Start.Line,
                    diagnostic.Range.Start.Character,
                    diagnostic.Range.End.Line,
                    diagnostic.Range.End.Character
                ),
            Message = diagnostic.Message,
            Severity = diagnostic.Severity switch
            {
                YarnDiagnostic.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
                YarnDiagnostic.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                YarnDiagnostic.DiagnosticSeverity.Info => DiagnosticSeverity.Information,
                _ => DiagnosticSeverity.Error,
            },
            Source = diagnostic.FileName,
        };
    }
}
