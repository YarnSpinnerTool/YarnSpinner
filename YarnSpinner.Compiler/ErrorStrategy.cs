// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

using Antlr4.Runtime;
using Yarn.Compiler;

internal class ErrorStrategy : DefaultErrorStrategy
{
    /// <inheritdoc/>
    protected override void ReportNoViableAlternative(Parser recognizer, NoViableAltException e)
    {
        string msg = null;

        if (this.IsInsideRule<YarnSpinnerParser.If_statementContext>(recognizer)
            && recognizer.RuleContext is YarnSpinnerParser.StatementContext
            && e.StartToken.Type == YarnSpinnerLexer.COMMAND_START
            && e.OffendingToken.Type == YarnSpinnerLexer.COMMAND_ELSE)
        {
            // We are inside an if statement, we're attempting to parse a
            // statement, and we got an '<<', 'else', and we weren't able
            // to match that. The programmer included an extra '<<else>>'.
            _ = this.GetEnclosingRule<YarnSpinnerParser.If_statementContext>(recognizer);

            msg = $"More than one <<else>> statement in an <<if>> statement isn't allowed";
        }
        else if (e.StartToken.Type == YarnSpinnerLexer.COMMAND_START 
            && e.OffendingToken.Type == YarnSpinnerLexer.COMMAND_END)
        {
            // We saw a << immediately followed by a >>. The programmer
            // forgot to include command text.
            msg = $"Command text expected";
        }

        if (msg == null)
        {
            msg = $"Unexpected \"{e.OffendingToken.Text}\" while reading {this.GetFriendlyNameForRuleContext(recognizer.RuleContext, true)}";
        }

        recognizer.NotifyErrorListeners(e.OffendingToken, msg, e);
    }

    /// <inheritdoc/>
    protected override void ReportInputMismatch(Parser recognizer, InputMismatchException e)
    {
        string msg = null;

        switch (recognizer.RuleContext)
        {
            case YarnSpinnerParser.If_statementContext ifStatement:
                if (e.OffendingToken.Type == YarnSpinnerLexer.BODY_END)
                {
                    // We have exited a body in the middle of an if
                    // statement. The programmer forgot to include an
                    // <<endif>>.
                    msg = $"Expected an <<endif>> to match the <<if>> statement on line {ifStatement.Start.Line}";
                }
                else if (e.OffendingToken.Type == YarnSpinnerLexer.COMMAND_ELSE && recognizer.GetExpectedTokens().Contains(YarnSpinnerLexer.COMMAND_ENDIF))
                {
                    // We saw an else, but we expected to see an endif. The
                    // programmer wrote an additional <<else>>.
                    msg = $"More than one <<else>> statement in an <<if>> statement isn't allowed";
                }

                break;
            case YarnSpinnerParser.VariableContext _:
                if (e.OffendingToken.Type == YarnSpinnerLexer.FUNC_ID)
                {
                    // We're parsing a variable (which starts with a '$'),
                    // but we encountered a FUNC_ID (which doesn't). The
                    // programmer forgot to include the '$'.
                    msg = "Variable names need to start with a $";
                }

                break;
        }

        if (msg == null)
        {
            msg = $"Unexpected \"{e.OffendingToken.Text}\" while reading {this.GetFriendlyNameForRuleContext(recognizer.RuleContext, true)}";
        }

        this.NotifyErrorListeners(recognizer, msg, e);
    }

    private bool IsInsideRule<TRuleType>(Parser recognizer)
        where TRuleType : RuleContext
    {
        RuleContext currentContext = recognizer.RuleContext;

        while (currentContext != null)
        {
            if (currentContext.GetType() == typeof(TRuleType))
            {
                return true;
            }

            currentContext = currentContext.Parent;
        }

        return false;
    }

    private TRuleType GetEnclosingRule<TRuleType>(Parser recognizer)
        where TRuleType : RuleContext
    {
        RuleContext currentContext = recognizer.RuleContext;

        while (currentContext != null)
        {
            if (currentContext.GetType() == typeof(TRuleType))
            {
                return currentContext as TRuleType;
            }

            currentContext = currentContext.Parent;
        }

        return null;
    }

    private string GetFriendlyNameForRuleContext(RuleContext context, bool withArticle = false)
    {
        string ruleName = YarnSpinnerParser.ruleNames[context.RuleIndex];

        string friendlyName = ruleName.Replace("_", " ");

        if (withArticle)
        {
            // If the friendly name's first character is a vowel, the
            // article is 'an'; otherwise, 'a'.
            char firstLetter = System.Linq.Enumerable.First(friendlyName);

            string article;

            char[] englishVowels = new[] { 'a', 'e', 'i', 'o', 'u' };

            article = System.Linq.Enumerable.Contains(englishVowels, firstLetter) ? "an" : "a";

            return $"{article} {friendlyName}";
        }
        else
        {
            return friendlyName;
        }
    }
}
