// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System.Collections.Generic;

    internal class StringTableManager
    {
        internal Dictionary<string, StringInfo> StringTable = new Dictionary<string, StringInfo>();

        internal bool ContainsImplicitStringTags
        {
            get
            {
                foreach (var item in this.StringTable)
                {
                    if (item.Value.isImplicitTag)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Registers a new string in the string table.
        /// </summary>
        /// <param name="text">The text of the string to register.</param>
        /// <param name="nodeName">The name of the node that this string
        /// was found in.</param>
        /// <param name="lineID">The line ID to use for this entry in the
        /// string table.</param>
        /// <param name="lineNumber">The line number that this string was
        /// found in.</param>
        /// <param name="tags">The tags to associate with this string in
        /// the string table.</param>
        /// <returns>The string ID for the newly registered
        /// string.</returns>
        /// <remarks>If <paramref name="lineID"/> is <see
        /// langword="null"/>, a line ID will be generated from <paramref
        /// name="fileName"/>, <paramref name="nodeName"/>, and the number
        /// of elements in <see cref="StringTable"/>.</remarks>
        internal string RegisterString(string text, string fileName, string nodeName, string lineID, int lineNumber, string[] tags)
        {
            string lineIDUsed;

            bool isImplicit;

            if (lineID == null)
            {
                lineIDUsed = $"line:{fileName}-{nodeName}-{this.StringTable.Count}";

                isImplicit = true;
            }
            else
            {
                lineIDUsed = lineID;

                isImplicit = false;
            }

            var theString = new StringInfo(text, fileName, nodeName, lineNumber, isImplicit, tags);

            // Finally, add this to the string table, and return the line
            // ID.
            this.StringTable.Add(lineIDUsed, theString);

            return lineIDUsed;
        }

        internal void Add(IDictionary<string, StringInfo> otherStringTable)
        {
            foreach (var entry in otherStringTable)
            {
                this.StringTable.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Checks to see if this string table already contains a line with
        /// the line ID <paramref name="lineID"/>.
        /// </summary>
        /// <param name="lineID">The line ID to check for.</param>
        /// <returns><see langword="true"/> if the string table already
        /// contains a line with this ID, <see langword="false"/>
        /// otherwise.</returns>
        internal bool ContainsKey(string lineID)
        {
            return this.StringTable.ContainsKey(lineID);
        }
    }
}
