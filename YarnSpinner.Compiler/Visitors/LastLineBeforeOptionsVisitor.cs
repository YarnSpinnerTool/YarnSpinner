// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

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

        // entry point for everything
        // if there are no ifs or options with embedded statements this will be all that is visited
        public override byte VisitBody([NotNull] YarnSpinnerParser.BodyContext context)
        {
            this.RunThroughStatements(context.statement());

            return 0;
        }

        // handles the statements inside of an if statement
        // chunks its way through the if, any else-ifs and elses internal block of statements
        public override byte VisitIf_statement([NotNull] YarnSpinnerParser.If_statementContext context)
        {
            RunThroughStatements(context.if_clause().statement());

            var elseifs = context.else_if_clause();
            if (elseifs?.Length > 0)
            {
                foreach (var elif in elseifs)
                {
                    RunThroughStatements(elif.statement());
                }
            }

            var el = context.else_clause()?.statement();
            if (el?.Length > 0)
            {
                RunThroughStatements(el);
            }

            return 0;
        }

        // visiting an option
        // basically just run through the statement (if any exist)
        public override byte VisitShortcut_option_statement([NotNull] YarnSpinnerParser.Shortcut_option_statementContext context)
        {
            foreach (var shortcut in context.shortcut_option())
            {
                var statements = shortcut.statement();
                if (statements?.Length > 0)
                {
                    RunThroughStatements(statements);
                }
            }

            return 0;
        }

        // in the current block of statements finds any lines that immediately follow an option block and visits them for tagging
        // this works by making our way through each and every statement inside of a block performing the following:
        // 1. assume the current statement is an option block
        // 2. assume the statement before it is a line
        // 3. if both of these hold true we have found a line we need to flag as being before options
        // 4. repeat this process until we run out of statements to check
        // this has the potential to have VERY deep call stacks
        private void RunThroughStatements(YarnSpinnerParser.StatementContext[] statements)
        {
            for (int i = 0; i < statements.Length; i++)
            {
                // if we are an if-block we have to visit it in case there are options and lines inside of that
                // once that is done we can move onto the next statement
                if (statements[i].if_statement() != null)
                {
                    this.Visit(statements[i]);
                    continue;
                }

                // we aren't an option, keep moving
                if (statements[i].shortcut_option_statement() == null)
                {
                    continue;
                }

                // we need to visit the option in case it has embedded statements
                this.Visit(statements[i]);

                // we are an option BUT there isn't a previous statement
                if (i == 0)
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
        }
    }
}
