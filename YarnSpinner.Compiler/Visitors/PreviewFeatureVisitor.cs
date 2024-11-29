using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System.Collections.Generic;

namespace Yarn.Compiler
{
    internal class PreviewFeatureVisitor : YarnSpinnerParserBaseVisitor<int>
    {

        internal PreviewFeatureVisitor(FileParseResult file, int languageVersion, IList<Diagnostic> diagnostics)
        {
            this.File = file;
            this.LanguageVersion = languageVersion;
            this.Diagnostics = diagnostics;
        }

        private void AddLanguageFeatureError(ParserRuleContext context, string featureType)
        {
            AddError(context, $"Language feature \"{featureType}\" is only available when preview features are enabled");
        }

        private void AddError(ParserRuleContext context, string message)
        {
            var d = new Diagnostic(File.Name, context, message, Diagnostic.DiagnosticSeverity.Error);
            this.Diagnostics.Add(d);
        }

        public FileParseResult File { get; }
        public int LanguageVersion { get; }
        public IList<Diagnostic> Diagnostics { get; }

        public void AddDiagnosticsForDeclarations(IEnumerable<Declaration> declarations)
        {
            foreach (var decl in declarations)
            {
                if (decl.IsInlineExpansion && LanguageVersion < Project.YarnSpinnerProjectVersion3)
                {
                    AddLanguageFeatureError(decl.InitialValueParserContext!, "smart variables");
                }
            }
        }

        public override int VisitEnum_statement([NotNull] YarnSpinnerParser.Enum_statementContext context)
        {
            if (LanguageVersion < Project.YarnSpinnerProjectVersion3)
            {
                AddLanguageFeatureError(context, "enums");
            }

            return base.VisitEnum_statement(context);
        }

        public override int VisitLine_group_statement([NotNull] YarnSpinnerParser.Line_group_statementContext context)
        {
            if (LanguageVersion < Project.YarnSpinnerProjectVersion3)
            {
                AddLanguageFeatureError(context, "line groups");
            }

            return base.VisitLine_group_statement(context);
        }

        public override int VisitLineOnceCondition([NotNull] YarnSpinnerParser.LineOnceConditionContext context)
        {
            if (LanguageVersion < Project.YarnSpinnerProjectVersion3)
            {
                AddLanguageFeatureError(context, "'once' conditions");
            }

            return base.VisitLineOnceCondition(context);
        }

        public override int VisitOnce_statement([NotNull] YarnSpinnerParser.Once_statementContext context)
        {
            if (LanguageVersion < Project.YarnSpinnerProjectVersion3)
            {
                AddLanguageFeatureError(context, "'once' statements");
            }

            return base.VisitOnce_statement(context);
        }

        public override int VisitWhen_header([NotNull] YarnSpinnerParser.When_headerContext context)
        {
            if (LanguageVersion < Project.YarnSpinnerProjectVersion3)
            {
                AddLanguageFeatureError(context, "'when' headers");
            }

            return base.VisitWhen_header(context);
        }
    }
}
