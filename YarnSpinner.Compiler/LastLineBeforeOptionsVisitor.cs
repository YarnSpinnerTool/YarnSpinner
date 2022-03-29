namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    class LastLineBeforeOptionsVisitor : YarnSpinnerParserBaseVisitor<byte>
    {
        // tags this line as being one that is the statement immediately before an option block, does this by adding a #lastline tag onto this line
        // no checking is needed because only lines that are needing to be tagged will be visited, others are skipped.
        // The line is tagged regardless of if there is a #lastline there already
        // technically unecessary in that case but this feels uncommon enough to not bother edgecasing
        public override byte VisitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            var hashtag = new YarnSpinnerParser.HashtagContext(context, 0);
            hashtag.text = new CommonToken(YarnSpinnerLexer.HASHTAG_TEXT, "lastline");
            context.AddChild(hashtag);

            return 0;
        }

        // finds any lines that immediately follow an option block and visits them for tagging
        // this works by making our way through each and every statement inside of a body block performing the following:
        // 1. assume the current statement is an option block
        // 2. assume the statement before it is a line
        // 3. if both of these hold true we have found a line we need to flag as being before options
        // 4. repeat this process until we run out of statements to check
        public override byte VisitBody([NotNull] YarnSpinnerParser.BodyContext context)
        {
            // starting at i = 1 is not a bug, we want to skip the first statement
            // the first statement by definition cannot have a line before it
            var statements = context.statement();
            if (statements.Length < 1)
            {
                return 0;
            }

            for (int i = 1; i < statements.Length; i++)
            {
                // we aren't an option, keep moving
                if (statements[i].shortcut_option_statement() == null)
                {
                    continue;
                }

                // the statement before us isn't a line, continue
                var previous = statements[i - 1].line_statement();
                if (previous == null)
                {
                    continue;
                }

                // ok now at this point we know the line that needs to be tagged as the last line
                // we do that inside the line visitation
                this.Visit(previous);
            }

            return 0;
        }
    }
}
