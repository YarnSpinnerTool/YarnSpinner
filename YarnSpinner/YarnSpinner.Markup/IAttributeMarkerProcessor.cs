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
    /// <summary>Provides a mechanism for producing replacement text for a
    /// marker.</summary>
    /// <seealso cref="LineParser.RegisterMarkerProcessor"/>
    internal interface IAttributeMarkerProcessor
    {
        /// <summary>
        /// Produces the replacement text that should be inserted into a parse
        /// result for a given attribute.
        /// </summary>
        /// <remarks>
        /// If the marker is an <i>open</i> marker, the text from the marker's
        /// position to its corresponding closing marker is provided as a string
        /// property called <c>contents</c>.
        /// </remarks>
        /// <param name="marker">The marker that should have text
        /// inserted.</param>
        /// <returns>The replacement text to insert.</returns>
        string ReplacementTextForMarker(MarkupAttributeMarker marker);
    }
}
