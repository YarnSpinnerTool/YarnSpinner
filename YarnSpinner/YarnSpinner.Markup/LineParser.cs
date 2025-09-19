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

namespace Yarn.Markup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /*
        Apropos of nothing while we said for v3 we wanted markup diagnostics,
        that isn't the main impetus for this rewrite, that was one particular
        bug I found. Take the following:

        [b] this is [pause = 1000] some bold text with a typewriter pause in it [/b]

        it gets rewritten into the following for display (at runtime not a line
        creation time):

        <strong> this is some bold text with a typewriter pause in it </strong>

        and has a single pause attribute at position 8, meant it was as if I had
        actually written:

        <strong> [pause = 1000] this is some bold text with a typewriter pause in it </strong>

        Which mean the pause was at the start and it was very confusing to me.
        So now we handle nesting and rewriting instead of just handing it off
        and hoping.

        However there are situations where a rewriter tag uses it's text length
        to determine what it rewrites, and if those are split to reblance the
        markup tree correctly it will do weird things. For example lets say we
        have the following (where z tags are a rewriter that appends the current
        word count to the start of a word for some weird reason):

        "[b]this is some [z]markup that has[/b] rewrite issues[/z]"

        and the expected end result would be something like:

        "[b]this is some 0:markup 1:that 2:has[/b] 3:rewrite 4:issues"

        but the split will turn this into the following:

        "[b]this is some [z]markup that has[/z][/b][z] rewrite issues[/z]"

        which would result in the following:

        "[b]this is some 0:markup 1:that 2:has[/b] 0:rewrite 1:issues"

        which isn't what the user wants.

        To fix this properly feels impossible because it means we'd need to know
        at parse time when certain tags are hit if they are to be a higher
        priority tag then their parent. And even then there will be situations
        where that itself isn't going to be possible because the ordering of
        rewriters will throw it off. Instead we have to just accept that this
        situation isn't possible in any tool and if you do need that behaviour
        correctly nest everything to do it.
    */

    /// <summary>
    /// Parses text and produces markup information.
    /// </summary>
    public sealed class LineParser : IDisposable
    {
        /// <summary>
        /// The name of the property in replacement attributes that
        /// contains the text of the attribute.
        /// </summary>
        public const string ReplacementMarkerContents = "contents";

        /// <summary>
        /// The name of the implicitly-generated <c>character</c> attribute.
        /// </summary>
        /// <seealso cref="CharacterAttributeNameProperty"/>
        public const string CharacterAttribute = "character";

        /// <summary>
        /// The name of the 'name' property, on the implicitly-generated
        /// <c>character</c> attribute.
        /// </summary>
        /// <seealso cref="CharacterAttribute"/>
        public const string CharacterAttributeNameProperty = "name";

        /// <summary>
        /// The name of the property to use to signify that trailing
        /// whitespace should be trimmed if a tag had preceding whitespace
        /// or begins the line. This property must be a bool value.
        /// </summary>
        public const string TrimWhitespaceProperty = "trimwhitespace";

        /// <summary>
        /// The name of the attribute to use to indicate that no marker
        /// processing should occur.
        /// </summary>
        public const string NoMarkupAttribute = "nomarkup";

        private const string InternalIncrement = "_internalIncrementingProperty";

        /// <summary>
        /// A dictionary that maps the names of attributes to an object
        /// that can generate replacement text for those attributes.
        /// </summary>
        private readonly Dictionary<string, IAttributeMarkerProcessor> markerProcessors = new Dictionary<string, IAttributeMarkerProcessor>();

        /// <summary>
        /// The original text that this line parser is parsing.
        /// </summary>
        private string? input;

        /// <summary>
        /// A string reader used for reading through the <see
        /// cref="input"/>.
        /// </summary>
        private StringReader? stringReader;

        /// <summary>Registers an object as a marker processor for a given
        /// marker name.</summary>
        /// <remarks>
        /// When a marker processor is registered for a marker name, the
        /// parser will ask the processor for text to insert into the plain
        /// text. This allows users of the <see cref="LineParser"/> class
        /// to dynamically replace text in a line. The <c>nomarkup</c> tag is
        /// implemented in this way by the <see cref="LineParser"/> class
        /// directly; the <see cref="Dialogue"/> class uses this mechanism
        /// to implement the <c>select</c>, <c>plural</c> and <c>ordinal</c> markers.
        /// </remarks>
        /// <param name="attributeName">The name of the marker that should
        /// use this marker processor.</param>
        /// <param name="markerProcessor">The object that should be invoked
        /// when markers with this name are encountered.</param>
        public void RegisterMarkerProcessor(string attributeName, IAttributeMarkerProcessor markerProcessor)
        {
            if (this.markerProcessors.ContainsKey(attributeName))
            {
                throw new InvalidOperationException($"A marker processor for {attributeName} has already been registered.");
            }

            this.markerProcessors.Add(attributeName, markerProcessor);
        }

        /// <summary>
        /// Removes any marker processor associated with a given marker name.
        /// </summary>
        /// <param name="attributeName">The name of the marker.</param>
        public void DeregisterMarkerProcessor(string attributeName)
        {
            this.markerProcessors.Remove(attributeName);
        }

        internal enum LexerTokenTypes
        {
            Text,
            OpenMarker,
            CloseMarker,
            CloseSlash,
            Identifier,
            Error,
            Start,
            End,
            Equals,
            StringValue,
            NumberValue,
            BooleanValue,
            InterpolatedValue,
        }
        internal class LexerToken
        {
            public LexerToken(LexerTokenTypes type)
            {
                this.Type = type;
            }
            internal LexerTokenTypes Type { get; set; }
            internal int Start { get; set; } = 0;
            internal int End { get; set; } = 0;
            internal int Range
            {
                get
                {
                    return End + 1 - Start;
                }
            }
        }
        private enum LexerMode
        {
            Text,
            Tag,
            Value,
        }

        internal class TokenStream
        {
            // this just manages a list of tokens and their current iterator
            // purely a convenience class and it wraps a list and an int
            internal List<LexerToken> tokens;

            public TokenStream(List<LexerToken> tokens)
            {
                this.tokens = tokens;
            }

            internal int iterator = 0;
            internal LexerToken Current
            {
                get
                {
                    if (iterator < 0)
                    {
                        iterator = 0;
                        var first = new LexerToken(LexerTokenTypes.Start);

                        return first;
                    }
                    if (iterator > tokens.Count - 1)
                    {
                        iterator = tokens.Count - 1;
                        var last = new LexerToken(LexerTokenTypes.End);

                        return last;
                    }
                    return tokens[iterator];
                }
            }
            internal LexerToken Next()
            {
                iterator += 1;
                return Current;
            }
            internal LexerToken Previous()
            {
                iterator -= 1;
                return Current;
            }
            internal void Consume(int number)
            {
                iterator += number;
            }
            internal LexerToken Peek()
            {
                iterator += 1;
                var next = Current;
                iterator -= 1;
                return next;
            }
            internal LexerToken LookAhead(int number)
            {
                iterator += number;
                var lookAhead = Current;
                iterator -= number;
                return lookAhead;
            }
            internal bool ComparePattern(LexerTokenTypes[] pattern)
            {
                bool match = true;
                int currentIterator = iterator;
                // ok so we march through the list from the current position and say "yep this matches" until it doesn't
                foreach (var type in pattern)
                {
                    if (Current.Type == type)
                    {
                        iterator += 1;
                        continue;
                    }
                    match = false;
                    break;
                }

                iterator = currentIterator;

                return match;
            }
        }

        internal List<LexerToken> LexMarkup(string input)
        {
            List<LexerToken> tokens = new List<LexerToken>();
            if (string.IsNullOrEmpty(input))
            {
                var start = new LexerToken(LexerTokenTypes.Start)
                {
                    Start = 0,
                    End = 0,
                };
                var end = new LexerToken(LexerTokenTypes.End)
                {
                    Start = 0,
                    End = 0,
                };
                tokens.Add(start);
                tokens.Add(end);
                // empty string, ignoring
                return tokens;
            }

            LexerMode mode = LexerMode.Text;

            this.input = input.Normalize();
            this.stringReader = new StringReader(this.input);

            int nextCharacter;
            LexerToken last = new LexerToken(LexerTokenTypes.Start)
            {
                Start = 0,
                End = 0,
            };
            tokens.Add(last);
            int currentPosition = 0;

            while ((nextCharacter = this.stringReader.Read()) != -1)
            {
                char c = (char)nextCharacter;

                if (mode == LexerMode.Text)
                {
                    switch (c)
                    {
                        case '[':
                            {
                                // check if the last token was text
                                // and if that text is a \ we run this as if we were text
                                if (last.Type == LexerTokenTypes.Text)
                                {
                                    var l = input[last.End];
                                    if (l == '\\')
                                    {
                                        goto default;
                                    }
                                }

                                last = new LexerToken(LexerTokenTypes.OpenMarker)
                                {
                                    Start = currentPosition,
                                    End = currentPosition
                                };
                                tokens.Add(last);
                                mode = LexerMode.Tag;
                                break;
                            }
                        default:
                            {
                                // if the last token is also a text we want to extend it
                                // otherwise we make a new text token
                                if (last.Type == LexerTokenTypes.Text)
                                {
                                    last.End = currentPosition;
                                }
                                else
                                {
                                    last = new LexerToken(LexerTokenTypes.Text)
                                    {
                                        Start = currentPosition,
                                        End = currentPosition
                                    };
                                    tokens.Add(last);
                                }
                                break;
                            }
                    }
                }
                else if (mode == LexerMode.Tag)
                {
                    // we are in tag mode
                    // this means different rules for text basically
                    switch (c)
                    {
                        case ']':
                            {
                                last = new LexerToken(LexerTokenTypes.CloseMarker)
                                {
                                    Start = currentPosition,
                                    End = currentPosition
                                };
                                tokens.Add(last);
                                mode = LexerMode.Text;
                                break;
                            }
                        case '/':
                            {
                                last = new LexerToken(LexerTokenTypes.CloseSlash)
                                {
                                    Start = currentPosition,
                                    End = currentPosition
                                };
                                tokens.Add(last);
                                break;
                            }
                        case '=':
                            {
                                last = new LexerToken(LexerTokenTypes.Equals)
                                {
                                    Start = currentPosition,
                                    End = currentPosition,
                                };
                                tokens.Add(last);
                                mode = LexerMode.Value;
                                break;
                            }
                        default:
                            {
                                // this is a bit more specialised
                                // because if we are inside tag mode and ARENT one of the above specific tokens we MUST be an identifier
                                // and identifiers have a specific structure of [a-zA-Z0-9] and nothing else
                                // so this means we want to eat characters until we are no longer a valid identifier character
                                // at which point we close off the identifier token and let lexing continue as normal
                                // we don't change mode because the next character will determine what we need to do
                                // either as another identifier, a value, or closing off the tag
                                if (char.IsLetterOrDigit(c))
                                {
                                    var start = currentPosition;

                                    // keep reading characters until the NEXT character is not a letter or digit
                                    // when that happens we will stop at that point, emit an id token
                                    while (char.IsLetterOrDigit((char)this.stringReader.Peek()))
                                    {
                                        _ = this.stringReader.Read();
                                        currentPosition += 1;
                                    }

                                    last = new LexerToken(LexerTokenTypes.Identifier)
                                    {
                                        Start = start,
                                        End = currentPosition,
                                    };
                                    tokens.Add(last);
                                }
                                else if (!char.IsWhiteSpace(c))
                                {
                                    // if we are whitespace we likely want to just continue because it's most likely just spacing between identifiers
                                    // the only time this isn't allowed is if they split the marker name, but that is a parser issue not a lexer issue
                                    // so basically if we encounter a non-alphanumeric or non-whitespace we error
                                    last = new LexerToken(LexerTokenTypes.Error)
                                    {
                                        Start = currentPosition,
                                        End = currentPosition,
                                    };
                                    tokens.Add(last);
                                    mode = LexerMode.Text;
                                }
                                break;
                            }
                    }
                }
                else if (mode == LexerMode.Value)
                {
                    // we are in value mode now
                    // this one is gonna be a little bit weird in that it will take over controlling the reader
                    // because capturing slices of a value doesn't really make any sense
                    // so what we want to do is make a few little changes depending on what the first non-whitespace character is

                    // if it is a number we will read until we have no more numbers to read (or decimals)
                    // if it is a " we will read until we hit another "
                    // if it is a boolean (so true or false)
                    // otherwise we will read arbitrary non ] characters until we hit that or a whitespace

                    // we allow an arbitrary amount of whitespace before a value
                    // so we just spin here gobbling up characters
                    if (!char.IsWhiteSpace(c))
                    {
                        // we are a number
                        // this happens if we are either a digit or the negation symbol
                        if (char.IsDigit(c) || c == '-')
                        {
                            var token = new LexerToken(LexerTokenTypes.NumberValue)
                            {
                                Start = currentPosition,
                            };

                            var next = (char)this.stringReader.Peek();
                            if (char.IsDigit(next) || next == '.')
                            {
                                // we want to keep advancing until we no longer get a digit/period
                                while (this.stringReader.Read() != -1)
                                {
                                    currentPosition += 1;
                                    // if the next position is the end we don't want to advance
                                    int position = this.stringReader.Peek();
                                    if (position == -1)
                                    {
                                        // this means we fell off the end without closing the number and as such the tag
                                        // so we are at an error, so we just tweak the type and continue
                                        token.Type = LexerTokenTypes.Error;
                                        break;
                                    }
                                    // alternatively if the next character isn't a number we also don't want to advance
                                    next = (char)position;
                                    if (!(char.IsDigit(next) || next == '.'))
                                    {
                                        break;
                                    }
                                }
                            }
                            // now here we have gobbled every contiguous digit/period
                            // so can finish up the token and return to tag mode
                            // but before we do that we want to do a quick check to see if its actually a valid float

#pragma warning disable CA1846 // Prefer 'AsSpan' over 'Substring' (not available in .NET Standard 2.0)
                            if (float.TryParse(input.Substring(token.Start, currentPosition + 1 - token.Start), out _))
#pragma warning restore CA1846 // Prefer 'AsSpan' over 'Substring'
                            {
                                token.End = currentPosition;
                                tokens.Add(token);
                                last = token;
                            }
                            else
                            {
                                token.End = currentPosition;
                                token.Type = LexerTokenTypes.Error;
                                tokens.Add(token);
                                last = token;
                            }

                            mode = LexerMode.Tag;
                        }
                        else if (c == '"')
                        {
                            // we are a " delimted string
                            // which means we want to go until the next " we find
                            var token = new LexerToken(LexerTokenTypes.StringValue)
                            {
                                Start = currentPosition,
                            };

                            // if we aren't the last character
                            if (this.stringReader.Peek() != -1)
                            {
                                // getting the next quote that isn't preceeded by a \
                                var match = System.Text.RegularExpressions.Regex.Match(input.Substring(currentPosition + 1), @"(?<!\\)""");
                                var nextQuote = input.IndexOf('"', currentPosition + match.Index + 1);

                                if (nextQuote == -1)
                                {
                                    // there isn't a closing string delimiter
                                    token.Type = LexerTokenTypes.Error;
                                }
                                else
                                {
                                    // next holds the index of the next "
                                    // which means we need to read this many characters and advance currentPosition by that many
                                    var length = nextQuote - currentPosition;
                                    for (int i = 0; i < length; i++)
                                    {
                                        _ = this.stringReader.Read();
                                    }
                                    currentPosition += length;
                                }
                            }
                            else
                            {
                                // the " is the last character
                                // which means we have an unclosed string and this is an error
                                token.Type = LexerTokenTypes.Error;
                            }

                            token.End = currentPosition;
                            last = token;
                            tokens.Add(token);
                            mode = LexerMode.Tag;
                        }
                        else if (c == '{')
                        {
                            // we are an interpolated value
                            // these won't exist at runtime but to provide diagonostics we need to handle them
                            // this isn't a full parse, this will just grab up until the next } and it can't be escaped
                            // any errors in this will be caught by the actual Yarn Spinner parser so we can ignore what is between the { }

                            var token = new LexerToken(LexerTokenTypes.InterpolatedValue)
                            {
                                Start = currentPosition,
                            };

                            bool exited = false;
                            while (this.stringReader.Peek() != -1)
                            {
                                currentPosition += 1;
                                if ((char)this.stringReader.Read() == '}')
                                {
                                    exited = true;
                                    break;
                                }
                            }
                            if (!exited)
                            {
                                // We reached the end of the string but we never
                                // saw a closing brace - this is an error
                                token.Type = LexerTokenTypes.Error;
                            }
                            token.End = currentPosition;
                            tokens.Add(token);
                            last = token;

                            mode = LexerMode.Tag;
                        }
                        else
                        {
                            // finally we are now one of three possibilities
                            // true/false/generic alphanumeric text
                            var token = new LexerToken(LexerTokenTypes.StringValue)
                            {
                                Start = currentPosition,
                            };

                            if (char.IsLetterOrDigit((char)this.stringReader.Peek()))
                            {
                                while (this.stringReader.Read() != -1)
                                {
                                    currentPosition += 1;
                                    if (!char.IsLetterOrDigit((char)this.stringReader.Peek()))
                                    {
                                        // the next one isn't alphanumeric
                                        // so we will exit now
                                        break;
                                    }
                                }
                            }

                            // ok so now we have accumulated all the value characters
                            // which we now want to do some quick checks on, is it true or false
                            var value = input.Substring(token.Start, currentPosition + 1 - token.Start);
                            if (value == "true" || value == "True" || value == "false" || value == "False")
                            {
                                // This was the word '[T]rue' or '[F]alse', so this
                                // is a boolean value and not an undelimited
                                // string
                                token.Type = LexerTokenTypes.BooleanValue;
                            }

                            token.End = currentPosition;
                            tokens.Add(token);
                            last = token;

                            mode = LexerMode.Tag;
                        }
                    }
                }
                else
                {
                    // we are in an invalid mode somehow
                    // lex as errors
                    last = new LexerToken(LexerTokenTypes.Error)
                    {
                        Start = currentPosition,
                        End = currentPosition,
                    };
                    tokens.Add(last);
                }

                currentPosition += 1;
            }

            last = new LexerToken(LexerTokenTypes.End)
            {
                Start = currentPosition,
                End = input.Length - 1,
            };
            tokens.Add(last);

            return tokens;
        }

        internal class MarkupTreeNode
        {
            public string? name;
            public LexerToken? firstToken;
            public List<MarkupTreeNode> children = new List<MarkupTreeNode>();
            public List<MarkupProperty> properties = new List<MarkupProperty>();
        }
        internal class MarkupTextNode : MarkupTreeNode { public string text = ""; }

        /// <summary>
        /// Represents a diagnostic message produced during markup parsing.
        /// </summary>
        public struct MarkupDiagnostic : IEquatable<MarkupDiagnostic>
        {
            /// <summary>
            /// Gets the text of the diagnostic.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Gets the zero-based column index of the start of the range at
            /// which this diagnostic appears.
            /// </summary>
            public int Column { get; }

            /// <summary>
            /// Initialises a new instance of the <see cref="MarkupDiagnostic"/>
            /// struct.
            /// </summary>
            /// <param name="message">The diagnostic text.</param>
            /// <param name="column">The zero-based first column of the
            /// diagnostic's range.</param>
            public MarkupDiagnostic(string message, int column = -1)
            {
                this.Message = message;
                this.Column = column;
            }

            /// <inheritdoc/>
            public override readonly bool Equals(object obj)
            {
                if (obj is not MarkupDiagnostic other)
                {
                    return false;
                }
                return Equals(other);
            }

            /// <inheritdoc/>
            public override readonly int GetHashCode()
            {
#pragma warning disable CA1307 // Specify StringComparison for clarity (not available in .NET Standard 2.0)
                return Message.GetHashCode() ^ Column.GetHashCode();
#pragma warning restore CA1307 // Specify StringComparison for clarity


            }

            /// <inheritdoc/>
            public static bool operator ==(MarkupDiagnostic left, MarkupDiagnostic right)
            {
                return left.Equals(right);
            }

            /// <inheritdoc/>
            public static bool operator !=(MarkupDiagnostic left, MarkupDiagnostic right)
            {
                return !(left == right);
            }

            /// <inheritdoc/>
            public readonly bool Equals(MarkupDiagnostic other)
            {
                return this.Column == other.Column && this.Message == other.Message;
            }
        }

        // this exists so that we have a way of seeing which attributes got split as part of the tree walking
        // this can then be used to merge them back together in the end
        private int internalIncrementingAttribute = 1;
        private MarkupProperty internalIDproperty()
        {
            var idProperty = new MarkupProperty(InternalIncrement, internalIncrementingAttribute);
            internalIncrementingAttribute += 1;

            return idProperty;
        }


        // This keeps track of the last seen older sibling during tree walking.
        // This is necessary to prevent a bug with "Yes... which I would have shown [emotion=\"frown\" /] had [b]you[/b] not interrupted me."
        // where the b tag is a rewriter and the emotion tag is not.
        // in that case because previously we only kept the last fully processed non-replacement sibling the emotion tag would eat the whitespace AFTER the b tag had replaced itself
        private MarkupTreeNode? sibling = null;
        internal void WalkAndProcessTree(MarkupTreeNode root, System.Text.StringBuilder builder, List<MarkupAttribute> attributes, string localeCode, List<MarkupDiagnostic> diagnostics, int offset = 0)
        {
            sibling = null;
            WalkTree(root, builder, attributes, localeCode, diagnostics, offset = 0);
        }

        private void WalkTree(MarkupTreeNode root, System.Text.StringBuilder builder, List<MarkupAttribute> attributes, string localeCode, List<MarkupDiagnostic> diagnostics, int offset = 0)
        {
            // if we are a text node
            if (root is MarkupTextNode)
            {
                var line = ((MarkupTextNode)root).text;

                // and we have an older sibling
                if (sibling != null)
                {
                    foreach (var property in sibling.properties)
                    {
                        // and that sibling has the whitespace trimming property
                        if (property.Name == TrimWhitespaceProperty)
                        {
                            // and that the whitespace trim is true
                            if (property.Value.BoolValue == true)
                            {
                                // and the text has a length of at least 1
                                if (line.Length > 0)
                                {
                                    // and that the first character is whitespace
                                    if (Char.IsWhiteSpace(line[0]))
                                    {
                                        // then we can trim the whitespace
                                        line = line.Substring(1);
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                // finally if there are any escaped markup in the line we need to clean them up also
                line = line.Replace("\\[", "[");
                line = line.Replace("\\]", "]");
                // then we add ourselves to the growing line
                builder.Append(line);
                // and make ourselve the new older sibling
                sibling = root;
                return;
            }

            // we aren't text so we will need to handle all our children
            // we do this recursively
            var childBuilder = new StringBuilder();
            var childAttributes = new List<MarkupAttribute>();
            foreach (var child in root.children)
            {
                WalkTree(child, childBuilder, childAttributes, localeCode, diagnostics, builder.Length + offset);
            }

            // before we go any further if we are the root node that means we have finished and can just wrap up
            if (string.IsNullOrEmpty(root.name))
            {
                // we are so we have nothing left to do, just add our children and be done
                builder.Append(childBuilder);
                attributes.AddRange(childAttributes);
                return;
            }

            // finally now our children have done their stuff so we can run our own rewriter if necessary
            // to do that we will need the combined finished string of all our children and their attributes
            if (markerProcessors.TryGetValue(root.name!, out var rewriter))
            {
                // we now need to do the rewrite
                // so in this case we need to give the rewriter the combined child string and it's attributes
                // because it is up to you to fix any attributes if you modify them
                MarkupAttribute attribute = new MarkupAttribute(builder.Length + offset, root.firstToken?.Start ?? -1, childBuilder.Length, root.name!, root.properties);
                diagnostics.AddRange(rewriter.ProcessReplacementMarker(attribute, childBuilder, childAttributes, localeCode));
            }
            else
            {
                // we aren't a replacement marker
                // which means we need to add ourselves as a tag
                // the source position one is easy enough, that is just the position of the first token (wait you never added these you dingus)
                // we know the length of all the children text because of the childBuilder so that gives us our range
                // and we know our relative start because of our siblings text in the builder
                MarkupAttribute attribute = new MarkupAttribute(builder.Length + offset, root.firstToken?.Start ?? -1, childBuilder.Length, root.name!, root.properties);
                attributes.Add(attribute);
            }

            // ok now at this stage inside childBuilder we have a valid modified (if was necessary) string
            // and our attributes have been added, all we need to do is add this to our siblings and continue
            builder.Append(childBuilder);
            attributes.AddRange(childAttributes);

            // finally we make ourselves the most immediate oldest sibling
            sibling = root;
        }

        internal static void SquishSplitAttributes(List<MarkupAttribute> attributes)
        {
            // grab every attribute that has a _internalIncrementingProperty property
            // then for every attribute with the same value of that property we merge them
            // and finally remove the _internalIncrementingProperty property
            List<int> removals = new List<int>();
            Dictionary<int, MarkupAttribute> merged = new Dictionary<int, MarkupAttribute>();
            for (int i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (attribute.Properties.TryGetValue(InternalIncrement, out var value))
                {
                    if (merged.TryGetValue(value.IntegerValue, out var existingAttribute))
                    {
                        if (existingAttribute.Position > attribute.Position)
                        {
                            existingAttribute.Position = attribute.Position;
                        }
                        existingAttribute.Length += attribute.Length;
                        merged[value.IntegerValue] = existingAttribute;
                    }
                    else
                    {
                        merged.Add(value.IntegerValue, attribute);
                    }
                    removals.Add(i);
                }
            }

            // now we need to remove all the ones with _internalIncrementingProperty
            removals.Sort();
            for (int i = removals.Count - 1; i > -1; i--)
            {
                attributes.RemoveAt(removals[i]);
            }
            // and add our merged attributes back in
            foreach (var pair in merged)
            {
                attributes.Add(pair.Value);
            }
        }

        // builds a tree of MarkupTreeNode nodes
        // the top is always just the empty root and it must have at least one child even if it's just text
        internal (MarkupTreeNode tree, List<MarkupDiagnostic> diagnostics) BuildMarkupTreeFromTokens(List<LexerToken> tokens, string OG)
        {
            var tree = new MarkupTreeNode();
            List<MarkupDiagnostic> diagnostics = new List<MarkupDiagnostic>();

            if (tokens == null || tokens.Count < 2)
            {
                diagnostics.Add(new MarkupDiagnostic("There are not enough tokens to form a valid tree."));
                return (tree, diagnostics);
            }
            if (string.IsNullOrEmpty(OG))
            {
                diagnostics.Add(new MarkupDiagnostic("There is a valid list of tokens but no original string."));
                return (tree, diagnostics);
            }
            if (tokens[0].Type != LexerTokenTypes.Start && tokens[tokens.Count - 1].Type != LexerTokenTypes.End)
            {
                diagnostics.Add(new MarkupDiagnostic("Token list doesn't start and end with the correct tokens."));
                return (tree, diagnostics);
            }

            OG = OG.Normalize();

            bool TryIntFromToken(LexerToken token, out int value)
            {
                var valueString = OG.Substring(token.Start, token.Range);

                if (int.TryParse(valueString, out value))
                {
                    return true;
                }

                value = 0;
                return false;
            }
            bool TryFloatFromToken(LexerToken token, out float value)
            {
                var valueString = OG.Substring(token.Start, token.Range);

                if (float.TryParse(valueString, out value))
                {
                    return true;
                }

                value = 0;
                return false;
            }
            bool TryBoolFromToken(LexerToken token, out bool value)
            {
                // at this point the lexer has determined that it is either a true or false value
                // so we are safe to do the ignore case check
                var valueString = OG.Substring(token.Start, token.Range);
                if (valueString.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    value = true;
                    return true;
                }
                if (valueString.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    value = false;
                    return true;
                }

                value = true;
                return false;
            }
            string ValueFromToken(LexerToken token)
            {
                var valueString = OG.Substring(token.Start, token.Range);
                if (valueString.StartsWith("\"", StringComparison.Ordinal) && valueString.EndsWith("\"", StringComparison.Ordinal))
                {
                    // if we are inside delimiters we will also need to remove any escaped characters
                    valueString = valueString.Replace("\\", string.Empty).Trim('"');
                }
                return valueString;
            }
            string ValueFromInterpolatedToken(LexerToken token)
            {
                // removing the { } from the interpolated value
                var valueString = OG.Substring(token.Start, token.Range);
                valueString = valueString.Trim('{').Trim('}');
                return valueString;
            }

            // [ / ]
            LexerTokenTypes[] closeAllPattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.CloseSlash, LexerTokenTypes.CloseMarker };
            // [ / ID ]
            LexerTokenTypes[] closeOpenAttributePattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.CloseSlash, LexerTokenTypes.Identifier, LexerTokenTypes.CloseMarker };
            // [ / ~( ID | ] ) 
            LexerTokenTypes[] closeErrorPattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.CloseSlash };
            // [ ID ]
            LexerTokenTypes[] openAttributePropertyLessPattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.Identifier, LexerTokenTypes.CloseMarker };
            // ID = VALUE
            LexerTokenTypes[] numberPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.NumberValue };
            LexerTokenTypes[] booleanPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.BooleanValue };
            LexerTokenTypes[] stringPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.StringValue };
            LexerTokenTypes[] interpolatedPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.InterpolatedValue };
            // / ]
            LexerTokenTypes[] selfClosingAttributeEndPattern = { LexerTokenTypes.CloseSlash, LexerTokenTypes.CloseMarker };

            var stream = new TokenStream(tokens);

            var openNodes = new Stack<MarkupTreeNode>();
            openNodes.Push(tree);

            var unmatchedCloses = new List<string>();

            while (stream.Current.Type != LexerTokenTypes.End)
            {
                var type = stream.Current.Type;

                switch (type)
                {
                    case LexerTokenTypes.Start:
                        {
                            break;
                        }
                    case LexerTokenTypes.End:
                        {
                            // we are at the end
                            // in this case we just want to make sure we clean up any remaning unmatched closes we still have
                            CleanUpUnmatchedCloses(openNodes, unmatchedCloses, diagnostics);
                            break;
                        }
                    case LexerTokenTypes.Text:
                        {
                            // we are adding text to the tree
                            // but first we need to make sure there aren't any closes left to clean up
                            if (unmatchedCloses.Count > 0)
                            {
                                CleanUpUnmatchedCloses(openNodes, unmatchedCloses, diagnostics);
                            }

                            var text = OG.Substring(stream.Current.Start, stream.Current.End + 1 - stream.Current.Start);
                            var node = new MarkupTextNode()
                            {
                                text = text,
                                firstToken = stream.Current,
                            };
                            openNodes.Peek().children.Add(node);
                            break;
                        }
                    case LexerTokenTypes.OpenMarker:
                        {
                            // we hit an open marker
                            // we first want to see if this is part of a close marker
                            // if it is then we can just wrap up the current root (or roots in the case of close all)
                            if (stream.ComparePattern(closeAllPattern))
                            {
                                // it's the close all marker
                                // so we want to pop off everything until we hit the tree root
                                stream.Consume(2);
                                while (openNodes.Count > 1)
                                {
                                    // if we have any unmatchedCloses we want to handle them now as well
                                    // in this case though all we need to do though is just remove the stack from the list as we go through it
                                    MarkupTreeNode markupTreeNode = openNodes.Pop();
                                    if (markupTreeNode.name != null)
                                    {
                                        _ = unmatchedCloses.Remove(markupTreeNode.name);
                                    }
                                }
                                foreach (var remaining in unmatchedCloses)
                                {
                                    diagnostics.Add(new MarkupDiagnostic($"asked to close {remaining} markup but there is no corresponding opening. Is [/{remaining}] a typo?"));
                                }
                                unmatchedCloses.Clear();
                                break;
                            }
                            else if (stream.ComparePattern(closeOpenAttributePattern))
                            {
                                // it's a close an open attribute marker
                                var closeIDToken = stream.LookAhead(2);
                                var closeID = OG.Substring(closeIDToken.Start, closeIDToken.Range);
                                // eat the tokens we compared
                                stream.Consume(3);

                                // ok now we need to work out what we do if they don't match
                                // first up we need to get the current top of the stack
                                if (openNodes.Count == 1)
                                {
                                    // this is an error, we can't close something when we only have the root node
                                    diagnostics.Add(new MarkupDiagnostic($"Asked to close {closeID}, but we don't have an open marker for it.", closeIDToken.Start));
                                }
                                else
                                {
                                    // if they have the same name we are in luck
                                    // we can pop this bad boy off the stack right now and continue
                                    // if not then we add this to the list of unmatched closes for later clean up and continue
                                    if (closeID == openNodes.Peek().name)
                                    {
                                        _ = openNodes.Pop();
                                    }
                                    else
                                    {
                                        unmatchedCloses.Add(closeID);
                                    }
                                }

                                break;
                            }
                            else if (stream.ComparePattern(closeErrorPattern))
                            {
                                // we are a malformed close tag
                                var message = $"Error parsing markup, detected invalid token {stream.LookAhead(2).Type}, following a close.";
                                diagnostics.Add(new MarkupDiagnostic(message, stream.Current.Start));
                                break;
                            }

                            // ok so now we are some variant of a regular open marker
                            // in that case we have to be one of:
                            // [ ID, [ ID =, [ nomarkup
                            // or an error of: [ *

                            // which means if the next token isn't an ID it's an error so let's handle that first
                            if (stream.Peek().Type != LexerTokenTypes.Identifier)
                            {
                                var message = $"Error parsing markup, detected invalid token {stream.Peek().Type}, following an open marker.";
                                diagnostics.Add(new MarkupDiagnostic(message, stream.Peek().Start));
                                break;
                            }

                            // ok so now we are a valid form of an open marker
                            // but before we can continue we need to make sure that the tree is correctly closed off
                            if (unmatchedCloses.Count > 0)
                            {
                                CleanUpUnmatchedCloses(openNodes, unmatchedCloses, diagnostics);
                            }

                            var idToken = stream.Peek();
                            var id = OG.Substring(idToken.Start, idToken.Range);

                            // there are two slightly weird variants we will want to handle now
                            // the first is the nomarkup attribute, which completely changes the flow of the tool
                            if (stream.ComparePattern(openAttributePropertyLessPattern))
                            {
                                if (id == NoMarkupAttribute)
                                {
                                    // so to get here we are [ nomarkup ]
                                    // which mean the first token after is 3 tokens away
                                    var tokenStart = stream.Current;
                                    var firstTokenAfterNoMarkup = stream.LookAhead(3);

                                    // we spin in here eating tokens until we hit closeOpenAttributePattern
                                    // when we do we stop and check if the id is nomarkupmarker
                                    // if it is we stop and return that
                                    // if we never find that we return an error instead
                                    MarkupTreeNode? nm = null;
                                    while (stream.Current.Type != LexerTokenTypes.End)
                                    {
                                        if (stream.ComparePattern(closeOpenAttributePattern))
                                        {
                                            // [ / id ]
                                            var nmIDToken = stream.LookAhead(2);
                                            if (OG.Substring(nmIDToken.Start, nmIDToken.Range) == NoMarkupAttribute)
                                            {
                                                // we have found the end of the nomarkup marker
                                                // create a new text node
                                                // assign it as the child of the markup node
                                                // return this
                                                var text = new MarkupTextNode()
                                                {
                                                    text = OG.Substring(firstTokenAfterNoMarkup.Start, stream.Current.Start - firstTokenAfterNoMarkup.Start),
                                                };
                                                nm = new MarkupTreeNode();
                                                nm.name = NoMarkupAttribute;
                                                nm.children.Add(text);
                                                // adding the tokens that represent this nomarkup element
                                                // which is the [ from the [ nomarkup ] triplet all the way to the ] of the [/ nomarkup ]
                                                nm.firstToken = tokenStart;

                                                // last step is to consume the tokens that represent [/nomarkup]
                                                stream.Consume(3);

                                                break;
                                            }
                                        }
                                        _ = stream.Next();
                                    }
                                    if (nm == null)
                                    {
                                        diagnostics.Add(new MarkupDiagnostic($"we entered nomarkup mode but didn't find an exit token", tokenStart.Start));
                                    }
                                    else
                                    {
                                        openNodes.Peek().children.Add(nm);
                                    }
                                    break;
                                }
                                else
                                {
                                    // we are a marker with no properties, [ ID ] the ideal case
                                    var completeMarker = new MarkupTreeNode();
                                    completeMarker.name = id;
                                    completeMarker.firstToken = stream.Current;
                                    openNodes.Peek().children.Add(completeMarker);
                                    openNodes.Push(completeMarker);
                                    // we now need to consume the id and ] tokens
                                    stream.Consume(2);

                                    break;
                                }
                            }

                            // ok so we are now one of two options
                            // a regular open marker (best case): [ ID (ID = Value)+ ]
                            // or an open marker with a nameless property: [ (ID = Value)+ ]
                            var marker = new MarkupTreeNode();
                            marker.name = id;
                            marker.firstToken = stream.Current;

                            openNodes.Peek().children.Add(marker);
                            openNodes.Push(marker);

                            if (stream.LookAhead(2).Type != LexerTokenTypes.Equals)
                            {
                                // we are part of a normal [ID id = value] group
                                // we want to consume the [ and ID
                                // so that the next token in the stream will be clean to handle id = value triples.
                                // this way the [ ID = variant doesn't realise that it wasn't part of a normal [ ID id = value ] group
                                stream.Consume(1);
                            }

                            break;
                        }
                    case LexerTokenTypes.Identifier:
                        {
                            // ok so we are now at an identifier
                            // which is the situation we want to be in for properties of the form ID = VALUE
                            // in all situations its the same
                            // we get the id, use that to make a new property
                            // we get the value and coorce an actual value from it
                            string id = OG.Substring(stream.Current.Start, stream.Current.Range);

                            if (stream.ComparePattern(numberPropertyPattern))
                            {
                                if (TryIntFromToken(stream.LookAhead(2), out int iValue))
                                {
                                    openNodes.Peek().properties.Add(new MarkupProperty(id, iValue));
                                }
                                else if (TryFloatFromToken(stream.LookAhead(2), out float fValue))
                                {
                                    openNodes.Peek().properties.Add(new MarkupProperty(id, fValue));
                                }
                                else
                                {
                                    var message = $"failed to convert the value {OG.Substring(stream.LookAhead(2).Start, stream.LookAhead(2).Range)} into a valid property";
                                    diagnostics.Add(new MarkupDiagnostic(message, stream.LookAhead(2).Start));
                                    break;
                                }
                            }
                            else if (stream.ComparePattern(booleanPropertyPattern))
                            {
                                if (TryBoolFromToken(stream.LookAhead(2), out bool bValue))
                                {
                                    openNodes.Peek().properties.Add(new MarkupProperty(id, bValue));
                                }
                                else
                                {
                                    var message = $"failed to convert the value {OG.Substring(stream.LookAhead(2).Start, stream.LookAhead(2).Range)} into a valid property";
                                    diagnostics.Add(new MarkupDiagnostic(message, stream.LookAhead(2).Start));
                                    break;
                                }
                            }
                            else if (stream.ComparePattern(stringPropertyPattern))
                            {
                                string sValue = ValueFromToken(stream.LookAhead(2));
                                openNodes.Peek().properties.Add(new MarkupProperty(id, sValue));
                            }
                            else if (stream.ComparePattern(interpolatedPropertyPattern))
                            {
                                // we don't really know what type of value the interpolated value is
                                // but that's fine we only need it to exist for the purposes of diagnostics
                                // so we will suggest it to be a string
                                string sValue = ValueFromInterpolatedToken(stream.LookAhead(2));
                                openNodes.Peek().properties.Add(new MarkupProperty(id, sValue));
                            }
                            else
                            {
                                var message = $"Expected to find a property and it's value, but instead found \"{id} {OG.Substring(stream.Peek().Start, stream.Peek().Range)} {OG.Substring(stream.LookAhead(2).Start, stream.LookAhead(2).Range)}\".";
                                diagnostics.Add(new MarkupDiagnostic(message, stream.Peek().Start));
                                break;
                            }

                            stream.Consume(2);

                            break;
                        }
                    case LexerTokenTypes.CloseSlash:
                        {
                            // this will only happen when we hit a self closing marker [ ID (= VALUE)? (ID = VALUE)* / ]
                            // in which case we just need to close off the current open marker as it can't have children
                            if (stream.ComparePattern(selfClosingAttributeEndPattern))
                            {
                                // ok last step is to add the trimwhitespace attribute in here
                                // unless it already has one
                                var top = openNodes.Pop();
                                bool found = false;
                                foreach (var property in top.properties)
                                {
                                    if (property.Name == TrimWhitespaceProperty)
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    var wpProperty = new MarkupProperty(TrimWhitespaceProperty, true);
                                    top.properties.Add(wpProperty);
                                }

                                stream.Consume(1);
                            }
                            else
                            {
                                // we found a / but aren't part of a self closing marker
                                // at this stage this is now an error
                                diagnostics.Add(new MarkupDiagnostic("Encountered an unexpected closing slash", stream.Current.Start));
                            }

                            break;
                        }
                }
                _ = stream.Next();
            }

            // we have now run off the end of the line
            // if we have any unmatched closes still lying around we want to close them off now
            // because at this stage it doesn't matter about ordering

            // ok last thing to check is is there only one element left on the stack of open nodes
            if (openNodes.Count > 1)
            {
                var line = "parsing finished with unclosed attributes still on the stack: ";
                foreach (var node in openNodes)
                {
                    if (string.IsNullOrEmpty(node.name))
                    {
                        line += " NULL";
                    }
                    else
                    {
                        line += " [" + node.name + "]";
                    }
                }
                diagnostics.Add(new MarkupDiagnostic(line));
            }
            if (unmatchedCloses.Count > 1)
            {
                var line = "parsing finished with unmatched closes still remaining: ";
                foreach (var unmatched in unmatchedCloses)
                {
                    line += " [/" + unmatched + "]";
                }
                diagnostics.Add(new MarkupDiagnostic(line));
            }

            return (tree, diagnostics);
        }

        // this cleans up and rebalances the tree for misclosed or invalid closing patterns like the following:
        // This [a] is [b] some markup [/a][/b] invalid structure.
        // This [a] is [b] some [c] nested [/a] markup [/c] with [/b] invalid structure.
        // [z] this [a] is [b] some [c] markup [d] with [e] both [/c][/e][/d][/a][/z] misclosed tags and double unclosable tags[/b]
        // it is a variant of the adoption agency algorithm
        private void CleanUpUnmatchedCloses(Stack<MarkupTreeNode> openNodes, List<string> unmatchedCloseNames, List<MarkupDiagnostic> errors)
        {
            var orphans = new Stack<MarkupTreeNode>();
            // while we still have unbalanced closes AND haven't hit the root of the tree
            while (unmatchedCloseNames.Count > 0 && openNodes.Count > 1)
            {
                // if the current top of the stack isn't one of the closes we will need to keep it around
                // otherwise we just remove it from the list of closes and keep walking back up the tree
                var top = openNodes.Pop();

                // need to check if we already have an id
                // if we do we don't want another one
                // this happens when an element is split multiple times
                bool found = false;
                foreach (var property in top.properties)
                {
                    if (property.Name == "_internalIncrementingProperty")
                    {
                        found = true;
                    }
                }
                if (!found)
                {
                    top.properties.Add(internalIDproperty()); // adding the tracking ID property into the attribute so that we can squish them back together later
                }

                if (top.name != null && !unmatchedCloseNames.Remove(top.name))
                {
                    orphans.Push(top);
                }
            }

            // now at this point we should have no unmatched closes left
            // if we did it meant we popped all the way to the end of the stack and are at the root and STILL didn't find that close
            // at this point it's an error as they typoed the close marker
            if (unmatchedCloseNames.Count > 0)
            {
                foreach (var unmatched in unmatchedCloseNames)
                {
                    var message = $"asked to close {unmatched} markup but there is no corresponding opening. Is [/{unmatched}] a typo?";
                    errors.Add(new MarkupDiagnostic(message));
                }
                unmatchedCloseNames.Clear();
                return;
            }

            // now on the top of the stack we have the current common ancestor of all the orphans
            // we want to reparent them back onto the stack now as cousin clones of their original selves
            foreach (var template in orphans)
            {
                var clone = new MarkupTreeNode();
                clone.name = template.name;
                clone.properties = template.properties;
                clone.firstToken = template.firstToken;
                openNodes.Peek().children.Add(clone);
                openNodes.Push(clone);
            }
        }

        /// <inheritdoc cref="ParseString(string, string, bool, bool, bool)"/>
        public MarkupParseResult ParseString(string input, string localeCode, bool addImplicitCharacterAttribute = true)
        {
            return ParseString(input, localeCode, squish: true, sort: true, addImplicitCharacterAttribute);
        }

        /// <summary>
        /// Parses a string of text and produces a markup parse result.
        /// </summary>
        /// <param name="input">The text to parse.</param>
        /// <param name="localeCode">The locale to use when processing markers,
        /// as a BCP-47 locale tag.</param>
        /// <param name="squish">If <see langword="false"/>, markers that are
        /// split as part of the markup parsing process will not be re-merged
        /// before returning the result.</param>
        /// <param name="sort">If <see langword="true"/>, markers will be sorted based on their position in the input.å</param>
        /// <param name="addImplicitCharacterAttribute">If true, the parser will
        /// attempt to detect a character name in the text and add a
        /// <c>[character]</c> attribute for it.</param>
        /// <returns>A markup parse result.</returns>
        internal MarkupParseResult ParseString(string input, string localeCode, bool squish, bool sort, bool addImplicitCharacterAttribute)
        {
            return ParseStringWithDiagnostics(input, localeCode, squish, sort, addImplicitCharacterAttribute).markup;
        }

        private static readonly char[] trimChars = { ':', ' ' };
        private static readonly System.Text.RegularExpressions.Regex implicitCharacterRegex = new(@"^[^:]*:\s*");

        internal (MarkupParseResult markup, List<MarkupDiagnostic> diagnostics) ParseStringWithDiagnostics(string input, string localeCode, bool squish = true, bool sort = true, bool addImplicitCharacterAttribute = true)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            input = input.Normalize();
            var tokens = LexMarkup(input);
            var parseResult = BuildMarkupTreeFromTokens(tokens, input);

            // ok so at this point if parseResult.diagnostics is not empty we have lexing/parsing errors
            // it makes no sense to continue, just set the text to be the input so something exists
            if (parseResult.diagnostics.Count > 0)
            {
                var errorMarkup = new MarkupParseResult(
                    text: input,
                    attributes: new List<MarkupAttribute>()
                );

                return (errorMarkup, parseResult.diagnostics);
            }

            var builder = new StringBuilder();
            List<MarkupAttribute> attributes = new List<MarkupAttribute>();
            List<MarkupDiagnostic> diagnostics = new List<MarkupDiagnostic>();

            WalkAndProcessTree(parseResult.tree, builder, attributes, localeCode, diagnostics);

            if (squish)
            {
                SquishSplitAttributes(attributes);
            }

            var finalText = builder.ToString();

            if (addImplicitCharacterAttribute)
            {
                var hasCharacterAttributeAlready = false;
                foreach (var attribute in attributes)
                {
                    if (attribute.Name == "character")
                    {
                        hasCharacterAttributeAlready = true;
                        break;
                    }
                }
                if (!hasCharacterAttributeAlready)
                {
                    var match = implicitCharacterRegex.Match(finalText);
                    if (match.Success)
                    {
                        var characterName = match.Value.TrimEnd(trimChars);
                        var propertyList = new List<MarkupProperty>
                        {
                            new MarkupProperty("name", characterName),
                        };
                        var characterMarker = new MarkupAttribute(0, 0, match.Length, "character", propertyList);
                        attributes.Add(characterMarker);
                    }
                }
            }

            if (sort)
            {
                // finally we want them sorted by their position in the source code
                attributes.Sort((a, b) => a.SourcePosition.CompareTo(b.SourcePosition));
            }

            // one last check for any errors that might have been introduced by the rewriters
            // if that happens then again just return the input string
            if (diagnostics.Count > 0)
            {
                finalText = input;
                attributes.Clear();
            }

            var markup = new MarkupParseResult(
                text: finalText,
                attributes: attributes
            );

            return (markup, diagnostics);
        }

        /// <summary>
        /// Replaces all substitution markers in a text with the given
        /// substitution list.
        /// </summary>
        /// <remarks>
        /// This method replaces substitution markers - for example, <c>{0}</c>
        /// - with the corresponding entry in <paramref name="substitutions"/>.
        /// If <paramref name="text"/> contains a substitution marker whose
        /// index is not present in <paramref name="substitutions"/>, it is
        /// ignored.
        /// </remarks>
        /// <param name="text">The text containing substitution markers.</param>
        /// <param name="substitutions">The list of substitutions.</param>
        /// <returns><paramref name="text"/>, with the content from <paramref
        /// name="substitutions"/> inserted.</returns>
        public static string ExpandSubstitutions(string text, IList<string> substitutions)
        {
            if (substitutions == null)
            {
                // if we have no substitutions we want to just return the text as is
                return text;
            }
            if (text == null)
            {
                // we somehow have substitutions to apply but no text for them to be applied into?
                throw new ArgumentNullException(nameof(text), $"{nameof(text)} is null. Cannot apply substitutions to an empty string");
            }

            for (int i = 0; i < substitutions.Count; i++)
            {
                string substitution = substitutions[i];
                text = text.Replace("{" + i + "}", substitution);
            }

            return text;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.stringReader?.Dispose();
        }
    }

    /// <summary>
    /// A marker processor that handles the built-in markers <c>[select]</c>
    /// <c>[plural]</c>, and <c>[ordinal]</c>.
    /// </summary>
    public class BuiltInMarkupReplacer : IAttributeMarkerProcessor
    {
        private static readonly System.Text.RegularExpressions.Regex ValuePlaceholderRegex = new System.Text.RegularExpressions.Regex(@"(?<!\\)%");

        private static List<LineParser.MarkupDiagnostic> SelectReplace(MarkupAttribute marker, StringBuilder childBuilder, string value)
        {
            List<LineParser.MarkupDiagnostic> diagnostics = new List<LineParser.MarkupDiagnostic>();

            if (!marker.TryGetProperty(value, out MarkupValue replacementProp))
            {
                diagnostics.Add(new LineParser.MarkupDiagnostic($"no replacement value for {value} was found"));
                return diagnostics;
            }

            string replacement = replacementProp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            replacement = ValuePlaceholderRegex.Replace(replacement, value);
            childBuilder.Append(replacement);

            return diagnostics;
        }

        private static List<LineParser.MarkupDiagnostic> PluralReplace(MarkupAttribute marker, string localeCode, StringBuilder childBuilder, double numericValue)
        {
            List<LineParser.MarkupDiagnostic> diagnostics = new List<LineParser.MarkupDiagnostic>();

            // CLDRPlurals only works with 'neutral' locale names (i.e. "en"),
            // not 'specific' locale names. We need to check to see if
            // localeCode is the name of a 'specific' locale name. If is,
            // we'll fetch its parent, which will be 'neutral', and use that.
            string languageCode;
            try
            {
                var culture = new System.Globalization.CultureInfo(localeCode);
                if (culture.IsNeutralCulture)
                {
                    languageCode = culture.Name;
                }
                else
                {
                    culture = culture.Parent;
                    if (culture != null)
                    {
                        languageCode = culture.Name;
                    }
                    else
                    {
                        languageCode = localeCode;
                    }
                }
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                // lanugage code doesn't represent a known culture.
                // Fallback to using what the user provided and hope.
                languageCode = localeCode;
            }

            CLDRPlurals.PluralCase pluralCase;
            switch (marker.Name)
            {
                case "plural":
                    pluralCase = CLDRPlurals.NumberPlurals.GetCardinalPluralCase(languageCode, numericValue);
                    break;
                case "ordinal":
                    pluralCase = CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(languageCode, numericValue);
                    break;
                default:
                    diagnostics.Add(new LineParser.MarkupDiagnostic($"Unexpected pluralisation marker name {marker.Name}"));
                    return diagnostics;
            }

            string pluralCaseName = pluralCase.ToString().ToUpperInvariant();

            // Now that we know the plural case, we can select the
            // appropriate replacement text for it
            if (!marker.TryGetProperty(pluralCaseName, out MarkupValue replacementValue))
            {
                diagnostics.Add(new LineParser.MarkupDiagnostic($"no replacement for {numericValue}'s plural case of {pluralCaseName} was found."));
                return diagnostics;
            }

            if (replacementValue.Type != MarkupValueType.String)
            {
                diagnostics.Add(new($"select replacement values are expected to be strings, not {replacementValue.Type}", marker.Position));
            }

            string input = replacementValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            childBuilder.Append(ValuePlaceholderRegex.Replace(input, numericValue.ToString(System.Globalization.CultureInfo.CurrentCulture)));
            return diagnostics;
        }

        /// <inheritdoc/>
        public List<LineParser.MarkupDiagnostic> ProcessReplacementMarker(MarkupAttribute marker, StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode)
        {
            // we have somehow been given an invalid setup, can't continue so early out.
            if (childBuilder == null || childAttributes == null)
            {
                List<LineParser.MarkupDiagnostic> diagnostics = new()
                {
                    new($"Requested to perform replacement on '{marker.Name}', but haven't been given valid string builder or attributes.")
                };
                return diagnostics;
            }
            // all of these are self-closing tags, there is no sensible way to perform a replacement for anything else, so we early out here
            if (childBuilder.Length > 0 || childAttributes.Count > 0)
            {
                List<LineParser.MarkupDiagnostic> diagnostics = new()
                {
                    new LineParser.MarkupDiagnostic($"'{marker.Name}' markup only works on self-closing tags.")
                };
                return diagnostics;
            }
            if (marker.TryGetProperty("value", out MarkupValue valueProp) == false)
            {
                List<LineParser.MarkupDiagnostic> diagnostics = new()
                {
                    new LineParser.MarkupDiagnostic($"no 'value' property was found on the marker, {marker.Name} requires this to exist.")
                };
                return diagnostics;
            }

            switch (marker.Name)
            {
                case "select":
                    return SelectReplace(marker, childBuilder, valueProp.ToString(System.Globalization.CultureInfo.InvariantCulture));
                case "plural":
                case "ordinal":
                    {
                        switch (valueProp.Type)
                        {
                            case MarkupValueType.Integer:
                                return PluralReplace(marker, localeCode, childBuilder, valueProp.IntegerValue);
                            case MarkupValueType.Float:
                                return PluralReplace(marker, localeCode, childBuilder, valueProp.FloatValue);
                            default:
                                {
                                    List<LineParser.MarkupDiagnostic> diagnostics = new()
                                    {
                                        new LineParser.MarkupDiagnostic($"Asked to pluralise '{valueProp}' but this is a type that does not support pluralisation."),
                                    };
                                    return diagnostics;
                                }
                        }
                    }
                default:
                    {
                        List<LineParser.MarkupDiagnostic> diagnostics = new()
                        {
                        new LineParser.MarkupDiagnostic($"Asked to perform replacement for {marker.Name}, a marker we don't handle."),
                    };
                        return diagnostics;
                    }
            }
        }
    }
}
