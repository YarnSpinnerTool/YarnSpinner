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
    /// <summary>Provides a mechanism for producing replacement text for a
    /// marker.</summary>
    /// <seealso cref="LineParser.RegisterMarkerProcessor"/>
    /// <seealso cref="LineParser.DeregisterMarkerProcessor(string)"/>
    public interface IAttributeMarkerProcessor
    {
        /// <summary>
        /// Produces replacement text for a marker.
        /// </summary>
        /// <param name="marker">The marker to process into replacement
        /// text.</param>
        /// <param name="childBuilder">A <see cref="System.Text.StringBuilder"/>
        /// that contains the child text contained within <paramref
        /// name="marker"/>. Use the methods on this stringbuilder to produce
        /// any text needed from this marker.</param>
        /// <param name="childAttributes">The child attributes of <paramref
        /// name="marker"/>.</param>
        /// <param name="localeCode">A BCP-47 locale code that represents the
        /// locale in which any processing should take place.</param>
        /// <example>
        /// <para>If the original text being processed by <see
        /// cref="LineParser.ParseString(string, string, bool)"/> is "<c>[a]
        /// text1 [b/] text2 [/a]</c>", then the following facts will be
        /// true:</para>
        /// <list type="bullet">
        /// <item>
        /// <paramref name="childBuilder"/> will contain the text "text1 text2".
        /// </item>
        /// <item>
        /// <paramref name="childAttributes"/> will contain a MarkupAttribute
        /// named "<c>b</c>".
        /// </item>
        /// </list>
        /// </example>
        /// <returns>The collection of diagnostics produced during processing,
        /// and the number of invisible characters created during processing.</returns>
        public ReplacementMarkerResult ProcessReplacementMarker(MarkupAttribute marker, System.Text.StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode);
    }

    /// <summary>
    /// A bundle of results from processing replacement markers.
    /// </summary>
    /// <seealso cref="IAttributeMarkerProcessor"/>
    public struct ReplacementMarkerResult
    {
        /// <summary>
        /// The collection of diagnostics produced during processing, if any.
        /// </summary>
        public List<LineParser.MarkupDiagnostic> Diagnostics;
        /// <summary>
        /// The number of invisible characters added into the line during processing.
        /// </summary>
        /// <remarks>
        /// This will vary depending on what the replacement markup needs to do.
        /// <list type="bullet">
        /// <item>
        /// <para>
        /// When only inserting rich-text tags, this should be the length of all inserted text.
        /// For example <c>"this is text with [bold]some bold[/bold] elements"</c> translated into Unity style rich-text become <c>"this is text with &lt;b>some bold&lt;/b> elements"</c>.
        /// In this case then the value of <c>InvisibleCharacters</c> would be seven.
        /// </para>
        /// </item>
        /// <item>
        /// <para>
        /// When only modifying the content of the children text, such as making all text upper case, this should be <c>0</c>.
        /// For example <c>"this is text with [upper]some uppercased[/upper] elements"</c> is transformed into <c>"this is text with SOME UPPERCASED elements"</c>.
        /// The number of invisible character will be zero.
        /// </para>
        /// </item>
        /// <item>
        /// When adding new content into the line (regardless of being added at the start, end, or middle) this should be zero but the replacement processor should make sure to shift along it's children attributes where appropriate.
        /// For example <c>"this is text with [emph]some emphasised[/emph] elements"</c> transformed into <c>"this is text with !!some emphasised!! elements"</c> the value of <c>InvisibleCharacters</c> would be zero.
        /// In this case however the <c>childAttributes</c> in <see cref="IAttributeMarkerProcessor.ProcessReplacementMarker"/> would need to be shifted down two.
        /// </item>
        /// </list>
        /// </remarks>
        public int InvisibleCharacters;

        /// <summary>
        /// Convenience constructor for replacement markup results.
        /// </summary>
        /// <param name="diagnostics">The diagnostics generated during processing</param>
        /// <param name="invisibleCharacters">the number of invisible characters generated during processing.</param>
        public ReplacementMarkerResult(List<LineParser.MarkupDiagnostic> diagnostics, int invisibleCharacters)
        {
            this.Diagnostics = diagnostics;
            this.InvisibleCharacters = invisibleCharacters;
        }
    }
}
