// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;
using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer
{
    public static class PositionHelper
    {
        public static Position GetPosition(ImmutableArray<int> lineStarts, in int offset)
        {
            (int line, int character) = TextCoordinateConverter.GetPosition(lineStarts, offset);
            return new Position(line, character);
        }

        public static bool DoesPositionContainToken(Position position, IToken token)
        {
            return position.Line == token.Line - 1 && position.Character >= token.Column && position.Character <= token.Column + token.Text.Length;
        }

        // public static IToken getTokenFromList(Position position, List<IToken> list)
        // {
        //    list.BinarySearch()
        // }
        public static int GetOffset(ImmutableArray<int> lineStarts, Position position) => TextCoordinateConverter.GetOffset(lineStarts, position.Line, position.Character);

        public static Range GetRange(ImmutableArray<int> lineStarts, IToken token)
        {
            return GetRange(lineStarts, token, token);
        }

        public static Range GetRange(ImmutableArray<int> lineStarts, IToken start, IToken end)
        {
            if (start == null)
            {
                throw new System.Exception("Attempted to get range of null token");
            }

            if (end == null)
            {
                return GetRange(lineStarts, start, start);
            }

            return new Range
            {
                Start = GetPosition(lineStarts, start.StartIndex),
                End = GetPosition(lineStarts, end.StopIndex + 1),
            };
        }

        public static Range GetRange(ImmutableArray<int> lineStarts, int start, int end)
        {
            return new Range
            {
                Start = GetPosition(lineStarts, start),
                End = GetPosition(lineStarts, end + 1),
            };
        }

        public static Range GetRange(Yarn.Compiler.Range range) {
            return new Range(
                range.Start.Line,
                range.Start.Character,
                range.End.Line,
                range.End.Character
            );
        }
    }
}