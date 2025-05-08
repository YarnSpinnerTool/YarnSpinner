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
        /// if any.</returns>
        public List<LineParser.MarkupDiagnostic> ProcessReplacementMarker(MarkupAttribute marker, System.Text.StringBuilder childBuilder, List<MarkupAttribute> childAttributes, string localeCode);
    }
}
