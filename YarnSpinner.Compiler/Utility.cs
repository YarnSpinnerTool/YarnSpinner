using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Yarn.Compiler {

    /// <summary>
    /// Utility methods for working with line tags.
    /// </summary>
    public static class Utility
    {

        private static readonly Random Random = new Random();

        /// <summary>
        /// Given Yarn source code, adds line tags to the ends of all lines
        /// that need one and do not already have one.
        /// </summary>
        /// <remarks>This method ensures that it does not generate line tags that are already present in the file, or present in the `existingLineTags` collection.
        /// 
        /// Line tags are added to any line of source code that contains user-visible text: lines, options, and shortcut options.
        /// </remarks>
        /// <param name="contents">The source code to add line tags to.</param>
        /// <param name="existingLineTags">The collection of line tags already exist elsewhere in the source code; the newly added line tags will not be duplicates of any in this collection.</param>
        /// <returns>The modified source code, with line tags added.</returns>
        public static string AddTagsToLines(string contents, ICollection<string> existingLineTags) {

            Program program;
            IDictionary<string, StringInfo> stringTable;

            Compiler.CompileString(contents, "input", out program, out stringTable);

            var untaggedLines = stringTable.Where(entry => entry.Value.isImplicitTag);

            var allSourceLines = contents.Split(new[] {"\n", "\r\n", "\n"}, StringSplitOptions.None);

            var existingLines = new HashSet<string>(existingLineTags);

            foreach (var untaggedLine in untaggedLines) {
                var lineNumber = untaggedLine.Value.lineNumber;
                var tag = "#" + GenerateString(existingLines);

                allSourceLines[lineNumber-1] += $" {tag}";

                existingLines.Add(tag);
            }

            return string.Join(Environment.NewLine, allSourceLines);
        }

        /// <summary>
        /// Generates a new unique line tag that is not present in
        /// `existingKeys`.
        /// </summary>
        /// <param name="existingKeys">The collection of keys that should
        /// be considered when generating a new, unique line tag.</param>
        /// <returns>A unique line tag that is not already present in
        /// `existingKeys`.</returns>
        private static string GenerateString(ICollection<string> existingKeys) {

            string tag;
            do
            {
                tag = string.Format(CultureInfo.InvariantCulture, "line:{0:x7}", Random.Next(0x1000000));
            }
            while (existingKeys.Contains(tag));

            return tag;
        }

    }
}