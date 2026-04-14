using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Yarn.Compiler
{
    /// <summary>
    /// A line tag generator that produces line IDs containing a random hexadecimal
    /// string.
    /// </summary>
    public class RandomLineTagGenerator : ILineTagGenerator
    {
        private static readonly Random Random = new();
        static readonly TimeSpan MaxSearchTime = TimeSpan.FromMilliseconds(500);

        private HashSet<string>? allKeys;
        
        /// <inheritdoc/>
        public void PrepareForLines(Dictionary<string, List<ILineTagGenerator.LineTagContext>> LineContexts)
        {
            allKeys = new();
            allKeys.UnionWith(LineContexts.SelectMany(a => a.Value).Select(b => b.LineID).OfType<string>());
        }

        /// <inheritdoc/>
        public string GenerateLineTag(string node, int lineIndex)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (allKeys == null)
            {
                throw new ArgumentException("Asked to generate a line tag but haven't been given the context");
            }

            string tag;
            do
            {
                if (stopwatch.Elapsed >= MaxSearchTime)
                {
                    var inner = new TimeoutException($"Failed to find a unique line ID within {MaxSearchTime.TotalMilliseconds}ms");
                    throw new ILineTagGenerator.LineTaggingException("Unable to tag the line due to running out of time.", inner);
                }

                tag = string.Format(CultureInfo.InvariantCulture, "line:{0:x7}", Random.Next(0x1000000));
            }
            while (allKeys.Contains(tag));
            stopwatch.Stop();

            allKeys.Add(tag);

            return tag;
        }
    }

}
