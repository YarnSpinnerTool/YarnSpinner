using System.Collections.Generic;
using Antlr4.Runtime;

namespace YarnLanguageServer
{
    public static class CommandTextSplitter
    {
        public class CommandTextItem {
            public CommandTextItem(string text, int offset)
            {
                Text = text;
                Offset = offset;
            }

            public string Text { get; set; }
            public int Offset { get; set; }
        }

        /// <summary>
        /// Splits input into a number of non-empty sub-strings, separated
        /// by whitespace, and grouping double-quoted strings into a single
        /// sub-string.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <returns>A collection of sub-strings.</returns>
        /// <remarks>
        /// This method behaves similarly to the <see
        /// cref="string.Split(char[], StringSplitOptions)"/> method with
        /// the <see cref="StringSplitOptions"/> parameter set to <see
        /// cref="StringSplitOptions.RemoveEmptyEntries"/>, with the
        /// following differences:
        ///
        /// <list type="bullet">
        /// <item>Text that appears inside a pair of double-quote
        /// characters will not be split.</item>
        ///
        /// <item>Text that appears after a double-quote character and
        /// before the end of the input will not be split (that is, an
        /// unterminated double-quoted string will be treated as though it
        /// had been terminated at the end of the input.)</item>
        ///
        /// <item>When inside a pair of double-quote characters, the string
        /// <c>\\</c> will be converted to <c>\</c>, and the string
        /// <c>\"</c> will be converted to <c>"</c>.</item>
        /// </list>
        /// </remarks>
        public static IEnumerable<CommandTextItem> SplitCommandText(string input, bool addBackInTheQuotes = false)
        {
            var reader = new System.IO.StringReader(input.Normalize());

            int c;

            int currentComponentOffset = 0;

            int position = 0;

            var results = new List<CommandTextItem>();
            var currentComponent = new System.Text.StringBuilder();

            while ((c = reader.Read()) != -1)
            {
                if (char.IsWhiteSpace((char)c))
                {
                    if (currentComponent.Length > 0)
                    {
                        // We've reached the end of a run of visible
                        // characters. Add this run to the result list and
                        // prepare for the next one.
                        results.Add(new CommandTextItem(currentComponent.ToString(), currentComponentOffset));
                        currentComponent.Clear();

                        currentComponentOffset = position + 1;
                    }
                    else
                    {
                        // We encountered a whitespace character, but
                        // didn't have any characters queued up. Skip this
                        // character.
                        currentComponentOffset = position + 1;
                    }

                    position += 1;
                    continue;
                }
                else if (c == '\"')
                {
                    // We've entered a quoted string!
                    while (true)
                    {
                        c = reader.Read();
                        if (c == -1)
                        {
                            // Oops, we ended the input while parsing a
                            // quoted string! Dump our current word
                            // immediately and return.
                            results.Add(new CommandTextItem(currentComponent.ToString(), currentComponentOffset));
                            return results;
                        }
                        else if (c == '\\')
                        {
                            // Possibly an escaped character!
                            var next = reader.Peek();
                            if (next == '\\' || next == '\"')
                            {
                                // It is! Skip the \ and use the character
                                // after it.
                                reader.Read();
                                currentComponent.Append((char)next);
                            }
                            else
                            {
                                // Oops, an invalid escape. Add the \ and
                                // whatever is after it.
                                currentComponent.Append((char)c);
                            }
                        }
                        else if (c == '\"')
                        {
                            // The end of a string!
                            break;
                        }
                        else
                        {
                            // Any other character. Add it to the buffer.
                            currentComponent.Append((char)c);
                        }
                    }

                    var output = addBackInTheQuotes ? $"\"{currentComponent}\"" : currentComponent.ToString();

                    var bork = new CommandTextItem(output, currentComponentOffset);
                    results.Add(bork);
                    
                    currentComponent.Clear();
                    currentComponentOffset = position + 1;
                }
                else
                {
                    currentComponent.Append((char)c);
                }

                position += 1;
            }

            if (currentComponent.Length > 0)
            {
                results.Add(new CommandTextItem(currentComponent.ToString(), currentComponentOffset));
            }

            return results;
        }
    }
}
