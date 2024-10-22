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

    /// <summary>
    /// Parses text and produces markup information.
    /// </summary>
    public class LineParser : IMarkupParser
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
        /// <c>character</c> attriubte.
        /// </summary>
        /// <seealso cref="CharacterAttribute"/>
        public const string CharacterAttributeNameProperty = "name";

        /// <summary>
        /// The name of the property to use to signify that trailing
        /// whitespace should be trimmed if a tag had preceding whitespace
        /// or begins the line. This property must be a bool value.
        /// </summary>
        public const string TrimWhitespaceProperty = "trimwhitespace";
        public const string NoMarkupAttribute = "nomarkup";

        /// <summary>
        /// A regular expression that matches a colon followed by optional
        /// whitespace.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex EndOfCharacterMarker = new System.Text.RegularExpressions.Regex(@":\s*");

        /// <summary>
        /// A comparison function that sorts <see cref="MarkupAttribute"/>s
        /// by their source position.
        /// </summary>
        /// <returns>A value indicating the relative source position of the
        /// two attributes.</returns>
        private static readonly Comparison<MarkupAttribute> AttributePositionComparison = (x, y) => x.SourcePosition.CompareTo(y.SourcePosition);

        /// <summary>
        /// A dictionary that maps the names of attributes to an object
        /// that can generate replacement text for those attributes.
        /// </summary>
        private readonly Dictionary<string, IAttributeMarkerProcessor> markerProcessors = new Dictionary<string, IAttributeMarkerProcessor>();

        /// <summary>
        /// The original text that this line parser is parsing.
        /// </summary>
        private string input;

        /// <summary>
        /// A string reader used for reading through the <see
        /// cref="input"/>.
        /// </summary>
        private StringReader stringReader;

        /// <summary>
        /// The current position of the string reader in the plain text,
        /// measured in text elements.
        /// </summary>
        private int position;

        /// <summary>
        /// The current position of the string reader in the plain text,
        /// measured in characters.
        /// </summary>
        private int sourcePosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="LineParser"/>
        /// class.
        /// </summary>
        internal LineParser()
        {
            this.RegisterMarkerProcessor("nomarkup", new NoMarkupTextProcessor());
        }

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
        internal void RegisterMarkerProcessor(string attributeName, IAttributeMarkerProcessor markerProcessor)
        {
            if (this.markerProcessors.ContainsKey(attributeName))
            {
                throw new InvalidOperationException($"A marker processor for {attributeName} has already been registered.");
            }

            this.markerProcessors.Add(attributeName, markerProcessor);
        }
        
        public enum LexerTokenTypes
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
        }
        public class LexerToken
        {
            internal LexerTokenTypes type;
            internal int start;
            internal int end;
            internal int range
            {
                get
                {
                    return end + 1 - start;
                }
            }
        }
        private enum LexerMode
        {
            Text,
            Tag,
            Value,
        }

        public class TokenStream
        {
            // this just manages a list of tokens and their current iterator
            // purely a convenience class and it wraps a list and an int
            public List<LexerToken> tokens;
            public int iterator = 0;
            public LexerToken current
            {
                get
                {
                    if (iterator < 0)
                    {
                        iterator = 0;
                        var first = new LexerToken();
                        first.type = LexerTokenTypes.Start;
                        return first;
                    }
                    if (iterator > tokens.Count -1)
                    {
                        iterator = tokens.Count - 1;
                        var last = new LexerToken();
                        last.type = LexerTokenTypes.End;
                        return last;
                    }
                    return tokens[iterator];
                }
            }
            public LexerToken Next()
            {
                iterator += 1;
                return current;
            }
            public LexerToken Previous()
            {
                iterator -= 1;
                return current;
            }
            public void Consume(int number)
            {
                iterator += number;
            }
            public LexerToken Peek()
            {
                iterator += 1;
                var next = current;
                iterator -= 1;
                return next;
            }
            public LexerToken LookAhead(int number)
            {
                iterator += number;
                var lookAhead = current;
                iterator -= number;
                return lookAhead;
            }
            public bool ComparePattern(LexerTokenTypes[] pattern)
            {
                bool match = true;
                int currentIterator = iterator;
                // ok so we march through the list from the current position and say "yep this matches" until it doesn't
                foreach (var type in pattern)
                {
                    if (current.type == type)
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

        public List<LexerToken> LexMarkup(string input)
        {
            List<LexerToken> tokens = new List<LexerToken>();
            if (string.IsNullOrEmpty(input))
            {
                var start = new LexerToken()
                {
                    type = LexerTokenTypes.Start,
                    start = 0,
                    end = 0,
                };
                var end = new LexerToken()
                {
                    type = LexerTokenTypes.End,
                    start = 0,
                    end = 0,
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
            LexerToken last = new LexerToken()
            {
                type = LexerTokenTypes.Start,
                start = 0,
                end = 0,
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
                            if (last.type == LexerTokenTypes.Text)
                            {
                                var l = input[last.end];
                                if (l == '\\')
                                {
                                    goto default;
                                }
                            }

                            last = new LexerToken
                            {
                                type = LexerTokenTypes.OpenMarker,
                                start = currentPosition,
                                end = currentPosition
                            };
                            tokens.Add(last);
                            mode = LexerMode.Tag;
                            break;
                        }
                        default:
                        {
                            // if the last token is also a text we want to extend it
                            // otherwise we make a new text token
                            if (last.type == LexerTokenTypes.Text)
                            {
                                last.end = currentPosition;
                            }
                            else
                            {
                                last = new LexerToken
                                {
                                    type = LexerTokenTypes.Text,
                                    start = currentPosition,
                                    end = currentPosition
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
                            last = new LexerToken
                            {
                                type = LexerTokenTypes.CloseMarker,
                                start = currentPosition,
                                end = currentPosition
                            };
                            tokens.Add(last);
                            mode = LexerMode.Text;
                            break;
                        }
                        case '/':
                        {
                            last = new LexerToken
                            {
                                type = LexerTokenTypes.CloseSlash,
                                start = currentPosition,
                                end = currentPosition
                            };
                            tokens.Add(last);
                            break;
                        }
                        case '=':
                        {
                            last = new LexerToken
                            {
                                type = LexerTokenTypes.Equals,
                                start = currentPosition,
                                end = currentPosition,
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

                                last = new LexerToken
                                {
                                    type = LexerTokenTypes.Identifier,
                                    start = start,
                                    end = currentPosition,
                                };
                                tokens.Add(last);
                            }
                            else if (!char.IsWhiteSpace(c))
                            {
                                // if we are whitespace we likely want to just continue because it's most likely just spacing between identifiers
                                // the only time this isn't allowed is if they split the marker name, but that is a parser issue not a lexer issue
                                // so basically if we encounter a non-alphanumeric or non-whitespace we error
                                last = new LexerToken
                                {
                                    type = LexerTokenTypes.Error,
                                    start = currentPosition,
                                    end = currentPosition,
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
                        if (char.IsDigit(c))
                        {
                            var token = new LexerToken()
                            {
                                type = LexerTokenTypes.NumberValue,
                                start = currentPosition,
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
                                        token.type = LexerTokenTypes.Error;
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
                            if (float.TryParse(input.Substring(token.start, currentPosition + 1 - token.start), out _))
                            {
                                token.end = currentPosition;
                                tokens.Add(token);
                                last = token;
                            }
                            else
                            {
                                token.end = currentPosition;
                                token.type = LexerTokenTypes.Error;
                                tokens.Add(token);
                                last = token;
                            }

                            mode = LexerMode.Tag;
                        }
                        else if (c == '"')
                        {
                            // we are a " delimted string
                            // which means we want to go until the next " we find
                            var token = new LexerToken()
                            {
                                type = LexerTokenTypes.StringValue,
                                start = currentPosition,
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
                                    token.type = LexerTokenTypes.Error;
                                }
                                else
                                {
                                    // next holds the index of the next "
                                    // which means we need to read this many characters and advance currentPosition by that many
                                    var length = nextQuote - currentPosition;
                                    for (int i = 0; i < length; i++)
                                    {
                                        this.stringReader.Read();
                                    }
                                    currentPosition += length;
                                }
                            }
                            else
                            {
                                // the " is the last character
                                // which means we have an unclosed string and this is an error
                                token.type = LexerTokenTypes.Error;
                            }

                            //[a p="str\"ing"]s[/a]
                            //012345678901234567890

                            token.end = currentPosition;
                            last = token;
                            tokens.Add(token);
                            mode = LexerMode.Tag;
                        }
                        else
                        {
                            // finally we are now one of three possibilities
                            // true/false/generic alphanumeric text
                            var token = new LexerToken()
                            {
                                start = currentPosition,
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
                            var value = input.Substring(token.start, currentPosition + 1 - token.start);
                            if (value == "true" || value == "false")
                            {
                                token.type = LexerTokenTypes.BooleanValue;
                            }
                            else
                            {
                                token.type = LexerTokenTypes.StringValue;
                            }

                            token.end = currentPosition;
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
                    last = new LexerToken
                    {
                        start = currentPosition,
                        end = currentPosition,
                        type = LexerTokenTypes.Error
                    };
                    tokens.Add(last);
                }

                currentPosition += 1;
            }

            last = new LexerToken()
            {
                start = currentPosition,
                end = input.Length - 1,
                type = LexerTokenTypes.End
            };
            tokens.Add(last);

            return tokens;
        }

        public class MarkupTreeNode
        {
            public string name;
            public LexerToken firstToken;
            public List<MarkupTreeNode> children = new List<MarkupTreeNode>();
            public List<MarkupProperty> properties = new List<MarkupProperty>();
        }
        public class MarkupTextNode: MarkupTreeNode { public string text;}

        // this exists so that we have a way of seeing which attributes got split as part of the tree walking
        // this can then be used to merge them back together in the end
        static int internalIncrementingAttribute = 1;
        private MarkupProperty internalIDproperty()
        {
            var idProperty = new MarkupProperty("_internalIncrementingProperty", internalIncrementingAttribute);
            internalIncrementingAttribute += 1;

            return idProperty;
        }

        public Dictionary<string, TimsAttributeMarkerProcessor> rewriters = new Dictionary<string, TimsAttributeMarkerProcessor>();
        public void WalkTree(MarkupTreeNode root, System.Text.StringBuilder builder, List<MarkupAttribute> attributes, string localeCode, int offset = 0)
        {
            // text needs to just be added to the builder and continue with one exception
            // if our immediate older sibling is configured to consume whitespace we need to handle this
            if (root is MarkupTextNode)
            {
                var line = ((MarkupTextNode)root).text;
                // do we have ANY siblings
                if (attributes.Count > 0)
                {
                    var sibling = attributes[attributes.Count - 1];
                    if (sibling.Properties.TryGetValue(TrimWhitespaceProperty, out var value))
                    {
                        if (value.BoolValue)
                        {
                            // our older sibling has requested that we trim our leftmost whitespace
                            // but we only do that if our leftmost character is whitespace
                            // and we only trim a SINGLE whitespace
                            if (Char.IsWhiteSpace(line[0]) && line.Length > 1)
                            {
                                line = line.Substring(1);
                            }
                        }
                    }
                }
                // finally if we have any escaped markup we need to handle that now as well
                // which is just a find and replace of \[ and \] with [ ]
                line = line.Replace("\\[", "[");
                line = line.Replace("\\]", "]");
                builder.Append(line);
                return;
            }

            var childBuilder = new System.Text.StringBuilder();
            var childAttributes = new List<MarkupAttribute>();
            foreach (var child in root.children)
            {
                WalkTree(child, childBuilder, childAttributes, localeCode, builder.Length + offset);
            }

            // before we do anything else need to check if we are the true root
            if (string.IsNullOrEmpty(root.name))
            {
                // we are so we have nothing left to do, just add our children and be done
                builder.Append(childBuilder);
                attributes.AddRange(childAttributes);
                return;
            }

            // finally now our children have done their stuff so we can run our own rewriter if necessary
            // to do that we will need the combined finished string of all our children and their attributes
            if (rewriters.TryGetValue(root.name, out var rewriter))
            {
                // we now need to do the rewrite
                // so in this case we need to give the rewriter the combined child string and it's attributes
                // because it is up to you to fix any attributes if you modify them
                rewriter.ReplacementTextForMarker(root.name, childBuilder, childAttributes, localeCode);
            }
            else
            {
                // we aren't a replacement marker
                // which means we need to add ourselves as a tag
                // the source position one is easy enough, that is just the position of the first token (wait you never added these you dingus)
                // we know the length of all the children text because of the childBuilder so that gives us our range
                // and we know our relative start because of our siblings text in the builder
                MarkupAttribute attribute = new MarkupAttribute(builder.Length + offset, root.firstToken.start, childBuilder.Length, root.name, root.properties);
                attributes.Add(attribute);
            }

            // ok now at this stage inside childBuilder we have a valid modified (if was necessary) string
            // and our attributes have been added, all we need to do is add this to our siblings and continue
            builder.Append(childBuilder);
            attributes.AddRange(childAttributes);
        }
        public void SquishSplitAttributes(List<MarkupAttribute> attributes)
        {
            // grab every attribute that has a _internalIncrementingProperty property
            // then for every attribute with the same value of that property we merge them
            // and finally remove the _internalIncrementingProperty property
            List<int> removals = new List<int>();
            Dictionary<int, MarkupAttribute> merged = new Dictionary<int, MarkupAttribute>();
            for (int i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (attribute.Properties.TryGetValue("_internalIncrementingProperty", out var value))
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
        public (MarkupTreeNode, List<string>) BuildMarkupTreeFromTokens(List<LexerToken> tokens, string OG)
        {
            var tree = new MarkupTreeNode();
            List<string> errors = new List<string>();

            if (tokens == null || tokens.Count < 2)
            {
                errors.Add("There are not enough tokens to form a valid tree");
                return (tree, errors);
            }
            if (string.IsNullOrEmpty(OG))
            {
                errors.Add("There is a valid list of tokens but no original string");
                return (tree, errors);
            }
            if (tokens[0].type != LexerTokenTypes.Start && tokens[tokens.Count - 1].type != LexerTokenTypes.End)
            {
                errors.Add("The token list is malformed, it does not start and end with the correct tokens");
                return (tree, errors);
            }

            bool TryIntFromToken(LexerToken token, out int value)
            {
                var valueString = OG.Substring(token.start, token.range);

                if (int.TryParse(valueString, out value))
                {
                    return true;
                }

                value = 0;
                return false;
            }
            bool TryFloatFromToken(LexerToken token, out float value)
            {
                var valueString = OG.Substring(token.start, token.range);

                if (float.TryParse(valueString, out value))
                {
                    return true;
                }

                value = 0;
                return false;
            }
            bool TryBoolFromToken(LexerToken token, out bool value)
            {
                var valueString = OG.Substring(token.start, token.range);
                if (valueString == "true")
                {
                    value = true;
                    return true;
                }
                if (valueString == "false")
                {
                    value = false;
                    return true;
                }

                value = true;
                return false;
            }
            string ValueFromToken(LexerToken token)
            {
                var valueString = OG.Substring(token.start, token.range);
                if (valueString.StartsWith("\"", StringComparison.InvariantCulture) && valueString.EndsWith("\"", StringComparison.InvariantCulture))
                {
                    // if we are inside delimiters we will also need to remove any escaped characters
                    valueString = valueString.Replace("\\", string.Empty).Trim('"');
                }
                return valueString;
            }

            // [ / ]
            LexerTokenTypes[] closeAllPattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.CloseSlash, LexerTokenTypes.CloseMarker};
            // [ / ID ]
            LexerTokenTypes[] closeOpenAttributePattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.CloseSlash, LexerTokenTypes.Identifier, LexerTokenTypes.CloseMarker};
            // [ / ~( ID | ] ) 
            LexerTokenTypes[] closeErrorPattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.CloseSlash};
            // [ ID ]
            LexerTokenTypes[] openAttributePropertyLessPattern = { LexerTokenTypes.OpenMarker, LexerTokenTypes.Identifier, LexerTokenTypes.CloseMarker};
            // ID = VALUE
            LexerTokenTypes[] numberPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.NumberValue};
            LexerTokenTypes[] booleanPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.BooleanValue};
            LexerTokenTypes[] stringPropertyPattern = { LexerTokenTypes.Identifier, LexerTokenTypes.Equals, LexerTokenTypes.StringValue};
            // / ]
            LexerTokenTypes[] selfClosingAttributeEndPattern = { LexerTokenTypes.CloseSlash, LexerTokenTypes.CloseMarker};

            var stream = new TokenStream();
            stream.tokens = tokens;

            var openNodes = new Stack<MarkupTreeNode>();
            openNodes.Push(tree);

            var unmatchedCloses = new List<string>();

            while (stream.current.type != LexerTokenTypes.End)
            {
                var type = stream.current.type;

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
                        CleanUpUnmatchedCloses(openNodes, unmatchedCloses, errors);
                        break;
                    }
                    case LexerTokenTypes.Text:
                    {
                        // we are adding text to the tree
                        // but first we need to make sure there aren't any closes left to clean up
                        if (unmatchedCloses.Count > 0)
                        {
                            CleanUpUnmatchedCloses(openNodes, unmatchedCloses, errors);
                        }

                        var text = OG.Substring(stream.current.start, stream.current.end + 1 - stream.current.start);
                        var node = new MarkupTextNode()
                        {
                            text = text,
                            firstToken = stream.current,
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
                                _ = unmatchedCloses.Remove(openNodes.Pop().name);
                            }
                            foreach (var remaining in unmatchedCloses)
                            {
                                errors.Add($"asked to close {remaining} markup but there is no corresponding opening. Is [/{remaining}] a typo?");
                            }
                            unmatchedCloses.Clear();
                            break;
                        }
                        else if (stream.ComparePattern(closeOpenAttributePattern))
                        {
                            // it's a close an open attribute marker
                            var closeIDToken = stream.LookAhead(2);
                            var closeID = OG.Substring(closeIDToken.start, closeIDToken.range);
                            // eat the tokens we compared
                            stream.Consume(3);

                            // ok now we need to work out what we do if they don't match
                            // first up we need to get the current top of the stack
                            if (openNodes.Count == 1)
                            {
                                // this is an error, we can't close something when we only have the root node
                                errors.Add($"Asked to close {closeID}, but we don't have an open marker for it.");
                            }
                            else
                            {
                                // if they have the same name we are in luck
                                // we can pop this bad boy off the stack right now and continue
                                // if not then we add this to the list of unmatched closes for later clean up and continue
                                if (closeID == openNodes.Peek().name)
                                {
                                    var top = openNodes.Pop();
                                    // adding the tracking ID for squish purposes later although this one cannot be split...
                                    // top.properties.Add(internalIDAttribute());
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
                            var message = $"Error parsing markup, detected invalid token {stream.LookAhead(2).type}, following a close.";
                            errors.Add(message);
                            break;
                        }

                        // ok so now we are some variant of a regular open marker
                        // in that case we have to be one of:
                        // [ ID, [ ID =, [ nomarkup
                        // or an error of: [ *
                        
                        // which means if the next token isn't an ID it's an error so let's handle that first
                        if (stream.Peek().type != LexerTokenTypes.Identifier)
                        {
                            var message = $"Error parsing markup, detected invalid token {stream.Peek().type}, following an open marker.";
                            errors.Add(message);
                            break;
                        }

                        // ok so now we are a valid form of an open marker
                        // but before we can continue we need to make sure that the tree is correctly closed off
                        if (unmatchedCloses.Count > 0)
                        {
                            CleanUpUnmatchedCloses(openNodes, unmatchedCloses, errors);
                        }

                        var idToken = stream.Peek();
                        var id = OG.Substring(idToken.start, idToken.range);

                        // there are two slightly weird variants we will want to handle now
                        // the first is the nomarkup attribute, which completely changes the flow of the tool
                        if (stream.ComparePattern(openAttributePropertyLessPattern))
                        {
                            if (id == NoMarkupAttribute)
                            {
                                // so to get here we are [ nomarkup ]
                                // which mean the first token after is 3 tokens away
                                var tokenStart = stream.current;
                                var firstTokenAfterNoMarkup = stream.LookAhead(3);

                                // we spin in here eating tokens until we hit closeOpenAttributePattern
                                // when we do we stop and check if the id is nomarkupmarker
                                // if it is we stop and return that
                                // if we never find that we return an error instead
                                MarkupTreeNode nm = null;
                                while (stream.current.type != LexerTokenTypes.End)
                                {
                                    if (stream.ComparePattern(closeOpenAttributePattern))
                                    {
                                        // [ / id ]
                                        var nmIDToken = stream.LookAhead(2);
                                        if (OG.Substring(nmIDToken.start, nmIDToken.range) == NoMarkupAttribute)
                                        {
                                            // we have found the end of the nomarkup marker
                                            // create a new text node
                                            // assign it as the child of the markup node
                                            // return this
                                            var text = new MarkupTextNode()
                                            {
                                                text = OG.Substring(firstTokenAfterNoMarkup.start, stream.current.start - firstTokenAfterNoMarkup.start),
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
                                    errors.Add($"we entered nomarkup mode but didn't find an exit token");
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
                                completeMarker.firstToken = stream.current;
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
                        marker.firstToken = stream.current;

                        openNodes.Peek().children.Add(marker);
                        openNodes.Push(marker);

                        if (stream.LookAhead(2).type != LexerTokenTypes.Equals)
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
                        string id = OG.Substring(stream.current.start, stream.current.range);

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
                                errors.Add($"failed to convert the value {stream.LookAhead(2)} into a valid property");
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
                                errors.Add($"failed to convert the value {stream.LookAhead(2)} into a valid property");
                                break;
                            }
                        }
                        else if (stream.ComparePattern(stringPropertyPattern))
                        {
                            string sValue = ValueFromToken(stream.LookAhead(2));
                            openNodes.Peek().properties.Add(new MarkupProperty(id, sValue));
                        }
                        else
                        {
                            errors.Add("attempted to match a id = value grouping but failed");
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
                            errors.Add($"Encountered a rogue closing slash at {stream.current.start}");
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
                errors.Add(line);
            }
            if (unmatchedCloses.Count > 1)
            {
                var line = "parsing finished with unmatched closes still remaining: ";
                foreach (var unmatched in unmatchedCloses)
                {
                    line += " /[" + unmatched + "]";
                }
                errors.Add(line);
            }

            return (tree, errors);
        }
        
        // this cleans up and rebalances the tree for misclosed or invalid closing patterns like the following:
        // This [a] is [b] some markup [/a][/b] invalid structure.
        // This [a] is [b] some [c] nested [/a] markup [/c] with [/b] invalid structure.
        // [z] this [a] is [b] some [c] markup [d] with [e] both [/c][/e][/d][/a][/z] misclosed tags and double unclosable tags[/b]
        // it is a variant of the adoption agency algorithm
        private void CleanUpUnmatchedCloses(Stack<MarkupTreeNode> openNodes, List<string> unmatchedCloseNames, List<string> errors)
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

                if (!unmatchedCloseNames.Remove(top.name))
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
                    errors.Add($"asked to close {unmatched} markup but there is no corresponding opening. Is [/{unmatched}] a typo?");
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

        public MarkupParseResult TimsParse(string input, string localeCode, bool squish = true, bool sort = true)
        {
            var tokens = LexMarkup(input);
            var parseResult = BuildMarkupTreeFromTokens(tokens, input);

            var builder = new System.Text.StringBuilder();
            List<MarkupAttribute> attributes = new List<MarkupAttribute>();
            WalkTree(parseResult.Item1, builder, attributes, localeCode);
            
            if (squish)
            {
                SquishSplitAttributes(attributes);
            }
            if (sort)
            {
                // finally we want them sorted by their position in the source code
                attributes.Sort((a,b) => a.SourcePosition.CompareTo(b.SourcePosition));
            }
            
            return new MarkupParseResult
            {
                Text = builder.ToString(),
                Attributes = attributes,
            };
        }

        /// <summary>Parses a line of text, and produces a
        /// <see cref="MarkupParseResult"/> containing the processed
        /// text.</summary>
        /// <param name="input">The text to parse.</param>
        /// <returns>The resulting markup information.</returns>
        public MarkupParseResult ParseMarkup(string input, string localeCode)
        {
            if (string.IsNullOrEmpty(input))
            {
                // We got a null input; return an empty markup parse result
                return new MarkupParseResult
                {
                    Text = string.Empty,
                    Attributes = new List<MarkupAttribute>(),
                };
            }

            this.input = input.Normalize();

            this.stringReader = new StringReader(this.input);

            var stringBuilder = new StringBuilder();

            var markers = new List<MarkupAttributeMarker>();

            int nextCharacter;

            char lastCharacter = char.MinValue;

            // Read the entirety of the line
            while ((nextCharacter = this.stringReader.Read()) != -1)
            {
                char c = (char)nextCharacter;

                if (c == '\\')
                {
                    // This may be the start of an escaped bracket ("\[" or
                    // "\]"). Peek ahead to see if it is.
                    var nextC = (char)this.stringReader.Peek();

                    if (nextC == '[' || nextC == ']')
                    {
                        // It is! We'll discard this '\', and read the next
                        // character as plain text.
                        c = (char)this.stringReader.Read();
                        stringBuilder.Append(c);
                        this.sourcePosition += 1;
                        continue;
                    }
                    else
                    {
                        // It wasn't an escaped bracket. Continue on, and
                        // parse the '\' as a normal character.
                    }
                }

                if (c == '[')
                {
                    // How long is our current string, in text elements
                    // (i.e. visible glyphs)?
                    this.position = new System.Globalization.StringInfo(stringBuilder.ToString()).LengthInTextElements;

                    // The start of a marker!
                    MarkupAttributeMarker marker = this.ParseAttributeMarker();

                    markers.Add(marker);

                    var hadPrecedingWhitespaceOrLineStart = this.position == 0 || char.IsWhiteSpace(lastCharacter);

                    bool wasReplacementMarker = false;

                    // Is this a replacement marker?
                    if (marker.Name != null && this.markerProcessors.ContainsKey(marker.Name))
                    {
                        wasReplacementMarker = true;

                        // Process it and get the replacement text!
                        var replacementText = this.ProcessReplacementMarker(marker, localeCode);

                        // Insert it into our final string and update our
                        // position accordingly
                        stringBuilder.Append(replacementText);
                    }

                    bool trimWhitespaceIfAble = false;

                    if (hadPrecedingWhitespaceOrLineStart)
                    {
                        // By default, self-closing markers will trim a
                        // single trailing whitespace after it if there was
                        // preceding whitespace. This doesn't happen if the
                        // marker was a replacement marker, or it has a
                        // property "trimwhitespace" (which must be
                        // boolean) set to false. All markers can opt-in to
                        // trailing whitespace trimming by having a
                        // 'trimwhitespace' property set to true.
                        if (marker.Type == TagType.SelfClosing)
                        {
                            trimWhitespaceIfAble = !wasReplacementMarker;
                        }

                        if (marker.TryGetProperty(TrimWhitespaceProperty, out var prop))
                        {
                            if (prop.Type != MarkupValueType.Bool)
                            {
                                throw new MarkupParseException($"Error parsing line {this.input}: attribute {marker.Name} at position {this.position} has a {prop.Type.ToString().ToLower()} property \"{TrimWhitespaceProperty}\" - this property is required to be a boolean value.");
                            }

                            trimWhitespaceIfAble = prop.BoolValue;
                        }
                    }

                    if (trimWhitespaceIfAble)
                    {
                        // If there's trailing whitespace, and we want to
                        // remove it, do so
                        if (this.PeekWhitespace())
                        {
                            // Consume the single trailing whitespace
                            // character (and don't update position)
                            this.stringReader.Read();
                            this.sourcePosition += 1;
                        }
                    }
                }
                else
                {
                    // plain text! add it to the resulting string and
                    // advance the parser's plain-text position
                    stringBuilder.Append(c);
                    this.sourcePosition += 1;
                }
                
                lastCharacter = c;
            }

            var attributes = this.BuildAttributesFromMarkers(markers);

            var characterAttributeIsPresent = false;
            foreach (var attribute in attributes)
            {
                if (attribute.Name == CharacterAttribute)
                {
                    characterAttributeIsPresent = true;
                }
            }

            if (characterAttributeIsPresent == false)
            {
                // Attempt to generate a character attribute from the start
                // of the string to the first colon
                var match = EndOfCharacterMarker.Match(this.input);

                if (match.Success)
                {
                    var endRange = match.Index + match.Length;
                    var characterName = this.input.Substring(0, match.Index);

                    MarkupValue nameValue = new MarkupValue
                    {
                        Type = MarkupValueType.String,
                        StringValue = characterName,
                    };

                    MarkupProperty nameProperty = new MarkupProperty(CharacterAttributeNameProperty, nameValue);

                    var characterAttribute = new MarkupAttribute(0, 0, endRange, CharacterAttribute, new[] { nameProperty });

                    attributes.Add(characterAttribute);
                }
            }

            return new MarkupParseResult
            {
                Text = stringBuilder.ToString(),
                Attributes = attributes,
            };
        }

        /// <summary>
        /// Parses a marker and generates replacement text to insert into
        /// the plain text.
        /// </summary>
        /// <param name="marker">The marker to parse.</param>
        /// <param name="localeCode">The locale code to use when processing the marker.</param>
        /// <returns>The replacement text to insert.</returns>
        private string ProcessReplacementMarker(MarkupAttributeMarker marker, string localeCode)
        {
            // If it's not an open or self-closing marker, we have no text
            // to insert, so return the empty string
            if (marker.Type != TagType.Open && marker.Type != TagType.SelfClosing)
            {
                return string.Empty;
            }

            // this is an attribute that we want to replace with text!

            // if this is an opening marker, we read up to the closing
            // marker, the close-all marker, or the end of the string; this
            // becomes the value of a property called "contents", and then
            // we perform the replacement
            if (marker.Type == TagType.Open)
            {
                // Read everything up to the closing tag
                string markerContents = this.ParseRawTextUpToAttributeClose(marker.Name);

                // Add this as a property
                marker.Properties.Add(
                    new MarkupProperty(
                        ReplacementMarkerContents,
                        new MarkupValue
                        {
                            StringValue = markerContents,
                            Type = MarkupValueType.String,
                        }));
            }

            // Fetch the text that should be inserted into the string at
            // this point
            var replacementText = this.markerProcessors[marker.Name].ReplacementTextForMarker(marker, localeCode);

            return replacementText;
        }

        /// <summary>
        /// Parses text up to either a close marker with the given name, or
        /// a close-all marker.
        /// </summary>
        /// <remarks>
        /// The closing marker itself is not included in the returned text.
        /// </remarks>
        /// <param name="name">The name of the close marker to look
        /// for.</param>
        /// <returns>The text up to the closing marker.</returns>
        private string ParseRawTextUpToAttributeClose(string name)
        {
            var remainderOfLine = this.stringReader.ReadToEnd();

            // Parse up to either [/name] or [/], allowing whitespace
            // between any elements.
            var match = System.Text.RegularExpressions.Regex.Match(remainderOfLine, $@"\[\s*\/\s*({name})?\s*\]");

            // If we didn't find it, then there's no closing marker, and
            // that's an error!
            if (match.Success == false)
            {
                throw new MarkupParseException($"Unterminated marker {name} in line {this.input} at position {this.position}");
            }

            // Split the line into the part up to the closing tag, and the
            // part afterwards
            var closeMarkerPosition = match.Index;

            var rawTextSubstring = remainderOfLine.Substring(0, closeMarkerPosition);
            var lineAfterRawText = remainderOfLine.Substring(closeMarkerPosition);

            // We've consumed all of this text in the string reader, so to
            // make it possible to parse the rest, we need to create a new
            // string reader with the remaining text
            this.stringReader = new StringReader(lineAfterRawText);

            return rawTextSubstring;
        }

        /// <summary>
        /// Creates a list of <see cref="MarkupAttribute"/>s from loose
        /// <see cref="MarkupAttributeMarker"/>s.
        /// </summary>
        /// <param name="markers">The collection of markers.</param>
        /// <returns>The list of attributes.</returns>
        /// <throws cref="MarkupParseException">Thrown when a close marker
        /// is encountered, but no corresponding open marker for it
        /// exists.</throws>
        private List<MarkupAttribute> BuildAttributesFromMarkers(List<MarkupAttributeMarker> markers)
        {
            // Using a linked list here because we want to append to the
            // front and be able to walk through it easily
            var unclosedMarkerList = new LinkedList<MarkupAttributeMarker>();

            var attributes = new List<MarkupAttribute>(markers.Count);

            foreach (var marker in markers)
            {
                switch (marker.Type)
                {
                    case TagType.Open:
                        // A new marker! Add it to the unclosed list at the
                        // start (because there's a high chance that it
                        // will be closed soon).
                        unclosedMarkerList.AddFirst(marker);
                        break;
                    case TagType.Close:
                        {
                            // A close marker! Walk back through the
                            // unclosed stack to find the most recent
                            // marker of the same type to find its pair.
                            MarkupAttributeMarker matchedOpenMarker = default;
                            foreach (var openMarker in unclosedMarkerList)
                            {
                                if (openMarker.Name == marker.Name)
                                {
                                    // Found a corresponding open!
                                    matchedOpenMarker = openMarker;
                                    break;
                                }
                            }

                            if (matchedOpenMarker.Name == null)
                            {
                                throw new MarkupParseException($"Unexpected close marker {marker.Name} at position {marker.Position} in line {this.input}");
                            }

                            // This attribute is now closed, so we can
                            // remove the marker from the unmatched list
                            unclosedMarkerList.Remove(matchedOpenMarker);

                            // We can now construct the attribute!
                            var length = marker.Position - matchedOpenMarker.Position;
                            var attribute = new MarkupAttribute(matchedOpenMarker, length);

                            attributes.Add(attribute);
                        }

                        break;
                    case TagType.SelfClosing:
                        {
                            // Self-closing markers create a zero-length
                            // attribute where they appear
                            var attribute = new MarkupAttribute(marker, 0);
                            attributes.Add(attribute);
                        }

                        break;
                    case TagType.CloseAll:
                        {
                            // Close all currently open markers

                            // For each marker that we currently have open,
                            // this marker has closed it, so create an
                            // attribute for it
                            foreach (var openMarker in unclosedMarkerList)
                            {
                                var length = marker.Position - openMarker.Position;
                                var attribute = new MarkupAttribute(openMarker, length);

                                attributes.Add(attribute);
                            }

                            // We've now closed all markers, so we can
                            // clear the unclosed list now
                            unclosedMarkerList.Clear();
                        }

                        break;
                }
            }

            attributes.Sort(AttributePositionComparison);

            return attributes;
        }

        /// <summary>
        /// Parses an open, close, self-closing, or close-all attribute
        /// marker.
        /// </summary>
        /// <returns>The parsed marker.</returns>
        private MarkupAttributeMarker ParseAttributeMarker()
        {
            var sourcePositionAtMarkerStart = this.sourcePosition;

            // We have already consumed the start of the marker '[' before
            // we enter here. Increment the sourcePosition counter to
            // account for it.
            this.sourcePosition += 1;

            // Next, start parsing from the characters that can appear
            // inside the marker
            if (this.Peek('/'))
            {
                // This is either the start of a closing tag or the start
                // of the 'close-all' tag
                this.ParseCharacter('/');

                if (this.Peek(']'))
                {
                    // It's the close-all tag!
                    this.ParseCharacter(']');
                    return new MarkupAttributeMarker(null, this.position, sourcePositionAtMarkerStart, new List<MarkupProperty>(), TagType.CloseAll);
                }
                else
                {
                    // It's a named closing tag!
                    var tagName = this.ParseID();
                    this.ParseCharacter(']');
                    return new MarkupAttributeMarker(tagName, this.position, sourcePositionAtMarkerStart, new List<MarkupProperty>(), TagType.Close);
                }
            }

            // If we're here, this is either an opening tag, or a
            // self-closing tag.

            // If the opening ID is not provided, the name of the attribute
            // is taken from the first property.

            // Tags always start with an ID, which is used as the name of
            // the attribute.
            string attributeName = this.ParseID();

            var properties = new List<MarkupProperty>();

            // If the ID was immediately followed by an '=', this was the
            // first property (its value is also used as the attribute
            // name.)
            if (this.Peek('='))
            {
                // This is also the first property!

                // Parse the rest of the property now before we parse any
                // others.
                this.ParseCharacter('=');
                var value = this.ParseValue();
                properties.Add(new MarkupProperty(attributeName, value));
            }

            // parse all remaining properties
            while (true)
            {
                this.ConsumeWhitespace();
                var next = this.stringReader.Peek();
                this.AssertNotEndOfInput(next);

                if ((char)next == ']')
                {
                    // End of an Opening tag.
                    this.ParseCharacter(']');
                    return new MarkupAttributeMarker(attributeName, this.position, sourcePositionAtMarkerStart, properties, TagType.Open);
                }

                if ((char)next == '/')
                {
                    // End of a self-closing tag.
                    this.ParseCharacter('/');
                    this.ParseCharacter(']');
                    return new MarkupAttributeMarker(attributeName, this.position, sourcePositionAtMarkerStart, properties, TagType.SelfClosing);
                }

                // Expect another property.
                var propertyName = this.ParseID();
                this.ParseCharacter('=');
                var propertyValue = this.ParseValue();

                properties.Add(new MarkupProperty(propertyName, propertyValue));
            }
        }

        /// <summary>
        /// Parses a property value.
        /// </summary>
        /// <remarks>
        /// Permitted value types are:
        ///
        /// <list type="bullet">
        /// <item>Integers</item>
        ///
        /// <item>Floating-point numbers</item>
        ///
        /// <item>Strings (delimited by double quotes). (Strings may contain
        /// escaped quotes with a backslash.)</item>
        ///
        /// <item>The words <c>true</c> or <c>false</c>
        /// </item>
        ///
        /// <item>Runs of alphanumeric characters, up to but not including a
        /// whitespace or the end of a tag; these are interpreted as a string
        /// (e.g. <c>[mood=happy]</c> is interpreted the same as
        /// <c>[mood="happy"]</c>
        /// </item>
        ///
        /// <item>Expressions (delimited by curly braces), which are processed
        /// as inline expressions.
        /// </item>
        /// </list>
        /// </remarks>
        /// <returns>The parsed value.</returns>
        private MarkupValue ParseValue()
        {
            // parse integers or floats:
            if (this.PeekNumeric())
            {
                // could be an int or a float
                var integer = this.ParseInteger();

                // if there's a decimal separator, this is a float
                if (this.Peek('.'))
                {
                    // a float
                    this.ParseCharacter('.');

                    // parse the fractional value
                    var fraction = this.ParseInteger();

                    // convert it to a float
                    var fractionDigits = fraction.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
                    float floatValue = integer + (float)(fraction / Math.Pow(10, fractionDigits));

                    return new MarkupValue { FloatValue = floatValue, Type = MarkupValueType.Float };
                }
                else
                {
                    // an integer
                    return new MarkupValue { IntegerValue = integer, Type = MarkupValueType.Integer };
                }
            }

            if (this.Peek('"'))
            {
                // a string
                var stringValue = this.ParseString();

                return new MarkupValue { StringValue = stringValue, Type = MarkupValueType.String };
            }

            var word = this.ParseID();

            // This ID is expected to be 'true', 'false', or something
            // else. if it's 'true' or 'false', interpret it as a bool.
            if (word.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return new MarkupValue { BoolValue = true, Type = MarkupValueType.Bool };
            }
            else if (word.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return new MarkupValue { BoolValue = false, Type = MarkupValueType.Bool };
            }
            else
            {
                // interpret this as a one-word string
                return new MarkupValue { StringValue = word, Type = MarkupValueType.String };
            }
        }

        /// <summary>
        /// Peeks ahead in the LineParser's input without consuming any
        /// characters.
        /// </summary>
        /// <remarks>
        /// This method returns false if the parser has reached the end of
        /// the line.
        /// </remarks>
        /// <param name="expectedCharacter">The character to look
        /// for.</param>
        /// <returns>True if the next character is <paramref
        /// name="expectedCharacter"/>, false otherwise.</returns>
        private bool Peek(char expectedCharacter)
        {
            this.ConsumeWhitespace();
            var next = this.stringReader.Peek();
            if (next == -1)
            {
                return false;
            }

            return (char)next == expectedCharacter;
        }

        /// <summary>
        /// Peeks ahead in the LineParser's input without consuming any
        /// characters, looking for whitespace.
        /// </summary>
        /// <remarks>
        /// This method returns false if the parser has reached the end of
        /// the line.
        /// </remarks>
        /// <returns>True if the next character is whitespace, false
        /// otherwise.</returns>
        private bool PeekWhitespace()
        {
            var next = this.stringReader.Peek();
            if (next == -1)
            {
                return false;
            }

            return char.IsWhiteSpace((char)next);
        }

        /// <summary>
        /// Peeks ahead in the LineParser's input without consuming any
        /// characters, looking for numeric characters.
        /// </summary>
        /// <remarks>
        /// This method returns false if the parser has reached the end of
        /// the line.
        /// </remarks>
        /// <returns>True if the next character is numeric, false
        /// otherwise.</returns>
        private bool PeekNumeric()
        {
            this.ConsumeWhitespace();
            var next = this.stringReader.Peek();
            if (next == -1)
            {
                return false;
            }

            return char.IsDigit((char)next);
        }

        /// <summary>
        /// Parses an integer from the stream.
        /// </summary>
        /// <remarks>
        /// This method returns false if the parser has reached the end of
        /// the line.
        /// </remarks>
        /// <returns>True if the next character is numeric, false
        /// otherwise.</returns>
        private int ParseInteger()
        {
            this.ConsumeWhitespace();

            StringBuilder intBuilder = new StringBuilder();

            while (true)
            {
                var tempNext = this.stringReader.Peek();
                this.AssertNotEndOfInput(tempNext);
                var nextChar = (char)tempNext;

                if (char.IsDigit(nextChar))
                {
                    this.stringReader.Read();
                    intBuilder.Append(nextChar);
                    this.sourcePosition += 1;
                }
                else
                {
                    // end of the integer! parse and return it
                    return int.Parse(intBuilder.ToString(), System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        private string ParseID()
        {
            this.ConsumeWhitespace();
            var idStringBuilder = new StringBuilder();

            // Read the first character, which must be a letter, number, or underscore
            int tempNext = this.stringReader.Read();
            this.sourcePosition += 1;
            this.AssertNotEndOfInput(tempNext);
            char nextChar = (char)tempNext;

            if (char.IsSurrogate(nextChar))
            {
                var nextNext = this.stringReader.Read();
                this.sourcePosition += 1;
                this.AssertNotEndOfInput(nextNext);
                var nextNextChar = (char)nextNext;

                // FIXME: This assumes that all surrogate pairs are
                // 'letters', which may not be the case.
                idStringBuilder.Append(nextChar);
                idStringBuilder.Append(nextNextChar);
            }
            else if (char.IsLetterOrDigit(nextChar) || nextChar == '_')
            {
                idStringBuilder.Append((char)tempNext);
            }
            else
            {
                throw new ArgumentException($"Expected an identifier inside markup in line \"{this.input}\"");
            }

            // Read zero or more letters, numbers, or underscores
            while (true)
            {
                tempNext = this.stringReader.Peek();
                if (tempNext == -1)
                {
                    break;
                }

                nextChar = (char)tempNext;

                if (char.IsSurrogate(nextChar))
                {
                    this.stringReader.Read(); // consume this char
                    this.sourcePosition += 1;

                    // consume the next character, which we expect to be a
                    // surrogate pair
                    var nextNext = this.stringReader.Read();
                    this.sourcePosition += 1;
                    this.AssertNotEndOfInput(nextNext);
                    var nextNextChar = (char)nextNext;

                    // This assumes that all surrogate pairs are 'letters',
                    // which may not be the case.
                    idStringBuilder.Append(nextChar);
                    idStringBuilder.Append(nextNextChar);
                }
                else if (char.IsLetterOrDigit(nextChar) || (char)tempNext == '_')
                {
                    idStringBuilder.Append((char)tempNext);
                    this.stringReader.Read(); // consume it
                    this.sourcePosition += 1;
                }
                else
                {
                    // no more
                    break;
                }
            }

            return idStringBuilder.ToString();
        }

        private string ParseString()
        {
            this.ConsumeWhitespace();

            var stringStringBuilder = new StringBuilder();

            int tempNext = this.stringReader.Read();
            this.AssertNotEndOfInput(tempNext);
            this.sourcePosition += 1;

            char nextChar = (char)tempNext;
            if (nextChar != '"')
            {
                throw new ArgumentException($"Expected a string inside markup in line {this.input}");
            }

            while (true)
            {
                tempNext = this.stringReader.Read();
                this.AssertNotEndOfInput(tempNext);
                this.sourcePosition += 1;
                nextChar = (char)tempNext;

                if (nextChar == '"')
                {
                    // end of string - consume it but don't append to the
                    // final collection
                    break;
                }
                else if (nextChar == '\\')
                {
                    // an escaped quote or backslash
                    int nextNext = this.stringReader.Read();
                    this.AssertNotEndOfInput(nextNext);
                    this.sourcePosition += 1;
                    char nextNextChar = (char)nextNext;
                    if (nextNextChar == '\\' || nextNextChar == '"')
                    {
                        stringStringBuilder.Append(nextNextChar);
                    }
                }
                else
                {
                    stringStringBuilder.Append(nextChar);
                }
            }

            return stringStringBuilder.ToString();
        }

        private void ParseCharacter(char character)
        {
            this.ConsumeWhitespace();

            int tempNext = this.stringReader.Read();
            this.AssertNotEndOfInput(tempNext);
            if ((char)tempNext != character)
            {
                throw new MarkupParseException($"Expected a {character} inside markup in line \"{this.input}\"");
            }

            this.sourcePosition += 1;
        }

        /// <summary>
        /// Throws an exception if <paramref name="value"/> is the
        /// end-of-line character.
        /// </summary>
        /// <param name="value">The character to test, as an
        /// integer.</param>
        /// <throws cref="MarkupParseException">Thrown when <paramref
        /// name="value"/> is the end-of-line character.</throws>
        private void AssertNotEndOfInput(int value)
        {
            if (value == -1)
            {
                throw new MarkupParseException($"Unexpected end of line inside markup in line \"{this.input}");
            }
        }

        /// <summary>
        /// Reads and discards whitespace, up to the first non-whitespace
        /// character.
        /// </summary>
        /// <param name="allowEndOfLine">If <see langword="false"/>, a <see
        /// cref="MarkupParseException"/> will be thrown if the end of the line
        /// is reached while reading whitespace.</param>
        /// <throws cref="MarkupParseException">Thrown when the end of the line
        /// is reached, and <paramref name="allowEndOfLine"/> is <see
        /// langword="false"/>.</throws>
        private void ConsumeWhitespace(bool allowEndOfLine = false)
        {
            while (true)
            {
                var tempNext = this.stringReader.Peek();
                if (tempNext == -1 && allowEndOfLine == false)
                {
                    throw new MarkupParseException($"Unexpected end of line inside markup in line \"{this.input}");
                }

                if (char.IsWhiteSpace((char)tempNext) == true)
                {
                    // consume it and continue
                    this.stringReader.Read();
                    this.sourcePosition += 1;
                }
                else
                {
                    // no more whitespace ahead; don't consume it, but
                    // instead stop eating whitespace
                    return;
                }
            }
        }
    }
}
