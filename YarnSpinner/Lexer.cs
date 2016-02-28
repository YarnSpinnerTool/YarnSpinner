/*

The MIT License (MIT)

Copyright (c) 2015 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

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
		public TokeniserException (string message) : base (message) {}
	}

	// save some typing, we deal with lists of tokens a LOT
	internal class TokenList : List<Token> {}

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
		public object value; // optional

		// Where we found this token
		public int lineNumber;
		public int columnNumber;
		public string context;

		// For <<, we record everything up to the next >> on the line,
		// so that the parser can get it back for custom commands
		public string associatedRawText;

		// If this is a function in an expression, this is the number
		// of parameters that were encountered
		public int parameterCount;
		
		public Token(TokenType type, object value=null) {
			this.type = type;
			this.value = value;
		}
		
		public override string ToString() {
			if (this.value != null) {
				return string.Format("{0} ({1}) at {2}:{3}", type.ToString(), value.ToString(), lineNumber, columnNumber);
			} else {
				return string.Format ("{0} at {1}:{2}", type, lineNumber, columnNumber);
			}
		}
	}


	// Lean, mean, string-readin' machine
	internal class Tokeniser {

		// A defined rule for matching a type of token
		public class TokenRule {

			public enum FreeTextMode {
				Begins,
				Ends,
				DoNotMatch,
				AllowMatch
			}

			public delegate bool FreeTextModeException( TokenList tokens );

			// If the tokenizer is in free text mode and this token would normally
			// not be matched (ie FreeTextMode == .DoNotMatch), an exception is made
			// if this function returns true.
			// This exists to allow tokens to make context-sensitive choices, such as
			// allowing OptionDelimit to be matched but only if we started an OptionStart 
			// two tokens ago.
			// TODO: I can see this slowly turning into a headache. Is there a better way
			// around this problem?
			public FreeTextModeException freeTextModeException;

			public TokenType type; // what token is this rule for?
			public Regex regex; // what should we look for?
			public bool discard; // should we throw away this token if we match it?

			// Some tokens are common English words (like "not", "and", "or") - if these
			// are the first word in what's otherwise a line of text,
			// the lexer will read it as "<AND> <TEXT>" instead of "<TEXT>".
			// So, we need to mark certain rules as "this token can start a line"
			public bool canBeginLine; 

			// This flag exists because we DON'T want to interpret lines like this:
			// "-> And what did they say?"
			// as <ShortcutOption> <And> <Text>. Instead, we say that certain tokens
			// "reset" the line, making the lexer treat the next token as the "start"
			// of the line - which means that tokens like "and" will not be interpreted.
			public bool resetsLine;

			// Free text mode means that only certain tokens are matched. It's
			// begun when a token is matched whose freeTextMode is Begins, and ends when
			// a token is matched whose freeTextMode is Ends (typically '<<' and newlines)
			// When in free text mode, token rules that are DoNotMatch are not used.
			public FreeTextMode freeTextMode;

			public override string ToString ()
			{
				return string.Format ("TokenRule: {0}", type.ToString());
			}
		}
		
		// The list of all known token types
		List<TokenRule> tokenRules = new List<TokenRule>();

		public Tokeniser() {

			// Load the token rules for this language
			PrepareTokenRules();

			// Ensure that all token types have a rule:
			#if DEBUG
			// First, obtain the string names of all the elements within myEnum 
			String[] tokenNames = Enum.GetNames( typeof( TokenType ) );
			
			// Obtain the values of all the elements within myEnum 
			TokenType[] values = (TokenType[])Enum.GetValues( typeof( TokenType ) );

			// Check to ensure that we've got a rule for every token type
			for ( int i = 0; i < tokenNames.Length; i++ )
			{
				bool found = false;
				foreach (var tokenRule in tokenRules) {
					if (tokenRule.type == values[i]) {
						// Found a match, carry on
						found = true;
					}
				}
				// noooooo we forgot to add a rule in PrepareTokenRules()
				if (found == false)
					throw new TokeniserException("Missing rule for token type " + tokenNames[i]);
			}
			#endif


		}
		
		// Define a new token rule, with a name and an optional rule
		public TokenRule AddTokenRule(TokenType type, string rule, 
			bool canBeginLine = false, 
			bool resetsLine = false, 
			bool requiresFollowingWhitespace=false,
			TokenRule.FreeTextMode freeTextMode = TokenRule.FreeTextMode.DoNotMatch	
		) {
			
			var newTokenRule = new TokenRule();
			newTokenRule.type = type;

			if (requiresFollowingWhitespace) {
				// Look for whitespace following this rule, but don't capture it as part of
				// the token
				rule += @"(?=\s+)";
			}

			// Set up a regex if we have a rule for it
			if (rule != null) {
				
				newTokenRule.regex = new Regex(rule);
			}
			newTokenRule.resetsLine = resetsLine;
			newTokenRule.canBeginLine = canBeginLine;

			newTokenRule.freeTextMode = freeTextMode;
			
			// Store it in the list and return
			tokenRules.Add(newTokenRule);
			return newTokenRule;
		}


		// Given an input string, parse it and return the list of tokens
		public TokenList Tokenise(string context, string input) {
			
			// The total collection of all tokens in this input
			var tokens = new TokenList();
			
			// Start by chopping up the input into lines
			var lines = input.Split(new char[] {'\n','\r'} , StringSplitOptions.None);

			// Keep track of which column each new indent started
			var indentLevels = new Stack<int>();
			
			// Start at indent 0
			indentLevels.Push(0);

			var lineNum = 0;
			foreach (var line in lines) {
				
				int newIndentLevel;
				
				// Get the tokens, plus the indentation level of this line
				var lineTokens = TokeniseLine(context, line, out newIndentLevel, lineNum);
				
				if (newIndentLevel > indentLevels.Peek()) {
					// We are now more indented than the last indent.
					// Emit a "indent" token, and push this new indent onto the stack.
					var indent = new Token(TokenType.Indent);
					indent.lineNumber = lineNum;
					indent.context = context;
					tokens.Add(indent);
					indentLevels.Push(newIndentLevel);
				} else if (newIndentLevel < indentLevels.Peek()) {
					// We are less indented than the last indent.
					// We may have dedented more than a single indent level, though, so
					// check this against all indent levels we know about
					
					while (newIndentLevel < indentLevels.Peek()) {
						// We've gone down an indent, holy crap, dedent it!
						var dedent = new Token(TokenType.Dedent);
						dedent.lineNumber = lineNum;
						tokens.Add(dedent);
						dedent.context = context;
						indentLevels.Pop();
					}
				}
				
				// Add the list of tokens that were in this line
				tokens.AddRange(lineTokens);

				// Update line number
				lineNum++;
				
			}

			// Back up a line - lineNum is now 1 more than the number
			// of physical lines in the text
			lineNum--;

			// Dedent if there's any indentations left (ie we reached the 
			// end of the file and it was still indented)
			// (we stop at the second-last one because we pushed 'indent 0' at the start,
			// and popping that would emit an unbalanced dedent token
			while (indentLevels.Count > 1) {
				indentLevels.Pop();
				var dedent = new Token(TokenType.Dedent);
				dedent.lineNumber = lineNum;
				dedent.context = context;
				tokens.Add(dedent);
			}

			// Emit the end-of-input token
			var endOfInput = new Token (TokenType.EndOfInput);
			endOfInput.lineNumber = lineNum;

			// Finish up with an ending token
			tokens.Add(endOfInput);

			// yay we're done
			return tokens;
		}

		// Tokenise a single line, and also report on how indented this line is
		private TokenList TokeniseLine(string context, string input, out int lineIndentation, int lineNumber) {
			
			// The tokens we found on this line
			var tokens = new TokenList();

			// Replace tabs with four spaces
			input = input.Replace ("\t", "    ");

			bool freeTextMode = false;

			// Find any whitespace at the start of a line
			var initialIndentRule = new Regex("^\\s+");
			
			// If there's whitespace at the start of the line, this line is indented
			if (initialIndentRule.IsMatch(input)) {
				// Record how indented this line is
				lineIndentation = initialIndentRule.Match(input).Length;
			} else {
				// There's no whitespace at the start of the line,
				// so this line's indentation level is zero.
				lineIndentation = 0;
			}
			
			// Keeps track of how much of the line we have left to parse
			int columnNumber = lineIndentation;

			// Are we at the start of the line? ie do we disregard token rules that
			// can't be at the start of a line?
			bool startOfLine = true;

			// While we have text left to parse in this line..
			while (columnNumber < input.Length) {
				
				// Keep track of whether we successfully found a rule to parse the next token
				var matched = false;

				// Check each rule to see if it matches
				foreach (var tokenRule in tokenRules) {
					
					// Is the next chunk of text a token?
					if (tokenRule.regex != null) {

						// Attempt to match this
						var match = tokenRule.regex.Match(input, columnNumber);

						// Bail out if this either failed to match, or matched
						// further out in the text
						if (match.Success == false || match.Index > columnNumber)
							continue;

						// Bail out if this is the first token and we aren't allowed to
						// match this rule at the start
						if (tokenRule.canBeginLine == false && startOfLine == true) {
							continue;
						}

						// Bail out if this match was zero-length
						if (match.Length == 0) {
							continue;
						}

						switch (tokenRule.freeTextMode) {
						case TokenRule.FreeTextMode.Begins:
							freeTextMode = true;
							break;
						case TokenRule.FreeTextMode.Ends:
							freeTextMode = false;
							break;
						case TokenRule.FreeTextMode.DoNotMatch:
							if (freeTextMode == true) {
								// Do not match, UNLESS we should make an exception
								if (tokenRule.freeTextModeException == null || 
									tokenRule.freeTextModeException(tokens) == false) {
									continue;
								}
							}
							break;
						}

						// Record the token only if we care
						// about it (ie it's not whitespace)
						if (tokenRule.discard == false) {
							Token token;
							
							// If this token type's rule had a capture group,
							// store that
							if (match.Captures.Count > 0) {
								token = new Token(tokenRule.type, match.Captures[0].Value);
							} else {
								token = new Token(tokenRule.type);
							}

							// If this was a string, lop off the quotes at the start and
							// end, and un-escape the quotes and slashes
							if (token.type == TokenType.String) {
								string processedString = token.value as string;
								processedString = processedString.Substring (1, processedString.Length - 2);

								processedString = processedString.Replace ("\\\\", "\\");
								processedString = processedString.Replace ("\\\"", "\"");
								token.value = processedString;
							}

							// Record where the token was found
							token.lineNumber = lineNumber;
							token.columnNumber = columnNumber;
							token.context = context;

							// Add it to the token stream
							tokens.Add(token);

							// If this is a token that 'resets' the fact
							// that we're at the start of the line (like '->' does),
							// then record that; otherwise, record that we're past
							// the start of the line and are now allowed to start
							// matching additional types of tokens
							if (tokenRule.resetsLine) {
								startOfLine = true;
							} else {
								startOfLine = false;
							}
							
						}

						// We've advanced through the string
						columnNumber += match.Length;

						// Record that we successfully found a type for this token
						matched = true;

						// We've matched a token type, stop trying to
						// match it against others
						break;
					}
				}
				
				if (matched == false) {
					// We've exhausted the list of possible token types, so we've
					// failed to interpret this string! Bail out!

					throw new InvalidOperationException("Failed to interpret token " + input);
				}
			}

			// Merge multiple runs of text on the same line
			var tokensToReturn = new TokenList();

			foreach (var token in tokens)  {
				
				Token lastToken = null;

				// Did we previously add a token?
				if (tokensToReturn.Count > 0) {
					lastToken = tokensToReturn [tokensToReturn.Count - 1];
				}

				// Was the last token in tokensToReturn a Text, AND
				// this token is a Text?
				if (lastToken != null &&
					lastToken.type == TokenType.Text && 
					token.type == TokenType.Text) {

					// Merge the texts!
					var str = (string)tokensToReturn[tokensToReturn.Count - 1].value;
					str += (string)token.value;
					lastToken.value = str;
				} else {
					tokensToReturn.Add (token);
				}
			}

			// Attach text between << and >> to the << token
			for (int i = 0; i < tokensToReturn.Count; i++) {
				if (i == tokensToReturn.Count - 1) {
					// don't bother checking if we're the last token in the line
					continue;
				}
				var startToken = tokensToReturn[i];
				if (startToken.type == TokenType.BeginCommand) {
					int startIndex = tokensToReturn[i+1].columnNumber;
					int endIndex = -1;
					// Find the next >> token
					for (int j = i; j < tokensToReturn.Count; j++) {
						var endToken = tokensToReturn [j];
						if (endToken.type == TokenType.EndCommand) {
							endIndex = endToken.columnNumber;
							break;
						}
					}

					if (endIndex != -1) {
						var text = input.Substring (startIndex, endIndex - startIndex);
						startToken.associatedRawText = text;
					}

				}
			}

			// Return the list of tokens we found
			return tokensToReturn;
		}

		// Prepare the regex rules for each possible token.
		private void PrepareTokenRules() {

			// The order of these rules is important - rules that were added first
			// get matched first.

			// Set up the whitespace token, which is discarded
			AddTokenRule(TokenType.Whitespace, "\\s+", canBeginLine:true)
				.discard = true;
			
			// Set up the special begin and end indentation tokens - 
			// these aren't matched by regexes, but rather emitted
			// during lexing by keeping track of the number of spaces
			AddTokenRule(TokenType.Indent, null, canBeginLine:true);
			AddTokenRule(TokenType.Dedent, null, canBeginLine:true);
			
			// Set up the end-of-line token
			AddTokenRule (TokenType.EndOfLine, @"\n", canBeginLine:true)
				.discard = true;
			
			// Set up the end-of-file token
			AddTokenRule(TokenType.EndOfInput, null, freeTextMode:TokenRule.FreeTextMode.Ends);

			// Comments
			AddTokenRule (TokenType.Comment, @"\/\/.*", canBeginLine: true, freeTextMode: TokenRule.FreeTextMode.AllowMatch)
				.discard = true;

			// Basic syntax
			AddTokenRule(TokenType.Number, @"((?<!\[\[)\d+)");
			AddTokenRule(TokenType.BeginCommand, @"\<\<", canBeginLine:true,  freeTextMode: TokenRule.FreeTextMode.Ends);
			AddTokenRule(TokenType.EndCommand, @"\>\>");
			AddTokenRule(TokenType.Variable, @"\$([A-z\d])+");

			// Strings
			AddTokenRule(TokenType.String, @"""[^""\\]*(?:\\.[^""\\]*)*""");
			
			// Options
			AddTokenRule(TokenType.ShortcutOption, @"-\>", canBeginLine:true, resetsLine:true);
			AddTokenRule(TokenType.OptionStart, @"\[\[", canBeginLine:true, resetsLine:true);
			AddTokenRule (TokenType.OptionDelimit, @"\|").freeTextModeException = delegate(TokenList tokens) {
				if (tokens.Count < 2)
					return false;

				// permitted in free text mode when we saw an OptionStart two tokens ago
				switch (tokens [tokens.Count - 2].type) {
				case TokenType.OptionStart:
					return true;
				default:
					return false;
				}
			};
			AddTokenRule(TokenType.OptionEnd, @"\]\]").freeTextModeException = delegate(TokenList tokens) {
				if (tokens.Count < 2)
					return false;
				
				// permitted in free text mode when we saw either an OptionDelimit or an OptionStart two tokens ago
				switch (tokens [tokens.Count - 2].type) {
				case TokenType.OptionStart:
				case TokenType.OptionDelimit:
					return true;
				default:
					return false;
				}
			};
			
			// Reserved words
			AddTokenRule(TokenType.If, @"if(?!\w)");
			AddTokenRule(TokenType.ElseIf, @"elseif(?!\w)");
			AddTokenRule(TokenType.Else, @"else(?!\w)");
			AddTokenRule(TokenType.EndIf, "endif(?!\\w)");

			// "set" needs following whitespace to avoid matching against 
			// words like "settings"
			AddTokenRule(TokenType.Set, "set", requiresFollowingWhitespace:true);

			// Boolean values
			AddTokenRule(TokenType.True, "true");
			AddTokenRule(TokenType.False, "false");

			// Null
			AddTokenRule(TokenType.Null, @"null(?!\w)");
			
			// Operators
			AddTokenRule(TokenType.EqualTo, @"(==|eq(?!\w)|is(?!\w))");
			AddTokenRule(TokenType.GreaterThanOrEqualTo, @"(\>=|gte(?!\w))");
			AddTokenRule(TokenType.LessThanOrEqualTo, @"(\<=|lte(?!\w))");
			AddTokenRule(TokenType.GreaterThan, @"(\>|gt(?!\w))");
			AddTokenRule(TokenType.LessThan, @"(\<|lt(?!\w))");
			AddTokenRule(TokenType.NotEqualTo, @"(\!=|neq)");

			AddTokenRule(TokenType.And, @"(\&\&|and(?:\s))");
			AddTokenRule(TokenType.Or, @"(\|\||or(?!\w))");
			AddTokenRule(TokenType.Xor, @"(\^|xor(?!\w))");
			AddTokenRule(TokenType.Not, @"(\!|not(?!\w))");

			// Assignment operators
			AddTokenRule (TokenType.EqualToOrAssign, @"(=|to(?!\w))");

			AddTokenRule(TokenType.AddAssign, @"\+=");
			AddTokenRule(TokenType.MinusAssign, "-=");
			AddTokenRule(TokenType.MultiplyAssign, @"\*=");
			AddTokenRule(TokenType.DivideAssign, @"\/=");

			AddTokenRule(TokenType.Add, @"\+");
			AddTokenRule(TokenType.Minus, "-");
			AddTokenRule(TokenType.Multiply, @"\*");
			AddTokenRule(TokenType.Divide, @"\/");

			AddTokenRule (TokenType.UnaryMinus, null); // this one has special matching rules

			AddTokenRule (TokenType.LeftParen, @"\(");
			AddTokenRule (TokenType.RightParen, @"\)");

			// Identifiers (any letter, number, period or underscore)
			AddTokenRule (TokenType.Identifier, @"(\w|_|\.)+");

			// Commas for separating function parameters
			AddTokenRule (TokenType.Comma, ",");

			// Free text - match anything except BeginCommand, OptionsDelimit or OptionsEnd
			// This always goes last so that anything else will preferably
			// match it
			AddTokenRule(TokenType.Text, @"(\||\<\<|\]\])*(?:(?!\<\<|\||\]\]).)*", canBeginLine:true, freeTextMode: TokenRule.FreeTextMode.Begins);
		}
	}
}

