namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// Finds #once hashtags on line statements, and registers variables for
    /// them. Finds calls to the <c>once</c> function, and if they do not have a
    /// single parameter, adds one.
    /// </summary>
    internal class ViewOnceVisitor : YarnSpinnerParserBaseVisitor<byte>
    {
        private readonly string sourceFileName;
        private readonly List<Declaration> declarations;
        private readonly List<Diagnostic> diagnostics;

        public ViewOnceVisitor(string sourceFileName, List<Declaration> declarations, List<Diagnostic> diagnostics)
        {
            this.sourceFileName = sourceFileName;
            this.declarations = declarations;
            this.diagnostics = diagnostics;
        }

        public override byte VisitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            // When a line statement contains a #once hashtag, register a new
            // variable for it that tracks whether this line has been seen by
            // the player or not. This variable will be checked when the line is
            // encountered, and set when the line is viewed.

            base.VisitLine_statement(context);
            var hashtags = context.hashtag();
            if (hashtags == null || hashtags.Length == 0)
            {
                return 0;
            }

            // Only one #once hashtag is allowed to be present
            YarnSpinnerParser.HashtagContext onceHashtag = null;
            int onceHashtagCount = 0;
            foreach (var hashtag in hashtags)
            {
                if (hashtag.text.Text == "once")
                {
                    if (onceHashtag != null)
                    {
                        var diag = new Diagnostic(this.sourceFileName, hashtag, "Only one #once hashtag is allowed per line");
                        this.diagnostics.Add(diag);
                    }

                    onceHashtagCount += 1;
                    onceHashtag = hashtag;
                }
            }

            if (onceHashtagCount != 1)
            {
                return 0;
            }

            var lineID = Compiler.GetLineID(context);

            // Register a boolean variable that will track whether we have seen
            // this line before
            var decl = Declaration.CreateVariable(
                Compiler.GetContentViewedVariableName(lineID),
                Types.Boolean,
                defaultValue: false,
                $"Tracks whether the line {lineID} has been seen by the player before.");

            this.declarations.Add(decl);
            return 0;
        }
    }
}
