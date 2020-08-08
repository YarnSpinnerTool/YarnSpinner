using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Text;

namespace Yarn.Compiler.Upgrader
{
    internal class LanguageUpgraderV1 : ILanguageUpgrader
    {
        public ICollection<TextReplacement> Upgrade(string contents, string fileName)
        {
            var replacements = new List<TextReplacement>();

            ICharStream input = CharStreams.fromstring(contents);

            YarnSpinnerV1Lexer lexer = new YarnSpinnerV1Lexer(input);
            CommonTokenStream tokens = new CommonTokenStream(lexer);

            YarnSpinnerV1Parser parser = new YarnSpinnerV1Parser(tokens);

            var tree = parser.dialogue();

            var walker = new ParseTreeWalker();

            var formatFunctionListener = new FormatFunctionListener(contents, parser, (replacement) => replacements.Add(replacement));

            walker.Walk(formatFunctionListener, tree);

            return replacements;
        }

        private class FormatFunctionListener : YarnSpinnerV1ParserBaseListener
        {
            private string contents;
            private YarnSpinnerV1Parser parser;
            private Action<TextReplacement> replacementCallback;

            public FormatFunctionListener(string contents, YarnSpinnerV1Parser parser, Action<TextReplacement> replacementCallback)
            {
                this.contents = contents;
                this.parser = parser;
                this.replacementCallback = replacementCallback;
            }

            public override void ExitFormat_function(YarnSpinnerV1Parser.Format_functionContext context)
            {
                // V1: [select {$gender} male="male" female="female" other="other"]
                //  function_name: "select" variable: "$gender" key_value_pair="male="male"..."
                // V2: [select value={$gender} male="male" female="female" other="other"/]
                var formatFunctionType = context.function_name.Text;
                var variableName = context.variable().GetText();

                StringBuilder sb = new StringBuilder();
                sb.Append($"{formatFunctionType} value={{{variableName}}}");

                foreach (var kvp in context.key_value_pair())
                {
                    sb.Append($" {kvp.GetText()}");
                }

                sb.Append(" /");

                // '[' and ']' are tokens that wrap this format_function,
                // so we're just replacing its innards

                var originalLength = context.Stop.StopIndex + 1 - context.Start.StartIndex;
                var originalStart = context.Start.StartIndex;
                var originalText = this.contents.Substring(originalStart, originalLength);

                var replacement = new TextReplacement()
                {
                    Start = context.Start.StartIndex,
                    StartLine = context.Start.Line,
                    OriginalText = originalText,
                    ReplacementText = sb.ToString(),
                    Comment = "Format functions have been replaced with markup.",
                };

                // Deliver the replacement!
                this.replacementCallback(replacement);
            }
        }
    }

    internal interface ILanguageUpgrader
    {
        ICollection<TextReplacement> Upgrade(string contents, string fileName);
    }
}
