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
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

#pragma warning disable CA1815
    /// <summary>
    /// The result of parsing a line of marked-up text.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by objects that can parse markup, such as <see cref="Dialogue"/>.
    /// </remarks>
    /// <seealso cref="Dialogue.ParseMarkup(string)"/>
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

        /// <summary>
        /// Returns the substring of <see cref="Text"/> covered by
        /// <paramref name="attribute"/> Position and Length properties.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the attribute's <see cref="MarkupAttribute.Length"/>
        /// property is zero, this method returns the empty string.
        /// </para>
        /// <para>
        /// This method does not check to see if <paramref
        /// name="attribute"/> is an attribute belonging to this
        /// MarkupParseResult. As a result, if you pass an attribute that
        /// doesn't belong, it may describe a range of text that does not
        /// appear in <see cref="Text"/>. If this occurs, an <see
        /// cref="System.IndexOutOfRangeException"/> will be thrown.
        /// </para>
        /// </remarks>
        /// <param name="attribute">The attribute to get the text
        /// for.</param>
        /// <returns>The text contained within the attribute.</returns>
        /// <throws cref="System.IndexOutOfRangeException">Thrown when
        /// attribute's <see cref="MarkupAttribute.Position"/> and <see
        /// cref="MarkupAttribute.Length"/> properties describe a range of
        /// text outside the maximum range of <see cref="Text"/>.</throws>
        public string TextForAttribute(MarkupAttribute attribute)
        {
            if (attribute.Length == 0)
            {
                return string.Empty;
            }

            if (this.Text.Length < attribute.Position + attribute.Length)
            {
                throw new System.ArgumentOutOfRangeException($"Attribute represents a range not representable by this text. Does this {nameof(MarkupAttribute)} belong to this {nameof(MarkupParseResult)}?");
            }

            return this.Text.Substring(attribute.Position, attribute.Length);
        }

        /// <summary>
        /// Deletes an attribute from this markup.
        /// </summary>
        /// <remarks>
        /// This method deletes the range of text covered by <paramref
        /// name="attributeToDelete"/>, and updates the other attributes in this
        /// markup as follows:
        ///
        /// <list type="bullet">
        /// <item>
        /// Attributes that start and end before the deleted attribute are
        /// unmodified.
        /// </item>
        ///
        /// <item>
        /// Attributes that start before the deleted attribute and end inside it
        /// are truncated to remove the part overlapping the deleted attribute.
        /// </item>
        ///
        /// <item>
        /// Attributes that have the same position and length as the deleted
        /// attribute are deleted, if they apply to any text.
        /// </item>
        ///
        /// <item>
        /// Attributes that start and end within the deleted attribute are
        /// deleted.
        /// </item>
        ///
        /// <item>
        /// Attributes that start within the deleted attribute, and end outside
        /// it, have their start truncated to remove the part overlapping the
        /// deleted attribute.
        /// </item>
        ///
        /// <item>
        /// Attributes that start after the deleted attribute have their start
        /// point adjusted to account for the deleted text.
        /// </item>
        /// </list>
        ///
        /// <para>
        /// This method does not modify the current object. A new <see
        /// cref="MarkupParseResult"/> is returned.
        /// </para>
        ///
        /// <para>
        /// If <paramref name="attributeToDelete"/> is not an attribute of this
        /// <see cref="MarkupParseResult"/>, the behaviour is undefined.
        /// </para>
        /// </remarks>
        /// <param name="attributeToDelete">The attribute to remove.</param>
        /// <returns>A new <see cref="MarkupParseResult"/> object, with the
        /// plain text modified and an updated collection of
        /// attributes.</returns>
        public MarkupParseResult DeleteRange(MarkupAttribute attributeToDelete)
        {
            var newAttributes = new List<MarkupAttribute>();

            // Address the trivial case: if the attribute has a zero
            // length, just create a new markup that doesn't include it.
            // The plain text is left unmodified, because this attribute
            // didn't apply to any text.
            if (attributeToDelete.Length == 0)
            {
                foreach (var a in this.Attributes)
                {
                    if (!a.Equals(attributeToDelete))
                    {
                        newAttributes.Add(a);
                    }
                }

                return new MarkupParseResult(this.Text, newAttributes);
            }

            var deletionStart = attributeToDelete.Position;
            var deletionEnd = attributeToDelete.Position + attributeToDelete.Length;

            var editedSubstring = this.Text.Remove(attributeToDelete.Position, attributeToDelete.Length);

            foreach (var existingAttribute in this.Attributes)
            {
                var start = existingAttribute.Position;
                var end = existingAttribute.Position + existingAttribute.Length;

                if (existingAttribute.Equals(attributeToDelete))
                {
                    // This is the attribute we're deleting. Don't include
                    // it.
                    continue;
                }

                var editedAttribute = existingAttribute;

                if (start <= deletionStart)
                {
                    // The attribute starts before start point of the item
                    // we're deleting.
                    if (end <= deletionStart)
                    {
                        // This attribute is entirely before the item we're
                        // deleting, and will be unmodified.
                    }
                    else if (end <= deletionEnd)
                    {
                        // This attribute starts before the item we're
                        // deleting, and ends inside it. The Position
                        // doesn't need to change, but its Length is
                        // trimmed so that it ends where the deleted
                        // attribute begins.
                        editedAttribute.Length = deletionStart - start;

                        if (existingAttribute.Length > 0 && editedAttribute.Length <= 0)
                        {
                            // The attribute's length has been reduced to
                            // zero. All of the contents it previous had
                            // have been removed, so we will remove the
                            // attribute itself.
                            continue;
                        }
                    }
                    else
                    {
                        // This attribute starts before the item we're
                        // deleting, and ends after it. Its length is
                        // edited to remove the length of the item we're
                        // deleting.
                        editedAttribute.Length -= attributeToDelete.Length;
                    }
                }
                else if (start >= deletionEnd)
                {
                    // The item begins after the item we're deleting. Its
                    // length isn't changing. We just need to offset its
                    // start position.
                    editedAttribute.Position = start - attributeToDelete.Length;
                }
                else if (start >= deletionStart && end <= deletionEnd)
                {
                    // The item is entirely within the item we're deleting.
                    // It will be deleted too - we'll skip including it in
                    // the updated attributes list.
                    continue;
                }
                else if (start >= deletionStart && end > deletionEnd)
                {
                    // The item starts within the item we're deleting, and
                    // ends outside it. We'll adjust the start point so
                    // that it begins at the point where this item and the
                    // item we're deleting stop overlapping.
                    var overlapLength = deletionEnd - start;
                    var newStart = deletionStart;
                    var newLength = existingAttribute.Length - overlapLength;

                    editedAttribute.Position = newStart;
                    editedAttribute.Length = newLength;
                }

                newAttributes.Add(editedAttribute);
            }

            return new MarkupParseResult(editedSubstring, newAttributes);
        }
    }
#pragma warning restore CA1815

#pragma warning disable CA1711
#pragma warning disable CA1815
    /// <summary>
    /// Represents a range of text in a marked-up string.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by objects that can parse markup, such as <see cref="Dialogue"/>.
    /// </remarks>
    /// <seealso cref="Dialogue.ParseMarkup(string)"/>
    public struct MarkupAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupAttribute"/>
        /// struct using specified values.
        /// </summary>
        /// <param name="position">The position in the plain text where
        /// this attribute begins.</param>
        /// <param name="sourcePosition">The position in the original
        /// source text where this attribute begins.</param>
        /// <param name="length">The number of text elements in the plain
        /// text that this attribute covers.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="properties">The properties associated with this
        /// attribute.</param>
        internal MarkupAttribute(int position, int sourcePosition, int length, string name, IEnumerable<MarkupProperty> properties)
        {
            this.Position = position;
            this.SourcePosition = sourcePosition;
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
        : this(openingMarker.Position, openingMarker.SourcePosition, length, openingMarker.Name, openingMarker.Properties)
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

        /// <summary>
        /// Gets the position in the original source text where this
        /// attribute begins.
        /// </summary>
        internal int SourcePosition { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[{this.Name}] - {this.Position}-{this.Position + this.Length} ({this.Length}");

            if (this.Properties?.Count > 0)
            {
                sb.Append($", {this.Properties.Count} properties)");
            }

            sb.Append(')');

            return sb.ToString();
        }
    }
#pragma warning restore CA1815
#pragma warning restore CA1711

    /// <summary>
    /// A property associated with a <see cref="MarkupAttribute"/>.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by objects that can parse markup, such as <see cref="Dialogue"/>.
    /// </remarks>
    /// <seealso cref="Dialogue.ParseMarkup(string)"/>
#pragma warning disable CA1815
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
#pragma warning restore CA1815

#pragma warning disable CA1815
    /// <summary>
    /// A value associated with a <see cref="MarkupProperty"/>.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by objects that can parse markup, such as <see cref="Dialogue"/>.
    /// </remarks>
    /// <seealso cref="Dialogue.ParseMarkup(string)"/>
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
#pragma warning restore CA1815

    /// <summary>
    /// Represents a marker (e.g. <c>[a]</c>) in line of marked up text.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. It is created
    /// by objects that can parse markup, such as <see cref="Dialogue"/>.
    /// </remarks>
    /// <seealso cref="Dialogue.ParseMarkup(string)"/>
    internal struct MarkupAttributeMarker
    {
        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="MarkupAttributeMarker"/> struct.
        /// </summary>
        /// <param name="name">The name of the marker.</param>
        /// <param name="position">The position of the marker.</param>
        /// <param name="sourcePosition">The position of the marker in the original text.</param>
        /// <param name="properties">The properties of the marker.</param>
        /// <param name="type">The type of the marker.</param>
        internal MarkupAttributeMarker(string name, int position, int sourcePosition, List<MarkupProperty> properties, TagType type)
        {
            this.Name = name;
            this.Position = position;
            this.SourcePosition = sourcePosition;
            this.Properties = properties;
            this.Type = type;
        }

        /// <summary>
        /// Gets the name of the marker.
        /// </summary>
        /// <remarks>
        /// For example, the marker <c>[wave]</c> has the name <c>wave</c>.
        /// </remarks>
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
        /// Gets or sets the position of this marker in the original source
        /// text.
        /// </summary>
        internal int SourcePosition { get; set; }

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
                if (prop.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
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
