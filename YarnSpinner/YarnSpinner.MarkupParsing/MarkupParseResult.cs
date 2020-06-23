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

namespace Yarn.MarkupParsing
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The result of parsing a line of marked-up text.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by the <see cref="LineParser.ParseMarkup"/> method.
    /// </remarks>
    public struct MarkupParseResult
    {
        /// <summary>
        /// The original text, with all parsed markers removed.
        /// </summary>
        public string Text;

        /// <summary>
        /// The list of <see cref="MarkupAttribute"/>s in this parse
        /// result.
        /// </summary>
        public List<MarkupAttribute> Attributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupParseResult"/> struct.
        /// </summary>
        /// <param name="text">The plain text.</param>
        /// <param name="attributes">The list of attributes.</param>
        internal MarkupParseResult(string text, List<MarkupAttribute> attributes)
        {
            this.Text = text;
            this.Attributes = attributes;
        }

        /// <summary>
        /// Gets the first attribute with the specified name, if present.
        /// </summary>
        /// <param name="name">The name of the attribute to get.</param>
        /// <param name="attribute">When this method returns, contains the
        /// attribute with the specified name, if the attribute is found;
        /// otherwise, the default <see cref="MarkupAttribute"/>. This
        /// parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if the <see
        /// cref="MarkupParseResult"/> contains an attribute with the
        /// specified name; otherwise, <see langword="false"/>.</returns>
        public bool TryGetAttributeWithName(string name, out MarkupAttribute attribute)
        {
            foreach (var a in this.Attributes)
            {
                if (a.Name == name)
                {
                    attribute = a;
                    return true;
                }
            }

            attribute = default;
            return false;
        }
    }

    /// <summary>
    /// Represents a range of text in a marked-up string.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by the <see cref="LineParser.ParseMarkup"/> method.
    /// </remarks>
    public struct MarkupAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupAttribute"/>
        /// struct using specified values.
        /// </summary>
        /// <param name="position">The position in the plain text where
        /// this attribute begins.</param>
        /// <param name="length">The number of text elements in the plain
        /// text that this attribute covers.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="properties">The properties associated with this
        /// attribute.</param>
        internal MarkupAttribute(int position, int length, string name, IEnumerable<MarkupProperty> properties)
        {
            this.Position = position;
            this.Length = length;
            this.Name = name;

            var props = new Dictionary<string, MarkupValue>();

            foreach (var prop in properties)
            {
                props.Add(prop.Name, prop.Value);
            }

            this.Properties = props;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupAttribute"/>
        /// struct, using information taken from an opening <see
        /// cref="MarkupAttributeMarker"/>.
        /// </summary>
        /// <param name="openingMarker">The marker that represents the
        /// start of this attribute.</param>
        /// <param name="length">The number of text elements in the plain
        /// text that this attribute covers.</param>
        internal MarkupAttribute(MarkupAttributeMarker openingMarker, int length)
        : this(openingMarker.Position, length, openingMarker.Name, openingMarker.Properties)
        {
        }

        /// <summary>
        /// Gets the position in the plain text where
        /// this attribute begins.
        /// </summary>
        public int Position { get; internal set; }

        /// <summary>
        /// Gets the number of text elements in the plain
        /// text that this attribute covers.
        /// </summary>
        public int Length { get; internal set; }

        /// <summary>
        /// Gets the name of the attribute.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the properties associated with this
        /// attribute.
        /// </summary>
        public IReadOnlyDictionary<string, MarkupValue> Properties { get; internal set; }
    }

    /// <summary>
    /// A property associated with a <see cref="MarkupAttribute"/>.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by the <see cref="LineParser.ParseMarkup"/> method.
    /// </remarks>
    public struct MarkupProperty
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupProperty"/>
        /// struct.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        internal MarkupProperty(string name, MarkupValue value)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public MarkupValue Value { get; private set; }
    }

    /// <summary>
    /// A value associated with a <see cref="MarkupProperty"/>.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by the <see cref="LineParser.ParseMarkup"/> method.
    /// </remarks>
    public struct MarkupValue
    {
        /// <summary>Gets the integer value of this property.</summary>
        /// <remarks>
        /// This property is only defined when the property's <see
        /// cref="Type"/> is <see cref="MarkupValueType.Integer"/>.
        /// </remarks>
        public int IntegerValue { get; internal set; }

        /// <summary>Gets the float value of this property.</summary>
        /// /// <remarks>
        /// This property is only defined when the property's <see
        /// cref="Type"/> is <see cref="MarkupValueType.Float"/>.
        /// </remarks>
        public float FloatValue { get; internal set; }

        /// <summary>Gets the string value of this property.</summary>
        /// <remarks>
        /// This property is only defined when the property's <see
        /// cref="Type"/> is <see cref="MarkupValueType.String"/>.
        /// </remarks>
        public string StringValue { get; internal set; }

        // Disable style warning "Summary should begin "Gets a value
        // indicating..." for this property, because that's not what this
        // bool property represents
#pragma warning disable SA1623
        /// <summary>Gets the bool value of this property.</summary>
        /// <remarks>
        /// This property is only defined when the property's <see
        /// cref="Type"/> is <see cref="MarkupValueType.Bool"/>.
        /// </remarks>
        public bool BoolValue { get; internal set; }
#pragma warning restore SA1623

        /// <summary>
        /// Gets the value's type.
        /// </summary>
        public MarkupValueType Type { get; internal set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            switch (this.Type)
            {
                case MarkupValueType.Integer:
                    return this.IntegerValue.ToString();
                case MarkupValueType.Float:
                    return this.FloatValue.ToString();
                case MarkupValueType.String:
                    return this.StringValue;
                case MarkupValueType.Bool:
                    return this.BoolValue.ToString();
                default:
                    throw new System.InvalidOperationException($"Invalid markup value type {this.Type}");
            }
        }
    }

    /// <summary>
    /// Represents a marker (e.g. `[a]`) in line of marked up text.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by the <see cref="LineParser.ParseMarkup"/> method.
    /// </remarks>
    public struct MarkupAttributeMarker
    {
        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="MarkupAttributeMarker"/> struct.
        /// </summary>
        /// <param name="name">The name of the marker.</param>
        /// <param name="position">The position of the marker.</param>
        /// <param name="properties">The properties of the marker.</param>
        /// <param name="type">The type of the marker.</param>
        internal MarkupAttributeMarker(string name, int position, List<MarkupProperty> properties, TagType type)
        {
            this.Name = name;
            this.Position = position;
            this.Properties = properties;
            this.Type = type;
        }

        /// <summary>
        /// Gets the name of the marker. For example, the marker `[wave]` has
        /// the name `wave`.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the position of the marker in the plain text.
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// Gets the list of properties associated with this marker.
        /// </summary>
        public List<MarkupProperty> Properties { get; private set; }

        /// <summary>
        /// Gets the type of marker that this is.
        /// </summary>
        public TagType Type { get; private set; }

        /// <summary>
        /// Gets the property associated with the specified key, if
        /// present.
        /// </summary>
        /// <param name="name">The name of the property to get.</param>
        /// <param name="result">When this method returns, contains the
        /// value associated with the specified key, if the key is found;
        /// otherwise, the default <see cref="MarkupValue"/>. This
        /// parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if the <see
        /// cref="MarkupAttributeMarker"/> contains an element with the
        /// specified key; otherwise, <see langword="false"/>.</returns>
        public bool TryGetProperty(string name, out MarkupValue result)
        {
            foreach (var prop in this.Properties)
            {
                if (prop.Name.Equals(name))
                {
                    result = prop.Value;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
