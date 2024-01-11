// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    /// <summary>
    /// Provides a method for generating CRC32 hashes of strings.
    /// </summary>
    internal static class CRC32
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

        /// <summary>
        /// Computes a CRC32 checksum from the given bytes.
        /// </summary>
        /// <param name="bytes">The bytes to generate a checksum
        /// for.</param>
        /// <returns>A CRC32 checksum derived from <paramref
        /// name="bytes"/>.</returns>
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

        /// <summary>
        /// Computes a CRC32 checksum from the given string.
        /// </summary>
        /// <remarks>
        /// This method converts the string to a UTF-8 encoding, and then
        /// computes a CRC32 checksum from those bytes.
        /// </remarks>
        /// <param name="s">The string to generate a checksum for.</param>
        /// <returns>A CRC32 checksum derived from <paramref
        /// name="s"/>.</returns>
        public static uint GetChecksum(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            return GetChecksum(bytes);
        }

        /// <summary>
        /// Gets the CRC-32 hash of <paramref name="s"/> as a string
        /// containing 8 lowercase hexadecimal characters.
        /// </summary>
        /// <remarks>
        /// This method converts the string to a UTF-8 encoding, and then
        /// computes a CRC32 checksum from those bytes.
        /// </remarks>
        /// <param name="s">The string to get the checksum of.</param>
        /// <returns>The string containing the checksum.</returns>
        public static string GetChecksumString(string s)
        {
            uint checksum = GetChecksum(s);
            byte[] bytes = System.BitConverter.GetBytes(checksum);
            return System.BitConverter.ToString(bytes).ToLowerInvariant().Replace("-", string.Empty);
        }
    }
}
