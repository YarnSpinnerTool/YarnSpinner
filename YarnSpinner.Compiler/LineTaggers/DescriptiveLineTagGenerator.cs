using System.Collections.Generic;
using Yarn.Markup;
using System.Text.RegularExpressions;

namespace Yarn.Compiler
{
    public class DescriptiveLineTagGenerator : ILineTagGenerator
    {
        const int IndexMultiplier = 100;
        const int RoundFactor = 5;

        private LineParser lineParser = new();
        private Dictionary<string, List<ILineTagGenerator.LineTagContext>>? lineContexts;
        private readonly Regex isMatchingNumbers = new("^[0-9][0-9][0-9][0-9]$");
        private readonly Regex isGeneration = new ("^g[0-9]+$");

        public void PrepareForLines(Dictionary<string, List<ILineTagGenerator.LineTagContext>> LineContexts)
        {
            lineContexts = LineContexts;
        }

        public string GenerateLineTag(string node, int lineIndex)
        {
            var context = ILineTagGenerator.LineTagContext.GetIndividualContext(lineContexts, node, lineIndex);

            var parsedMarkup = lineParser.ParseString(context.LineText, System.Globalization.CultureInfo.InvariantCulture.TwoLetterISOLanguageName);

            List<string> lineIDComponents = new()
            {
                node
            };

            // alright once I have my neighbours I can start doing the insertion
            // where what I have to do is work out my bounds based on that
            var neighbours = GetNeighbourInfo(lineContexts[node],  lineIndex);
            
            int increment;
            int finalIndex;
            if (neighbours.leftCount == -1 && neighbours.rightCount == -1)
            {
                // this is the ideal case, we have no tagged lines anywhere
                // we can just use our line index to tag ourselves
                finalIndex = (lineIndex + 1) * IndexMultiplier;
                increment = IndexMultiplier;
            }
            else
            {
                // ok we have some tagged neighbours
                // there are three possibilites:
                // being inserted between two tagged line
                // being inserted before any tagged lines
                // being inserted after any tagged lines

                if (neighbours.leftCount != -1 && neighbours.rightCount != -1)
                {
                    // we have a leftmost neighbour and a rightmost neighbour
                    // this means we are part of an insertion
                    // so we know we need to be between neighbours.leftCount and neighbours.rightCount
                    // but there might be other untagged lines also inside that block

                    // this is the total space we have to play with
                    var diff = neighbours.rightCount - neighbours.leftCount;

                    // but we need to also know how many new lines we are adding in
                    var insertions = neighbours.rightIndex - 1 - neighbours.leftIndex;
                    // and from that we can work out how much is should be incremented by
                    increment = diff / (insertions + 1);

                    // final step is to work out which of the insertions we are
                    // so we can use that to give us our new value
                    var relativeIndex = lineIndex - neighbours.leftIndex;
                    finalIndex = neighbours.leftCount + relativeIndex * increment;
                }
                else if (neighbours.leftIndex == -1)
                {
                    // we have a rightmost neighbour but no leftmost one
                    // this means we are being inserted before any tagged lines
                    // we just need to work out our new increment
                    // and can then advance ourselves along by that

                    increment = neighbours.rightCount / (neighbours.rightIndex + 1);
                    finalIndex = increment * (1 + lineIndex);
                }
                else
                {
                    // this means we have a leftmost neighbour but no rightmost neighbour
                    // aka we are being inserted after any tagged lines
                    // so we need to work out our new 0 which is the next index multiplier after our leftmost neighbour
                    // and our relative index which is just where we are in all the untagged lines
                    var nextMultiple = neighbours.leftCount + IndexMultiplier - 1 - (neighbours.leftCount + IndexMultiplier - 1) % IndexMultiplier;
                    
                    increment = IndexMultiplier;
                    finalIndex = (lineIndex - neighbours.leftIndex) * IndexMultiplier + nextMultiple;
                }
            }

            // rounding to the nearest five
            // if the gap is too small to allow for this we don't round
            // this is just to make the numbers look neater
            // this has a quirk though where if you have say the following
            //      existing #line:0
            //      new untagged line
            //      existing #line:12
            // the "new untagged line" will get given "line:10" which feels wrong but like it's uncommon enough to not worry about imo
            if (increment > RoundFactor)
            {
                finalIndex = finalIndex + RoundFactor - 1 - (finalIndex + RoundFactor - 1) % RoundFactor;
            }

            lineIDComponents.Add(string.Format("{0:D4}", finalIndex));

            if (parsedMarkup.TryGetAttributeWithName("character", out var characterAttribute)
                && characterAttribute.TryGetProperty("name", out MarkupValue characterNameMarkup)
                && characterNameMarkup.Type == MarkupValueType.String
                )
            {
                lineIDComponents.Add(characterNameMarkup.StringValue);
            }

            lineIDComponents.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:x7}", globalCount));
            globalCount++;

            var id = "line:" + string.Join("_", lineIDComponents);

            return id;
        }

        // this is temporary so I can keep generating keys without causing collision
        // will go away later
        private int globalCount = 0;

        private (int leftCount, int leftIndex, int leftGeneration, int rightCount, int rightIndex, int rightGeneration) GetNeighbourInfo(List<ILineTagGenerator.LineTagContext> lines, int lineIndex)
        {
            // ok I need to walk both left and right now until I find a neighbour who is tagged

            // walking forwards down the line
            var rightNeighbour = (-1, -1, -1);
            for (int i = lineIndex + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line.LineID))
                {
                    continue;
                }

                var pieces = line.LineID.Split('_');
                foreach (var piece in pieces)
                {
                    if (isMatchingNumbers.IsMatch(piece))
                    {
                        if (int.TryParse(piece, out int value))
                        {
                            rightNeighbour = (value, i, rightNeighbour.Item3);
                            i = lines.Count;
                        }
                    }
                    if (isGeneration.IsMatch(piece)) // currently unused
                    {
                        // this means its g[0-9]+
                        // so I strip off the g
                        // and parse it as a generation
                        if (int.TryParse(piece, out int value))
                        {
                            rightNeighbour = (rightNeighbour.Item1, rightNeighbour.Item2, value);
                            i = lines.Count;
                        }
                    }
                }
            }

            // now we walk backwards down the line
            var leftNeighbour = (-1, -1, -1);
            for (int i = lineIndex -1; i > -1; i--)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line.LineID))
                {
                    continue;
                }

                var pieces = line.LineID.Split('_');
                foreach (var piece in pieces)
                {
                    if (isMatchingNumbers.IsMatch(piece))
                    {
                        if (int.TryParse(piece, out int value))
                        {
                            leftNeighbour = (value, i, leftNeighbour.Item3);
                            i = -1;
                        }
                    }
                    if (isGeneration.IsMatch(piece)) // currently unused
                    {
                        // this means its g[0-9]+
                        // so I strip off the g
                        // and parse it as a generation
                        if (int.TryParse(piece, out int value))
                        {
                            rightNeighbour = (leftNeighbour.Item1, leftNeighbour.Item2, value);
                            i = -1;
                        }
                    }
                }
            }

            return (leftNeighbour.Item1, leftNeighbour.Item2, leftNeighbour.Item3, rightNeighbour.Item1, rightNeighbour.Item2, rightNeighbour.Item3);
        }
    }

}
