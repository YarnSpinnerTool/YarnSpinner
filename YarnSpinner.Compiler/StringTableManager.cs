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

        private static class CRC32
        {
            private static readonly uint[] LookupTable;

            static CRC32()
            {
                uint seedPolynomial = 0xedb88320;
                LookupTable = new uint[256];
                uint temp;
                for (uint i = 0; i < LookupTable.Length; ++i)
                {
                    temp = i;
                    for (int j = 8; j > 0; --j)
                    {
                        if ((temp & 1) == 1)
                        {
                            temp = (temp >> 1) ^ seedPolynomial;
                        }
                        else
                        {
                            temp >>= 1;
                        }
                    }

                    LookupTable[i] = temp;
                }
            }

            public static uint GetChecksum(byte[] bytes)
            {
                uint crc = 0xffffffff;
                for (int i = 0; i < bytes.Length; ++i)
                {
                    byte index = (byte)((crc & 0xff) ^ bytes[i]);
                    crc = (crc >> 8) ^ LookupTable[index];
                }

                return ~crc;
            }

            public static uint GetChecksum(string s) {
                var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                return GetChecksum(bytes);
            }

            /// <summary>
            /// Gets the CRC-32 hash of <paramref name="s"/> as a string
            /// containing 8 lowercase hexadecimal characters.
            /// </summary>
            /// <param name="s">The string to get the checksum of.</param>
            /// <returns>The string containing the checksum.</returns>
            public static string GetChecksumString(string s)
            {
                uint checksum = GetChecksum(s);
                byte[] bytes = System.BitConverter.GetBytes(checksum);
                return System.BitConverter.ToString(bytes).ToLowerInvariant().Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// Registers a new string in the string table.
        /// </summary>
        /// <param name="text">The text of the string to register.</param>
        /// <param name="fileName">The name of the yarn file that this line is contained within</param>
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
                string candidateSeed = $"{fileName}{nodeName}{this.StringTable.Count}";
                int count = 0;
                do
                {
                    if (count > 1000)
                    {
                        throw new DialogueException($"Internal error: string table failed to find a non-colliding hash for \"{candidateSeed}\" after {count} attempts");
                    }

                    string suffix = count != 0 ? count.ToString() : string.Empty;
                    lineIDUsed = "line:" + CRC32.GetChecksumString(candidateSeed + suffix);
                    count += 1;
                }
                while (this.StringTable.ContainsKey(lineIDUsed) == true);

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
