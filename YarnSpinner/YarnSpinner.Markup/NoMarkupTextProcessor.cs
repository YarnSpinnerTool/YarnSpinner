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
    /// <summary>A markup text processor that implements the <c>[nomarkup]</c>
    /// attribute's behaviour.</summary>
    internal class NoMarkupTextProcessor : IAttributeMarkerProcessor
    {
        /// <inheritdoc/>
        public string ReplacementTextForMarker(MarkupAttributeMarker marker)
        {
            if (marker.TryGetProperty(LineParser.ReplacementMarkerContents, out var prop))
            {
                return prop.StringValue;
            }
            else
            {
                // this is only possible when this marker is self-closing (i.e.
                // it's '[nomarkup/]'), in which case there's no text to
                // provide, so we'll provide the empty string here
                return string.Empty;
            }
        }
    }
}
