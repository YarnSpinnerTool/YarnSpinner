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

using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Yarn {

    internal class TokeniserException : InvalidOperationException  {

        public int lineNumber;
        public int columnNumber;

        public TokeniserException (string message) : base (message) {}
        public TokeniserException (int lineNumber, int columnNumber, string message)
            : base(string.Format ("{0}:{1}: {2}", lineNumber, columnNumber, message))
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
        }


        public static TokeniserException ExpectedTokensFromState (int lineNumber, int columnNumber, Lexer.LexerState state) {

            var names = new List<string> ();
            foreach (var tokenRule in state.tokenRules) {
                names.Add (tokenRule.type.ToString ());
            }

            string nameList;
            if (names.Count > 1) {
                nameList = String.Join (", ", names.ToArray (), 0, names.Count - 1);
                nameList += ", or " + names [names.Count - 1];
            } else {
                nameList = names [0];
            }

            var message = string.Format ("Expected {0}", nameList);

            return new TokeniserException (lineNumber, columnNumber, message);
        }
    }

    // save some typing, we deal with lists of tokens a LOT
    internal class TokenList : List<Token> {
        // quick constructor to make it easier to create
        // TokenLists with a list of tokens
        public TokenList (params Token[] tokens) : base()
        {
            AddRange(tokens);
        }

    }

    internal enum TokenType {


        // Special tokens
        Whitespace,
        Indent,
        Dedent,
        EndOfLine,
        EndOfInput,

        // Numbers. Everybody loves a number
        Number,

        // Strings. Everybody also loves a string
        String,

        // '#'
        TagMarker,

        // Command syntax ("<<foo>>")
        BeginCommand,
        EndCommand,

        // Variables ("$foo")
        Variable,

        // Shortcut syntax ("->")
        ShortcutOption,

        // Option syntax ("[[Let's go here|Destination]]")
        OptionStart, // [[
        OptionDelimit, // |
        OptionEnd, // ]]

        // Command types (specially recognised command word)
        If,
        ElseIf,
        Else,
        EndIf,
        Set,

        // Boolean values
        True,
        False,

        // The null value
        Null,

        // Parentheses
        LeftParen,
        RightParen,

        // Parameter delimiters
        Comma,

        // Operators
        EqualTo, // ==, eq, is
        GreaterThan, // >, gt
        GreaterThanOrEqualTo, // >=, gte
        LessThan, // <, lt
        LessThanOrEqualTo, // <=, lte
        NotEqualTo, // !=, neq

        // Logical operators
        Or, // ||, or
        And, // &&, and
        Xor, // ^, xor
        Not, // !, not

        // this guy's special because '=' can mean either 'equal to'
        // or 'becomes' depending on context
        EqualToOrAssign, // =, to

        UnaryMinus, // -; this is differentiated from Minus
                    // when parsing expressions

        Add, // +
        Minus, // -
        Multiply, // *
        Divide, // /
        Modulo, // %

        AddAssign, // +=
        MinusAssign, // -=
        MultiplyAssign, // *=
        DivideAssign, // /=

        Comment, // a run of text that we ignore

        Identifier, // a single word (used for functions)

        Text // a run of text until we hit other syntax
    }

    // A parsed token.
    internal class Token {

        // The token itself
        public TokenType type;
        public string value; // optional

        // Where we found this token
        public int lineNumber;
        public int columnNumber;
        public string context;

        public bool delimitsText = false;

        // If this is a function in an expression, this is the number
        // of parameters that were encountered
        public int parameterCount;

        // The state that the lexer was in when this token was emitted
        public string lexerState;

        public Token(TokenType type, Lexer.LexerState lexerState, int lineNumber = -1, int columnNumber = -1, string value=null) {
            this.type = type;
            this.value = value;
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.lexerState = lexerState.name;
        }

        public override string ToString() {
            if (this.value != null) {
                return string.Format("{0} ({1}) at {2}:{3} (state: {4})", type.ToString(), value.ToString(), lineNumber, columnNumber, lexerState);
            } else {
                return string.Format ("{0} at {1}:{2} (state: {3})", type, lineNumber, columnNumber, lexerState);
            }
        }
    }

    internal class Lexer {

        internal class LexerState {

            public string name;

            private Dictionary<TokenType, string> patterns;

            public LexerState (Dictionary<TokenType, string> patterns)
            {
                this.patterns = patterns;
            }

            public List<TokenRule> tokenRules = new List<TokenRule>();

            public TokenRule AddTransition(TokenType type, string entersState = null, bool delimitsText = false) {

                var pattern = string.Format (@"\G{0}", patterns [type]);

                var rule = new TokenRule (type, new Regex(pattern), entersState, delimitsText);

                tokenRules.Add(rule);

                return rule;
            }

            // A "text" rule matches everything that it possibly can, up to ANY of
            // the rules that already exist.
            public TokenRule AddTextRule (TokenType type, string entersState = null)
            {
                if (containsTextRule) {
                    throw new InvalidOperationException ("State already contains a text rule");
                }

                var delimiterRules = new List<string>();

                foreach (var otherRule in tokenRules) {
                    if (otherRule.delimitsText == true)
                        delimiterRules.Add (string.Format ("({0})", otherRule.regex.ToString().Substring(2)));
                }

                // Create a regex that matches all text up to but not including
                // any of the delimiter rules
                var pattern = string.Format (@"\G((?!{0}).)*",
                    string.Join ("|", delimiterRules.ToArray()));

                var rule = AddTransition(type, entersState);

                rule.regex = new Regex (pattern);
                rule.isTextRule = true;

                return rule;
            }

            public bool containsTextRule {
                get {
                    foreach (var rule in tokenRules) {
                        if (rule.isTextRule)
                            return true;
                    }
                    return false;
                }
            }

            public bool setTrackNextIndentation = false;


        }
        internal class TokenRule {
            public Regex regex = null;

            // set to null if it should stay in the same state
            public string entersState;
            public TokenType type;
            public bool isTextRule = false;
            public bool delimitsText = false;

            public TokenRule (TokenType type, Regex regex, string entersState = null, bool delimitsText = false)
            {
                this.regex = regex;
                this.entersState = entersState;
                this.type = type;
                this.delimitsText = delimitsText;
            }

            public override string ToString ()
            {
                return string.Format (string.Format ("[TokenRule: {0} - {1}]", type, this.regex));
            }

        }

        // Single-line comments. If this is encountered at any point, the rest of the line is skipped.
        const string LINE_COMMENT = "//";

        Dictionary<string, LexerState> states;

        LexerState defaultState;
        LexerState currentState;

        // tracks indentation levels, and whether an
        // indent token was emitted for each level
        Stack<KeyValuePair<int,bool>> indentationStack;
        bool shouldTrackNextIndentation;

        public Lexer ()
        {
            CreateStates ();
        }

        void CreateStates ()
        {

            var patterns = new Dictionary<TokenType, string> ();

            patterns[TokenType.Text] = ".*";

            patterns[TokenType.Number] = @"\-?[0-9]+(\.[0-9+])?";
            patterns[TokenType.String] = @"""([^""\\]*(?:\\.[^""\\]*)*)""";
            patterns[TokenType.TagMarker] = @"\#";
            patterns[TokenType.LeftParen] = @"\(";
            patterns[TokenType.RightParen] = @"\)";
            patterns[TokenType.EqualTo] = @"(==|is(?!\w)|eq(?!\w))";
            patterns[TokenType.EqualToOrAssign] = @"(=|to(?!\w))";
            patterns[TokenType.NotEqualTo] = @"(\!=|neq(?!\w))";
            patterns[TokenType.GreaterThanOrEqualTo] = @"(\>=|gte(?!\w))";
            patterns[TokenType.GreaterThan] = @"(\>|gt(?!\w))";
            patterns[TokenType.LessThanOrEqualTo] = @"(\<=|lte(?!\w))";
            patterns[TokenType.LessThan] = @"(\<|lt(?!\w))";
            patterns[TokenType.AddAssign] = @"\+=";
            patterns[TokenType.MinusAssign] = @"\-=";
            patterns[TokenType.MultiplyAssign] = @"\*=";
            patterns[TokenType.DivideAssign] = @"\/=";
            patterns[TokenType.Add] = @"\+";
            patterns[TokenType.Minus] = @"\-";
            patterns[TokenType.Multiply] = @"\*";
            patterns[TokenType.Divide] = @"\/";
            patterns[TokenType.Modulo] = @"\%";
            patterns[TokenType.And] = @"(\&\&|and(?!\w))";
            patterns[TokenType.Or] = @"(\|\||or(?!\w))";
            patterns[TokenType.Xor] = @"(\^|xor(?!\w))";
            patterns[TokenType.Not] = @"(\!|not(?!\w))";
            patterns[TokenType.Variable] = @"\$([A-Za-z0-9_\.])+";
            patterns[TokenType.Comma] = @",";
            patterns[TokenType.True] = @"true(?!\w)";
            patterns[TokenType.False] = @"false(?!\w)";
            patterns[TokenType.Null] = @"null(?!\w)";

            patterns[TokenType.BeginCommand] = @"\<\<";
            patterns[TokenType.EndCommand] = @"\>\>";

            patterns[TokenType.OptionStart] = @"\[\[";
            patterns[TokenType.OptionEnd] = @"\]\]";
            patterns[TokenType.OptionDelimit] = @"\|";

            patterns[TokenType.Identifier] = @"[a-zA-Z0-9_:\.]+";

            patterns[TokenType.If] = @"if(?!\w)";
            patterns[TokenType.Else] = @"else(?!\w)";
            patterns[TokenType.ElseIf] = @"elseif(?!\w)";
            patterns[TokenType.EndIf] = @"endif(?!\w)";
            patterns[TokenType.Set] = @"set(?!\w)";

            patterns[TokenType.ShortcutOption] = @"\-\>";

            states = new Dictionary<string, LexerState> ();

            states ["base"] = new LexerState (patterns);
            states ["base"].AddTransition(TokenType.BeginCommand, "command", delimitsText:true);
            states ["base"].AddTransition(TokenType.OptionStart, "link", delimitsText:true);
            states ["base"].AddTransition(TokenType.ShortcutOption, "shortcut-option");
            states ["base"].AddTransition (TokenType.TagMarker, "tag", delimitsText: true);
            states ["base"].AddTextRule (TokenType.Text);

            states ["tag"] = new LexerState (patterns);
            states ["tag"].AddTransition (TokenType.Identifier, "base");

            states ["shortcut-option"] = new LexerState (patterns);
            states ["shortcut-option"].setTrackNextIndentation = true;
            states ["shortcut-option"].AddTransition (TokenType.BeginCommand, "expression", delimitsText: true);
            states ["shortcut-option"].AddTransition (TokenType.TagMarker, "shortcut-option-tag", delimitsText: true);
            states ["shortcut-option"].AddTextRule (TokenType.Text, "base");

            states ["shortcut-option-tag"] = new LexerState (patterns);
            states ["shortcut-option-tag"].AddTransition (TokenType.Identifier, "shortcut-option");

            states ["command"] = new LexerState (patterns);
            states ["command"].AddTransition (TokenType.If, "expression");
            states ["command"].AddTransition (TokenType.Else);
            states ["command"].AddTransition (TokenType.ElseIf, "expression");
            states ["command"].AddTransition (TokenType.EndIf);
            states ["command"].AddTransition (TokenType.Set, "assignment");
            states ["command"].AddTransition (TokenType.EndCommand,  "base", delimitsText: true);
            states ["command"].AddTransition (TokenType.Identifier, "command-or-expression");
            states ["command"].AddTextRule (TokenType.Text);

            states ["command-or-expression"] = new LexerState (patterns);
            states ["command-or-expression"].AddTransition (TokenType.LeftParen, "expression");
            states ["command-or-expression"].AddTransition (TokenType.EndCommand, "base", delimitsText:true);
            states ["command-or-expression"].AddTextRule (TokenType.Text);

            states ["assignment"] = new LexerState (patterns);
            states ["assignment"].AddTransition(TokenType.Variable);
            states ["assignment"].AddTransition(TokenType.EqualToOrAssign, "expression");
            states ["assignment"].AddTransition(TokenType.AddAssign, "expression");
            states ["assignment"].AddTransition(TokenType.MinusAssign, "expression");
            states ["assignment"].AddTransition(TokenType.MultiplyAssign, "expression");
            states ["assignment"].AddTransition(TokenType.DivideAssign, "expression");

            states ["expression"] = new LexerState (patterns);
            states ["expression"].AddTransition(TokenType.EndCommand, "base");
            states ["expression"].AddTransition(TokenType.Number);
            states ["expression"].AddTransition(TokenType.String);
            states ["expression"].AddTransition(TokenType.LeftParen);
            states ["expression"].AddTransition(TokenType.RightParen);
            states ["expression"].AddTransition(TokenType.EqualTo);
            states ["expression"].AddTransition(TokenType.EqualToOrAssign);
            states ["expression"].AddTransition(TokenType.NotEqualTo);
            states ["expression"].AddTransition(TokenType.GreaterThanOrEqualTo);
            states ["expression"].AddTransition(TokenType.GreaterThan);
            states ["expression"].AddTransition(TokenType.LessThanOrEqualTo);
            states ["expression"].AddTransition(TokenType.LessThan);
            states ["expression"].AddTransition(TokenType.Add);
            states ["expression"].AddTransition(TokenType.Minus);
            states ["expression"].AddTransition(TokenType.Multiply);
            states ["expression"].AddTransition(TokenType.Divide);
            states ["expression"].AddTransition (TokenType.Modulo);
            states ["expression"].AddTransition(TokenType.And);
            states ["expression"].AddTransition(TokenType.Or);
            states ["expression"].AddTransition(TokenType.Xor);
            states ["expression"].AddTransition(TokenType.Not);
            states ["expression"].AddTransition(TokenType.Variable);
            states ["expression"].AddTransition(TokenType.Comma);
            states ["expression"].AddTransition(TokenType.True);
            states ["expression"].AddTransition(TokenType.False);
            states ["expression"].AddTransition(TokenType.Null);
            states ["expression"].AddTransition(TokenType.Identifier);

            states ["link"] = new LexerState (patterns);
            states ["link"].AddTransition (TokenType.OptionEnd, "base", delimitsText:true);
            states ["link"].AddTransition (TokenType.OptionDelimit, "link-destination", delimitsText:true);
            states ["link"].AddTextRule (TokenType.Text);

            states ["link-destination"] = new LexerState (patterns);
            states ["link-destination"].AddTransition (TokenType.Identifier);
            states ["link-destination"].AddTransition (TokenType.OptionEnd, "base");

            defaultState = states ["base"];

            // Make all states aware of their names
            foreach (KeyValuePair<string, LexerState> entry in states) {
                entry.Value.name = entry.Key;
            }
        }

        public TokenList Tokenise (string title, string text)
        {

            // Do some initial setup
            indentationStack = new Stack<KeyValuePair<int,bool>> ();
            indentationStack.Push (new KeyValuePair<int, bool>(0, false));
            shouldTrackNextIndentation = false;

            var tokens = new TokenList();

            currentState = defaultState;

            // Parse each line
            var lines = new List<string>(text.Split ('\n'));
            // Add a blank line to ensure that we end with zero indentation
            lines.Add("");

            int lineNumber = 1;

            foreach (var line in lines) {
                tokens.AddRange (this.TokeniseLine (line, lineNumber));
                lineNumber++;
            }

            var endOfInput = new Token (TokenType.EndOfInput, currentState, lineNumber, 0);
            tokens.Add (endOfInput);

            return tokens;
        }

        TokenList TokeniseLine (string line, int lineNumber)
        {
            var lineTokens = new Stack<Token> ();

            // Replace tabs with four spaces
            line = line.Replace ("\t", "    ");

            // Strip out \r's
            line = line.Replace("\r", "");

            // Record the indentation level if the previous state wants us to

            var thisIndentation = LineIndentation (line);
            var previousIndentation = indentationStack.Peek ();

            if (shouldTrackNextIndentation && thisIndentation > previousIndentation.Key) {
                // If we are more indented than before, emit an
                // indent token and record this new indent level
                indentationStack.Push (new KeyValuePair<int, bool>(thisIndentation, true));

                var indent = new Token (TokenType.Indent, currentState, lineNumber, previousIndentation.Key);
                indent.value = "".PadLeft (thisIndentation - previousIndentation.Key);

                shouldTrackNextIndentation = false;

                lineTokens.Push (indent);

            } else if (thisIndentation < previousIndentation.Key) {

                // If we are less indented, emit a dedent for every
                // indentation level that we passed on the way back to 0 that also
                // emitted an indentation token.
                // at the same time, remove those indent levels from the stack

                while (thisIndentation < indentationStack.Peek ().Key) {

                    var topLevel = indentationStack.Pop ();

                    if (topLevel.Value) {
                        var dedent = new Token (TokenType.Dedent, currentState, lineNumber, 0);
                        lineTokens.Push (dedent);
                    }

                }
            }

            // Now that we're past any initial indentation, start
            // finding tokens.
            int columnNumber = thisIndentation;

            var whitespace = new Regex (@"\s*");

            while (columnNumber < line.Length) {

                // If we're about to hit a line comment, abort processing line
                // immediately
                if (line.Substring(columnNumber).StartsWith(LINE_COMMENT)) {
                    break;
                }

                var matched = false;

                foreach (var rule in currentState.tokenRules) {

                    var match = rule.regex.Match (line, columnNumber);

                    if (match.Success == false || match.Length == 0)
                        continue;

                    string tokenText;

                    if (rule.type == TokenType.Text) {
                        // if this is text, then back up to the most recent text
                        // delimiting token, and treat everything from there as
                        // the text.
                        // we do this because we don't want this:
                        //    <<flip Harley3 +1>>
                        // to get matched as this:
                        //    BeginCommand Identifier("flip") Text("Harley3 +1") EndCommand
                        // instead, we want to match it as this:
                        //    BeginCommand Text("flip Harley3 +1") EndCommand

                        int textStartIndex = thisIndentation;

                        if (lineTokens.Count > 0) {
                            while (lineTokens.Peek().type == TokenType.Identifier) {
                                lineTokens.Pop ();
                            }

                            var startDelimiterToken = lineTokens.Peek ();
                            textStartIndex = startDelimiterToken.columnNumber;
                            if (startDelimiterToken.type == TokenType.Indent)
                                textStartIndex += startDelimiterToken.value.Length;
                            if (startDelimiterToken.type == TokenType.Dedent)
                                textStartIndex = thisIndentation;
                        }

                        columnNumber = textStartIndex;

                        var textEndIndex = match.Index + match.Length;

                        tokenText = line.Substring (textStartIndex, textEndIndex-textStartIndex);

                    } else {
                        tokenText = match.Value;
                    }

                    columnNumber += tokenText.Length;

                    // If this was a string, lop off the quotes at the start and
                    // end, and un-escape the quotes and slashes
                    if (rule.type == TokenType.String) {
                        tokenText = tokenText.Substring (1, tokenText.Length - 2);

                        tokenText = tokenText.Replace (@"\\", @"\");
                        tokenText = tokenText.Replace (@"\""", @"""");
                    }

                    var token = new Token (rule.type, currentState, lineNumber, columnNumber, tokenText);

                    token.delimitsText = rule.delimitsText;

                    lineTokens.Push (token);

                    if (rule.entersState != null) {
                        if (states.ContainsKey(rule.entersState) == false) {
                            throw new TokeniserException (lineNumber, columnNumber, "Unknown tokeniser state " + rule.entersState);
                        }

                        EnterState (states [rule.entersState]);

                        if (shouldTrackNextIndentation == true) {
                            if (indentationStack.Peek().Key < thisIndentation) {
                                indentationStack.Push (new KeyValuePair<int, bool>(thisIndentation, false));
                            }

                        }
                    }

                    matched = true;

                    break;
                }

                if (matched == false) {

                    throw TokeniserException.ExpectedTokensFromState (lineNumber, columnNumber, currentState);
                }

                // consume any lingering whitespace before the next token
                var lastWhitespace = whitespace.Match(line, columnNumber);
                if (lastWhitespace != null) {
                    columnNumber += lastWhitespace.Length;
                }

            }

            var listToReturn = new TokenList (lineTokens.ToArray ());
            listToReturn.Reverse ();

            return listToReturn;

        }

        int LineIndentation(string line)
        {
            var initialIndentRegex = new Regex (@"^(\s*)");
            var match = initialIndentRegex.Match (line);

            if (match == null || match.Groups [0] == null) {
                return 0;
            }

            return match.Groups [0].Length;
        }

        void EnterState(LexerState state) {
            currentState = state;

            if (currentState.setTrackNextIndentation)
                shouldTrackNextIndentation = true;
        }

    }
}

