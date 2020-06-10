/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal class LineParser
    {

        // A string used to mark where a value should be injected in a
        // format function. Generated during format function parsing; not
        // typed by a human.
        private const string FormatFunctionValuePlaceholder = "<VALUE PLACEHOLDER>";

        internal static MarkupParseResult ParseMarkup(string line)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Expands all [format functions]({{|ref
        /// "syntax.md#format-functions"|}}) in a given string, using
        /// pluralisation rules specified by the given locale. 
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <param name="localeCode">The locale code, as an IETF BCP-47
        /// language tag, to use when determining the plural categories of
        /// numbers.</param>
        /// <returns>The original string, with any format functions
        /// replaced with their evaluated versions.</returns>
        /// <throws cref="ArgumentException">Thrown when the string
        /// contains a `plural` or `ordinal` format function, but the
        /// specified value cannot be parsed as a number.</throws>
        internal static string ExpandFormatFunctions(string input, string localeCode)
        {
            ParseFormatFunctions(input, out var lineWithReplacements, out var formatFunctions);

            for (int i = 0; i < formatFunctions.Length; i++)
            {
                ParsedFormatFunction function = formatFunctions[i];

                // Apply the "select" format function
                if (function.functionName == "select")
                {
                    if (function.data.TryGetValue(function.value, out string replacement) == false)
                    {
                        replacement = $"<no replacement for {function.value}>";
                    }

                    // Insert the value if needed
                    replacement = replacement.Replace(FormatFunctionValuePlaceholder, function.value);

                    lineWithReplacements = lineWithReplacements.Replace("{" + i + "}", replacement);
                }
                else
                {
                    // Apply the "plural" or "ordinal" format function

                    if (double.TryParse(function.value, out var value) == false)
                    {
                        throw new ArgumentException($"Error while pluralising line '{input}': '{function.value}' is not a number");
                    }

                    CLDRPlurals.PluralCase pluralCase;

                    switch (function.functionName)
                    {
                        case "plural":
                            pluralCase = CLDRPlurals.NumberPlurals.GetCardinalPluralCase(localeCode, value);
                            break;
                        case "ordinal":
                            pluralCase = CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(localeCode, value);
                            break;
                        default:
                            throw new ArgumentException($"Unknown formatting function '{function.functionName}' in line '{input}'");
                    }

                    if (function.data.TryGetValue(pluralCase.ToString().ToLowerInvariant(), out string replacement) == false)
                    {
                        replacement = $"<no replacement for {function.value}>";
                    }

                    // Insert the value if needed
                    replacement = replacement.Replace(FormatFunctionValuePlaceholder, function.value);

                    lineWithReplacements = lineWithReplacements.Replace("{" + i + "}", replacement);

                }
            }
            return lineWithReplacements;
        }

        

        internal static void ParseFormatFunctions(string input, out string lineWithReplacements, out ParsedFormatFunction[] parsedFunctions)
        {

            var stringBuilder = new System.Text.StringBuilder();

            var returnedFunctions = new List<ParsedFormatFunction>();

            int next;

            LineParser parser = new LineParser(input);

            // Read the entirety of the line
            while ((next = parser.stringReader.Read()) != -1)
            {
                char c = (char)next;

                if (c != '[')
                {
                    // plain text!
                    stringBuilder.Append(c);
                }
                else
                {
                    // the start of a format function!

                    ParsedFormatFunction function = parser.ParseFormatFunction(parser);

                    // add a placeholder for this function's value
                    stringBuilder.Append("{" + returnedFunctions.Count + "}");

                    // and reached the end of this function; add it to the
                    // list
                    returnedFunctions.Add(function);
                }
            }

            lineWithReplacements = stringBuilder.ToString();
            parsedFunctions = returnedFunctions.ToArray();
        }

        private ParsedFormatFunction ParseFormatFunction(LineParser parser)
        {
            
            ParsedFormatFunction function = new ParsedFormatFunction();

            // Structure of a format function:
            // [ name "value" key1="value1" key2="value2" ]

            // Read the name
            function.functionName = parser.ExpectID();

            // Ensure that only valid function names are used
            switch (function.functionName)
            {
                case "select":
                    break;
                case "plural":
                    break;
                case "ordinal":
                    break;
                default:
                    throw new ArgumentException($"Invalid formatting function {function.functionName} in line \"{input}\"");
            }

            function.value = parser.ExpectString();

            function.data = new Dictionary<string, string>();

            // parse and read the data for this format function
            while (true)
            {
                parser.ConsumeWhitespace();

                var peek = parser.stringReader.Peek();
                if ((char)peek == ']')
                {
                    // we're done adding parameters
                    break;
                }

                // this is a key-value pair
                var key = parser.ExpectID();
                parser.ExpectCharacter('=');
                var value = parser.ExpectString();

                if (function.data.ContainsKey(key))
                {
                    throw new ArgumentException($"Duplicate value '{key}' in format function inside line \"{input}\"");
                }

                function.data.Add(key, value);

            }

            // We now expect the end of this format function
            parser.ExpectCharacter(']');

            return function;
        }

        private string ExpectID()
        {
            ConsumeWhitespace();
            var idStringBuilder = new StringBuilder();

            // Read the first character, which must be a letter
            int tempNext = stringReader.Read();
            AssertNotEndOfInput(tempNext);
            char nextChar = (char)tempNext;

            if (char.IsLetter(nextChar) || nextChar == '_')
            {
                idStringBuilder.Append((char)tempNext);
            }
            else
            {
                throw new ArgumentException($"Expected an identifier inside a format function in line \"{input}\"");
            }

            // Read zero or more letters, numbers, or underscores
            while (true)
            {
                tempNext = stringReader.Peek();
                if (tempNext == -1)
                {
                    break;
                }
                nextChar = (char)tempNext;
                if (char.IsLetterOrDigit(nextChar) || (char)tempNext == '_')
                {
                    idStringBuilder.Append((char)tempNext);
                    stringReader.Read(); // consume it
                }
                else
                {
                    // no more
                    break;
                }
            }
            return idStringBuilder.ToString();
        }

        private string ExpectString()
        {
            ConsumeWhitespace();

            var stringStringBuilder = new StringBuilder();

            int tempNext = stringReader.Read();
            AssertNotEndOfInput(tempNext);

            char nextChar = (char)tempNext;
            if (nextChar != '"')
            {
                throw new ArgumentException($"Expected a string inside a format function in line {input}");
            }

            while (true)
            {
                tempNext = stringReader.Read();
                AssertNotEndOfInput(tempNext);
                nextChar = (char)tempNext;

                if (nextChar == '"')
                {
                    // end of string - consume it but don't
                    // append to the final collection
                    break;
                }
                else if (nextChar == '\\')
                {
                    // an escaped quote or backslash
                    int nextNext = stringReader.Read();
                    AssertNotEndOfInput(nextNext);
                    int nextNextChar = (char)nextNext;
                    if (nextNextChar == '\\' || nextNextChar == '"' || nextNextChar == '%')
                    {
                        stringStringBuilder.Append(nextNextChar);
                    }
                }
                else if (nextChar == '%')
                {
                    stringStringBuilder.Append(FormatFunctionValuePlaceholder);
                }
                else
                {
                    stringStringBuilder.Append(nextChar);
                }

            }

            return stringStringBuilder.ToString();
        }

        private void ExpectCharacter(char character)
        {
            ConsumeWhitespace();

            int tempNext = stringReader.Read();
            AssertNotEndOfInput(tempNext);
            if ((char)tempNext != character)
            {
                throw new ArgumentException($"Expected a {character} inside a format function in line \"{input}\"");
            }
        }

        private void AssertNotEndOfInput(int value)
        {
            if (value == -1)
            {
                throw new ArgumentException($"Unexpected end of line inside a format function in line \"{input}");
            }
        }

        private void ConsumeWhitespace(bool allowEndOfLine = false)
        {
            while (true)
            {
                var tempNext = stringReader.Peek();
                if (tempNext == -1 && allowEndOfLine == false)
                {
                    throw new ArgumentException($"Unexpected end of line inside a format function in line \"{input}");
                }

                if (char.IsWhiteSpace((char)tempNext) == true)
                {
                    // consume it and continue
                    stringReader.Read();
                }
                else
                {
                    // no more whitespace ahead; don't
                    // consume it, but instead stop eating
                    // whitespace
                    return;
                }
            }
        }

        private string input;
        private StringReader stringReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="LineParser"/> class.
        /// </summary>
        /// <param name="input"></param>
        public LineParser(string input)
        {
            this.input = input;
            this.stringReader = new StringReader(input);
        }
    }
}
