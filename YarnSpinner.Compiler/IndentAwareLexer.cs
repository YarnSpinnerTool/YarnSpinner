// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Antlr4.Runtime;

    /// <summary>
    /// A Lexer subclass that detects newlines and generates indent and
    /// dedent tokens accordingly.
    /// </summary>
    public abstract class IndentAwareLexer : Lexer
    {
        // TODO: come up with a better system
        // pretty sure some of these can vars be rolled up into one another

        /// <summary>
        /// A stack keeping track of the levels of indentations we have
        /// seen so far that are relevant to shortcuts.
        /// </summary>
        private Stack<int> unbalancedIndents = new Stack<int>();

        /// <summary>
        /// Keeps track of the last indentation encounterd.
        /// This is used to see if depth has changed between lines.
        /// </summary>
        private int lastIndent = 0;
        /// <summary>
        /// A flag to say the last line observed was a shortcut or not.
        /// Used to determine if tracking indents needs to occur.
        /// </summary>
        bool lineContainsShortcut = false;
        /// <summary>
        /// Holds the last observed token from the stream.
        /// Used to see if a line is blank or not.
        /// </summary>
        IToken lastToken;

        /// <summary>
        /// holds the line number of the last seen option.
        /// Lets us work out if the blank line needs to end the option.
        /// </summary>
        int lastSeenOptionContent = -1;

        /// <summary>
        /// The collection of tokens that we have seen, but have not yet
        /// returned. This is needed when NextToken encounters a newline,
        /// which means we need to buffer indents or dedents. NextToken
        /// only returns a single <see cref="IToken"/> at a time, which
        /// means we use this list to buffer it.
        /// </summary>
        private readonly Queue<IToken> pendingTokens = new Queue<IToken>();

        /// <summary>
        /// The collection of <see cref="Warning"/> objects we've
        /// generated.
        /// </summary>
        private readonly List<Warning> warnings = new List<Warning>();

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="IndentAwareLexer"/> class.
        /// </summary>
        /// <param name="input">The incoming character stream.</param>
        /// <param name="output">The <see cref="TextWriter"/> to send
        /// output to.</param>
        /// <param name="errorOutput">The <see cref="TextWriter"/> to send
        /// errors to.</param>
        public IndentAwareLexer(ICharStream input, TextWriter output, TextWriter errorOutput)
        : base(input, output, errorOutput)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="IndentAwareLexer"/> class.
        /// </summary>
        /// <param name="input">The incoming character stream.</param>
        protected IndentAwareLexer(ICharStream input)
        : base(input)
        {
        }

        /// <summary>
        /// Gets the collection of warnings determined during lexing.
        /// </summary>
        public IEnumerable<Warning> Warnings { get => this.warnings; }

        /// <inheritdoc/>
        public override IToken NextToken()
        {
            if (this.HitEOF && this.pendingTokens.Count > 0)
            {
                // We have hit the EOF, but we have tokens still pending.
                // Start returning those tokens.
                return this.pendingTokens.Dequeue();
            }
            else if (this.InputStream.Size == 0)
            {
                // There's no more incoming symbols, and we don't have
                // anything pending, so we've hit the end of the file.
                this.HitEOF = true;

                // Return the EOF token.
                return new CommonToken(Eof, "<EOF>");
            }
            else
            {
                // Get the next token, which will enqueue one or more new
                // tokens into the pending tokens queue.
                this.CheckNextToken();

                if (this.pendingTokens.Count > 0)
                {
                    // Then, return a single token from the queue.
                    return this.pendingTokens.Dequeue();
                }
                else
                {
                    // Nothing left in the queue. Return null.
                    return null;
                }
            }
        }

        private void CheckNextToken()
        {
            var currentToken = base.NextToken();

            switch (currentToken.Type)
            {
                case YarnSpinnerLexer.NEWLINE:
                    // Insert indents or dedents depending on the next
                    // token's indentation, and enqueues the newline at the
                    // correct place
                    this.HandleNewLineToken(currentToken);
                    break;

                case Eof:
                    // Insert dedents before the end of the file, and then
                    // enqueues the EOF.
                    this.HandleEndOfFileToken(currentToken);
                    break;

                case YarnSpinnerLexer.SHORTCUT_ARROW:
                    this.pendingTokens.Enqueue(currentToken);
                    lineContainsShortcut = true;
                    break;

                case YarnSpinnerLexer.BODY_END:
                    // we are at the end of the node
                    // depth no longer matters
                    // clear the stack
                    lineContainsShortcut = false;
                    lastIndent = 0;
                    unbalancedIndents.Clear();
                    lastSeenOptionContent = -1;
                    // TODO: this should be empty by now actually...
                    this.pendingTokens.Enqueue(currentToken);
                    break;

                default:
                    this.pendingTokens.Enqueue(currentToken);
                    break;
            }
            lastToken = currentToken;
        }

        private void HandleEndOfFileToken(IToken currentToken)
        {
            // We're at the end of the file. Emit as many dedents as we
            // currently have on the stack.
            while (this.unbalancedIndents.Count > 0)
            {
                var indent = this.unbalancedIndents.Pop();

                // so that we don't end up printing <dedent from 8> into the stream we set the text to be empty
                // I dislike this and need to look into if you can set a debug text setting in ANTLR
                // TODO: see above comment
                // this.InsertToken($"<dedent: {indent}>", YarnSpinnerLexer.DEDENT);
                this.InsertToken(string.Empty, YarnSpinnerLexer.DEDENT);
            }

            // Finally, enqueue the EOF token.
            this.pendingTokens.Enqueue(currentToken);
        }

        private void HandleNewLineToken(IToken currentToken)
        {
            // We're about to go to a new line. Look ahead to see how
            // indented it is.

            // insert the current NEWLINE token
            this.pendingTokens.Enqueue(currentToken);

            int currentIndentationLength = this.GetLengthOfNewlineToken(currentToken);

            // we have seen an option somewhere
            if (lastSeenOptionContent != -1)
            {
                // we are a blank line
                if (currentToken.Type == lastToken?.Type)
                {
                    // is the option content directly above us?
                    if (this.Line - lastSeenOptionContent == 1)
                    {
                        // so that we don't end up printing <ending option group> into the stream we set the text to be empty
                        // I dislike this and need to look into if you can set a debug text setting in ANTLR
                        // TODO: see above comment
                        // this.InsertToken("<ending option group>", YarnSpinnerLexer.BLANK_LINE_FOLLOWING_OPTION);
                        this.InsertToken(string.Empty, YarnSpinnerLexer.BLANK_LINE_FOLLOWING_OPTION);
                    }

                    // disabling the option tracking
                    lastSeenOptionContent = -1;
                }
            }

            // we need to actually see if there is a shortcut *somewhere* above us
            // if there isn't we just chug on without worrying
            if (lineContainsShortcut)
            {
                // we have a shortcut *somewhere* above us
                // that means we need to check our depth
                // and compare it to the shortcut depth

                // if the depth of the current line is greater than the previous one
                // we need to add this depth to the indents stack
                if (currentIndentationLength > lastIndent)
                {
                    unbalancedIndents.Push(currentIndentationLength);
                    // so that we don't end up printing <indent to 8> into the stream we set the text to be empty
                    // I dislike this and need to look into if you can set a debug text setting in ANTLR
                    // TODO: see above comment
                    // this.InsertToken($"<indent to {currentIndentationLength}>", YarnSpinnerLexer.INDENT);
                    this.InsertToken(string.Empty, YarnSpinnerLexer.INDENT);
                }
                // we've now started tracking the indentation, or ignored it, so can turn this off
                lineContainsShortcut = false;
                lastSeenOptionContent = this.Line;
            }

            // now we need to see if the current depth requires any indents or dedents
            // we do this by first checking to see if there are any unbalanced indents
            if (unbalancedIndents.Count > 0)
            {
                var top = unbalancedIndents.Peek();

                // later should make it check if indentation has changed inside the statement block and throw out a warning
                // this.warnings.Add(new Warning { Token = currentToken, Message = "Indentation inside of shortcut block has changed. This is generally a bad idea."});

                // while there are unbalanced indents
                // we need to check if the current line is shallower than the indent stack
                // if it is then we emit a dedent and continue checking
                while (currentIndentationLength < top)
                {
                    // so that we don't end up printing <indent from 8> into the stream we set the text to be empty
                    // I dislike this and need to look into if you can set a debug text setting in ANTLR
                    // TODO: see above comment
                    // this.InsertToken($"<dedent from {top}>", YarnSpinnerLexer.DEDENT);
                    this.InsertToken(string.Empty, YarnSpinnerLexer.DEDENT);

                    unbalancedIndents.Pop();

                    if (unbalancedIndents.Count > 0)
                    {
                        top = unbalancedIndents.Peek();
                    }
                    else
                    {
                        top = 0;
                        // we've dedented all the way out of the shortcut
                        // as such we are done with the option block
                        // previousLineWasOptionOrOptionBlock = false;
                        lastSeenOptionContent = this.Line;
                    }
                }
            }

            // finally we update the last seen depth
            lastIndent = currentIndentationLength;
        }

        // Given a NEWLINE token, return the length of the indentation
        // following it by counting the spaces and tabs after it.
        private int GetLengthOfNewlineToken(IToken currentToken)
        {
            if (currentToken.Type != YarnSpinnerLexer.NEWLINE)
            {
                throw new ArgumentException($"{nameof(this.GetLengthOfNewlineToken)} expected {nameof(currentToken)} to be a {nameof(YarnSpinnerLexer.NEWLINE)} ({YarnSpinnerLexer.NEWLINE}), not {currentToken.Type}");
            }

            int length = 0;
            bool sawSpaces = false;
            bool sawTabs = false;

            foreach (char c in currentToken.Text)
            {
                switch (c)
                {
                    case ' ':
                        length += 1;
                        sawSpaces = true;
                        break;
                    case '\t':
                        sawTabs = true;
                        length += 8;
                        break;
                }
            }

            if (sawSpaces && sawTabs)
            {
                this.warnings.Add(new Warning { Token = currentToken, Message = "Indentation contains tabs and spaces" });
            }

            return length;
        }

        /// <summary>
        /// Inserts a new token with the given text and type, as though it
        /// had appeared in the input stream.
        /// </summary>
        /// <param name="text">The text to use for the token.</param>
        /// <param name="type">The type of the token.</param>
        /// <remarks>The token will have a zero length.</remarks>
        private void InsertToken(string text, int type)
        {
            // ***
            // https://www.antlr.org/api/Java/org/antlr/v4/runtime/Lexer.html#_tokenStartCharIndex
            int startIndex = this.TokenStartCharIndex + this.Text.Length;
            this.InsertToken(startIndex, startIndex - 1, text, type, this.Line, this.Column, YarnSpinnerLexer.DefaultTokenChannel);
        }

        private void InsertToken(int startIndex, int stopIndex, string text, int type, int line, int column, int channel)
        {
            var tokenFactorySourcePair = Tuple.Create((ITokenSource)this, (ICharStream)this.InputStream);

            CommonToken token = new CommonToken(tokenFactorySourcePair, type, channel, startIndex, stopIndex)
            {
                Text = text,
                Line = line,
                Column = column,
            };

            this.pendingTokens.Enqueue(token);
        }

        /// <summary>
        /// A warning emitted during lexing.
        /// </summary>
        public struct Warning
        {
            /// <summary>
            /// The token associated with the warning.
            /// </summary>
            public IToken Token;

            /// <summary>
            /// The message associated with the warning.
            /// </summary>
            public string Message;
        }
    }
}
