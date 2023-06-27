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
    internal class LineParser
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

        /// <summary>Parses a line of text, and produces a
        /// <see cref="MarkupParseResult"/> containing the processed
        /// text.</summary>
        /// <param name="input">The text to parse.</param>
        /// <returns>The resulting markup information.</returns>
        internal MarkupParseResult ParseMarkup(string input)
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
                        var replacementText = this.ProcessReplacementMarker(marker);

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
        /// <returns>The replacement text to insert.</returns>
        private string ProcessReplacementMarker(MarkupAttributeMarker marker)
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
            var replacementText = this.markerProcessors[marker.Name].ReplacementTextForMarker(marker);

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
