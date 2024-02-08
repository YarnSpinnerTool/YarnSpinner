// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System.Collections.Generic;

    internal class StringTableManager
    {
        internal Dictionary<string, StringInfo> StringTable = new Dictionary<string, StringInfo>();

        internal Dictionary<string, YarnSpinnerParser.Line_statementContext> LineContexts = new Dictionary<string, YarnSpinnerParser.Line_statementContext>();

        internal bool ContainsImplicitStringTags
        {
            get
            {
                foreach (var item in this.StringTable)
                {
                    if (item.Value.isImplicitTag && item.Value.shadowLineID == null)
                    {
                        // This is an implicitly created line ID, and is not
                        // shadowing another line.
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Registers a new string in the string table.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="text">The text of the string to register.</param>
        /// <param name="fileName">The name of the yarn file that this line is contained within</param>
        /// <param name="nodeName">The name of the node that this string
        /// was found in.</param>
        /// <param name="existingLineID">The line ID to use for this entry in the
        /// string table.</param>
        /// <param name="lineNumber">The line number that this string was
        /// found in.</param>
        /// <param name="tags">The tags to associate with this string in
        /// the string table.</param>
        /// <param name="shadowID">The line ID that this line is shadowing.</param>
        /// <returns>The string ID for the newly registered
        /// string.</returns>
        /// <remarks>If <paramref name="existingLineID"/> is <see
        /// langword="null"/>, a line ID will be generated from <paramref
        /// name="fileName"/>, <paramref name="nodeName"/>, and the number
        /// of elements in <see cref="StringTable"/>.</remarks>
        internal string RegisterString(YarnSpinnerParser.Line_statementContext context, string? text, string fileName, string nodeName, string? existingLineID, int lineNumber, string[] tags, string? shadowID)
        {
            string lineIDUsed;

            bool isImplicit;

            if (existingLineID == null)
            {
                string candidateSeed = $"{fileName}{nodeName}{this.StringTable.Count}";
                int count = 0;
                do
                {
                    if (count > 1000)
                    {
                        throw new DialogueException($"Internal error: string table failed to find a non-colliding hash for \"{candidateSeed}\" after {count} attempts");
                    }

                    string suffix = count != 0 ? count.ToString() : string.Empty;

                    string prefix = "";
                    if (shadowID != null)
                    {
                        // Line IDs that we generate for shadow lines are prefixed
                        // with a tag to help identify them. (Don't rely on this
                        // prefix to detect that a line ID is a shadow line - it's
                        // purely for internal convenience.)
                        prefix = "sh_";
                    }
                    lineIDUsed = "line:" + prefix + CRC32.GetChecksumString(candidateSeed + suffix);
                    count += 1;
                }
                while (this.StringTable.ContainsKey(lineIDUsed) == true);

                isImplicit = true;
            }
            else
            {
                lineIDUsed = existingLineID;

                isImplicit = false;
            }

            var theString = new StringInfo(text, fileName, nodeName, lineNumber, isImplicit, tags, shadowID);

            // Finally, add this to the string table, and return the line
            // ID.
            this.StringTable.Add(lineIDUsed, theString);

            this.LineContexts.Add(lineIDUsed, context);

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
