using System.Collections.Generic;
using Yarn.Markup;
using System.Text.RegularExpressions;

namespace Yarn.Compiler
{
    /// <summary>
    /// Creates line ids in a form that attempts to approximate how a person would manually tag lines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If you had a yarn node like the following:
    /// <code>
    /// title: Node
    /// subtitle: Subtitle
    /// when: always
    /// ---
    /// Alice: This is me saying a line
    /// Alice: And another line
    /// &lt;&lt;some command&gt;&gt;
    /// Bob: And me responding
    /// And finally a line that isn't from a character
    /// ===
    /// </code>
    /// This would make the following tags
    /// <list type="number">
    /// <item><c>#line:Node.Subtitle_0100_Alice</c></item>
    /// <item><c>#line:Node.Subtitle_0200_Alice</c></item>
    /// <item><c>#line:Node.Subtitle_0300_Bob</c></item>
    /// <item><c>#line:Node.Subtitle_0400</c></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// Where possible the tagger will attempt to add numbers equally in-between existing tags.
    /// Meaning if you had this yarn:
    /// <code>
    /// title: Node
    /// subtitle: Subtitle
    /// when: always
    /// ---
    /// Alice: This is me saying a line #line:Node.Subtitle_0100_Alice
    /// Bob: We added a retort here
    /// Alice: And another line #line:Node.Subtitle_0200_Alice
    /// &lt;&lt;some command&gt;&gt;
    /// Bob: And me responding #line:Node.Subtitle_0300_Bob
    /// And finally a line that isn't from a character #line:Node.Subtitle_0400
    /// ===
    /// </code>
    /// The line <c>"Bob: We added a retort here"</c> would get given the <c>#line:Node.Subtitle_0150_Bob</c> tag.
    /// </para>
    /// 
    /// <para>
    /// If it isn't possible to fit the number of tags in-between existing tagged lines the tagger will start adding generational information to repeated tags.
    /// In the following Yarn we have two tags that have no space between them.
    /// <code>
    /// title: Node
    /// subtitle: Subtitle
    /// when: always
    /// ---
    /// Alice: This is me saying a line #line:Node.Subtitle_0100_Alice
    /// Bob: I have a retort #line:Node.Subtitle_0101_Bob
    /// ===
    /// </code>
    /// This means any lines added in-between <c>#line:Node.Subtitle_0100_Alice</c> and <c>#line:Node.Subtitle_0101_Bob</c> can't be given a unique number
    /// So adding a line in-between requires adding a generation:
    /// /// <code>
    /// title: Node
    /// subtitle: Subtitle
    /// when: always
    /// ---
    /// Alice: This is me saying a line #line:Node.Subtitle_0100_Alice
    /// Alice: And saying a bit more
    /// Bob: I have a retort #line:Node.Subtitle_0101_Bob
    /// ===
    /// </code>
    /// Running the tagger over this would result in the <c>"Alice: And saying a bit more"</c> line being given the <c>#line:Node.Subtitle_0101_g1_Alice</c> line id.
    /// The <c>_g1</c> represents the generation of the <c>0101</c> indexed line in the node.
    /// Each indexed line can have as many generations as necessary.
    /// This means if we were to add another line in-between <c>0100</c> and <c>0101</c> it's generation number will be <c>2</c>, but it could be above or below the line with the first generation.
    /// </para>
    /// </remarks>
    public class DescriptiveLineTagGenerator : ILineTagGenerator
    {
        const int IndexMultiplier = 100;
        const int RoundFactor = 5;

        private LineParser lineParser = new();
        private Dictionary<string, List<ILineTagGenerator.LineTagContext>>? lineContexts;
        private Dictionary<string, Dictionary<int, int>> generations = new();
        private readonly Regex isMatchingNumbers = new("^[0-9]+$");
        private readonly Regex isGeneration = new ("^g[0-9]+$");

        // this isn't working, not sure why
        // private readonly Regex isMatchingLineID = new ("#line:[^_]+_(?<number>[0-9]+)(?<generation>_g[0-9]+)?_?.*");

        private Dictionary<string, int[]>? numbers;
        private HashSet<string> exclusions = new();

        /// <inheritdocs/>
        public void PrepareForLines(Dictionary<string, List<ILineTagGenerator.LineTagContext>> LineContexts, HashSet<string> excludedIDs)
        {
            lineContexts = LineContexts;
            numbers = new();
            exclusions.UnionWith(excludedIDs);

            foreach (var pair in lineContexts)
            {
                var lines = pair.Value;
                var elements = new int[lines.Count];

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];

                    int num = -1;
                    int generation = 0;

                    if (!string.IsNullOrWhiteSpace(line.LineID))
                    {
#pragma warning disable CS8602
                        var pieces = line.LineID.Split('_');
#pragma warning restore CS8602
                        foreach (var piece in pieces)
                        {
                            if (isMatchingNumbers.IsMatch(piece))
                            {
                                if (int.TryParse(piece, out int numberValue))
                                {
                                    num = numberValue;
                                }
                            }
                            else if (isGeneration.IsMatch(piece))
                            {
                                if (int.TryParse(piece.Remove(0,1), out int generationValue))
                                {
                                    generation = generationValue;
                                }
                            }
                        }

                        // this doesn't work, look into that later
                        // var match = isMatchingLineID.Match(line.LineID.Trim());
                        // if (match.Success)
                        // {
                        //     if (int.TryParse(match.Groups["number"].Value ?? string.Empty, out int numberValue))
                        //     {
                        //         num = numberValue;
                        //     }
                        //     if (int.TryParse(match.Groups["generation"].Value.Remove(0,2) ?? string.Empty, out int generationValue))
                        //     {
                        //         generation = generationValue;
                        //     }
                        // }
                    }

                    // ok now we either have our generation and count for the line or we don't
                    // either way we need to now put it into it's relevant pieces

                    // first up the generation, if we found a number we need to record it's generation
                    if (num != -1)
                    {
                        if (generations.TryGetValue(pair.Key, out var genCollection))
                        {
                            if (genCollection.TryGetValue(num, out var current))
                            {
                                if (generation > current)
                                {
                                    genCollection[num] = generation;
                                }
                            }
                            else
                            {
                                genCollection[num] = generation;
                            }
                        }
                        else
                        {
                            var newGeneration = new Dictionary<int, int>();
                            newGeneration[num] = generation;
                            generations[pair.Key] = newGeneration;
                        }
                    }
                    // then the relative position of the line and its number
                    elements[i] = num;
                }
                numbers[pair.Key] = elements;
            }
        }

        /// <inheritdoc/>
        public string GenerateLineTag(string node, int lineIndex)
        {
            if (lineContexts == null)
            {
                throw new ILineTagGenerator.LineTaggingException($"Asked to generate a line at index {lineIndex} for {node} node but we haven't been given a context");
            }
            if (!lineContexts.TryGetValue(node, out var linesForNode))
            {
                throw new ILineTagGenerator.LineTaggingException($"Asked to generate a line at index {lineIndex} for {node} node but we have no node with this name");
            }
            if (linesForNode.Count == 0)
            {
                throw new ILineTagGenerator.LineTaggingException($"Asked to generate a line at index {lineIndex} for {node} node but this list is empty");
            }
            if (lineIndex < 0)
            {
                throw new ILineTagGenerator.LineTaggingException($"Asked to generate a line at index {lineIndex} for {node} node but the index is out of bounds");
            }
            if (lineIndex >= linesForNode.Count)
            {
                throw new ILineTagGenerator.LineTaggingException($"Asked to generate a line at index {lineIndex} for {node} node but the index is out of bounds");
            }
            var context = linesForNode[lineIndex];

            if (numbers == null)
            {
                throw new System.ArgumentException("Asked to generate a line tag but haven't been given the context");
            }
            if (!numbers.TryGetValue(node, out var ranges))
            {
                throw new System.ArgumentException("Asked to generate a line tag but haven't been given the context");
            }

            var parsedMarkup = lineParser.ParseString(context.LineText, System.Globalization.CultureInfo.InvariantCulture.TwoLetterISOLanguageName);

            List<string> lineIDComponents = new()
            {
                node
            };

            // alright once I have my neighbours I can start doing the insertion
            // where what I have to do is work out my bounds based on that
            var neighbours = GetNeighbours(lineIndex, ranges);
            
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

                // we treat all three differently because while placing values evenly across a range is easy we want slightly different behaviours around the ends being inclusive and exclusive depending on the range
                // so while I could move all of this into one spot it would still have to have these special cases to work out the bounds anyways

                if (neighbours.leftCount != -1 && neighbours.rightCount != -1)
                {
                    // we have a leftmost neighbour and a rightmost neighbour
                    // this means we are part of an insertion
                    // so we know we need to be between neighbours.leftCount and neighbours.rightCount
                    // but there might be other untagged lines also inside that block

                    // there is however one situation we need to handle
                    // if the ordering has been invalidated in some way
                    // this might be because of a manual line id being misidentified as one of our tags
                    // or someone reordered or modified the lines after being tagged
                    // but we can't linearly space things incrementally in a descending range
                    if (neighbours.leftCount > neighbours.rightCount)
                    {
                        throw new ILineTagGenerator.LineTaggingException($"The preceeding dialogue has a greater tagged value ({neighbours.leftCount}) than the following ({neighbours.rightCount}).", context.SourceFileName, context.LineNumber);
                    }

                    // this is the total space we have to play with
                    var diff = neighbours.rightCount - neighbours.leftCount;

                    // but we need to also know how many new lines we are adding in
                    var insertions = neighbours.rightIndex - 1 - neighbours.leftIndex;
                    // and from that we can work out how much is should be incremented by
                    increment = diff / (insertions + 1);

                    // final step is to work out which of the insertions we are
                    // so we can use that to give us our new value
                    var relativeIndex = lineIndex - neighbours.leftIndex;

                    // if we have enough space that is fine, just keep moving along
                    // but it is possible we don't, in which case we change the bounds exclusivity, force it to be a dense packing and go again
                    // this will basically force generational numbers but there isn't much you can do to prevent that
                    // but this gives us the best chance to avoid that
                    if (increment > 0)
                    {    
                        finalIndex = neighbours.leftCount + relativeIndex * increment;
                    }
                    else
                    {
                        increment = 1;
                        finalIndex = System.Math.Min(neighbours.leftCount + relativeIndex, neighbours.rightCount);
                    }
                }
                else if (neighbours.leftIndex == -1)
                {
                    // we have a rightmost neighbour but no leftmost one
                    // this means we are being inserted before any tagged lines
                    // we just need to work out our new increment
                    // and can then advance ourselves along by that

                    increment = neighbours.rightCount / (neighbours.rightIndex + 1);
                    if (increment > 0)
                    {    
                        finalIndex = increment * (1 + lineIndex);
                    }
                    else
                    {
                        // we didnt have enough space so we need to go to dense packing
                        increment = 1;
                        finalIndex = System.Math.Min(increment * lineIndex, neighbours.rightCount);
                    }
                }
                else
                {
                    // this means we have a leftmost neighbour but no rightmost neighbour
                    // aka we are being inserted after any tagged lines
                    // so we need to work out our new 0 which is the next index multiplier after our leftmost neighbour
                    // and our relative index which is just where we are in all the untagged lines
                    var nextMultiple = neighbours.leftCount + IndexMultiplier - 1 - (neighbours.leftCount + IndexMultiplier - 1) % IndexMultiplier;
                    
                    // then we can just continue on as if we were numbering normally, we have no practical upper bound
                    increment = IndexMultiplier;
                    finalIndex = (lineIndex - neighbours.leftIndex) * IndexMultiplier + nextMultiple;
                }
            }

            // rounding up to the nearest five
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

            var generation = GetGeneration(node, finalIndex);
            if (generation != 0)
            {
                lineIDComponents.Add($"g{generation}");
            }

            if (parsedMarkup.TryGetAttributeWithName("character", out var characterAttribute)
                && characterAttribute.TryGetProperty("name", out MarkupValue characterNameMarkup)
                && characterNameMarkup.Type == MarkupValueType.String
                )
            {
                lineIDComponents.Add(characterNameMarkup.StringValue);
            }

            var id = "line:" + string.Join("_", lineIDComponents);

            if (exclusions.Contains(id))
            {
                throw new ILineTagGenerator.LineTaggingException($"The generated id '{id}' conflicts with an id we were excluded from using.", context.SourceFileName, context.LineNumber);
            }

            return id;
        }

        private (int leftCount, int leftIndex, int rightCount, int rightIndex) GetNeighbours(int lineIndex, int[] numbers)
        {
            // need to walk left and right until I find a tagged neighbour
            int lc = -1;
            int li = -1;
            int rc = -1;
            int ri = -1;

            // walking fowards down the lines
            for (int i = lineIndex + 1; i < numbers.Length; i++)
            {
                if (numbers[i] == -1)
                {
                    continue;
                }
                ri = i;
                rc = numbers[i];
                break;
            }
            // walking backwards up the lines
            for (int i = lineIndex -1; i > -1; i--)
            {
                if (numbers[i] == -1)
                {
                    continue;
                }
                li = i;
                lc = numbers[i];
                break;
            }
            return (lc, li, rc, ri);
        }
        private int GetGeneration(string node, int number)
        {
            if (generations.TryGetValue(node, out var numbers))
            {
                if (numbers.TryGetValue(number, out var generation))
                {
                    generations[node][number] = generation + 1;
                    return generation + 1;
                }
                
                // we have generation data on this node, but not for this number
                numbers[number] = 0;
            }
            else
            {
                // we have no generational data at all for this node, need to create it and then add it
                // this shouldnt occur outside of fully untagged lines where it generations won't happen
                // but it doesn't hurt to be sure
                var nodeData = new Dictionary<int, int>
                {
                    [number] = 0
                };
                generations[node] = nodeData;
            }

            return 0;
        }
    }
}
