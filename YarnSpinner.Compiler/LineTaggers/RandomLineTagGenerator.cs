using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

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

        /// <inheritdoc/>
        public string GenerateLineTag(ILineTagGenerator.LineTagContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string tag;
            do
            {
                if (stopwatch.Elapsed >= MaxSearchTime)
                {
                    throw new TimeoutException($"Failed to find a unique line ID within {MaxSearchTime.TotalMilliseconds}ms");
                }

                tag = string.Format(CultureInfo.InvariantCulture, "line:{0:x7}", Random.Next(0x1000000));
            }
            while (context.ExistingLineIDs != null && context.ExistingLineIDs.Contains(tag));
            stopwatch.Stop();

            return tag;
        }
    }

}
