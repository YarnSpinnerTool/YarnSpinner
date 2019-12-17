using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Yarn.Compiler {
    public static class Utility {

        public static Random random = new Random();

        // Generates a new unique line tag that is not present in 'existingKeys'.
        static string GenerateString(ICollection<string> existingKeys) {

            string tag = null;
            
            do
            {
                tag = string.Format(CultureInfo.InvariantCulture, "line:{0:x7}", random.Next(0x1000000));
            } while (existingKeys.Contains(tag));

            return tag;
        }

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

    }
}