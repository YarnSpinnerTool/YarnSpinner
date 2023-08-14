// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    public static class TextCoordinateConverter
    {
        /// <summary>
        /// Gets the indices at which lines start in <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text to get line starts for.</param>
        /// <returns>A collection of indices indicating where a new line
        /// starts.</returns>
        public static ImmutableArray<int> GetLineStarts(string text)
        {
            var lineStarts = new List<int> { 0 };

            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];

                if (character == '\r')
                {
                    if (i < text.Length - 1 && text[i + 1] == '\n')
                    {
                        continue;
                    }

                    lineStarts.Add(i + 1);
                }

                if (text[i] == '\n')
                {
                    lineStarts.Add(i + 1);
                }
            }

            return lineStarts.ToImmutableArray();
        }

        public static (int line, int character) GetPosition(IReadOnlyList<int> lineStarts, int offset)
        {
            if (lineStarts.Count == 0)
            {
                throw new ArgumentException($"{nameof(lineStarts)} must not be empty.");
            }

            if (lineStarts[0] != 0)
            {
                throw new ArgumentException($"The first element of {nameof(lineStarts)} must be 0, but got {lineStarts[0]}.");
            }

            if (offset < 0)
            {
                throw new ArgumentException($"{nameof(offset)} must not be a negative number.");
            }

            int line = BinarySearch(lineStarts, offset);

            if (line < 0)
            {
                // If the actual line start was not found,
                // the binary search returns the 2's-complement of the next line start, so substracting 1.
                line = ~line - 1;
            }

            return (line, offset - lineStarts[line]);
        }

        public static int GetOffset(IReadOnlyList<int> lineStarts, int line, int character)
        {
            if (line < 0 || line >= lineStarts.Count)
            {
                throw new ArgumentException("The specified line number is not valid.");
            }

            return lineStarts[line] + character;
        }

        private static int BinarySearch(IReadOnlyList<int> values, int target)
        {
            int start = 0;
            int end = values.Count - 1;

            while (start <= end)
            {
                int mid = start + ((end - start) / 2);

                if (values[mid] == target)
                {
                    return mid;
                }
                else if (values[mid] < target)
                {
                    start = mid + 1;
                }
                else
                {
                    end = mid - 1;
                }
            }

            return ~start;
        }

        /// <summary>
        /// Gets a <see cref="Range"/> for a given <see
        /// cref="Microsoft.CodeAnalysis.Text.TextSpan"/>.
        /// </summary>
        /// <param name="span">The text span to get a range for.</param>
        /// <param name="lineStarts">The line start information to use.</param>
        /// <returns>The <see cref="Range"/>.</returns>
        public static Range GetRange(Microsoft.CodeAnalysis.Text.TextSpan span, IReadOnlyList<int> lineStarts) {
            var start = GetPosition(lineStarts, span.Start);
            var end = GetPosition(lineStarts, span.End);
            return new Range(start, end);
        }
    }
}
