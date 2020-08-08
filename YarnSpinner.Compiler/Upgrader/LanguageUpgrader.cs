using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("YarnSpinner.Tests")]

namespace Yarn.Compiler.Upgrader
{
    /// <summary>
    /// Contains information describing a replacement to make in a string.
    /// </summary>
    public struct TextReplacement
    {
        /// <summary>
        /// The position in the original string where the substitution
        /// should be made.
        /// </summary>
        public int Start;

        /// <summary>
        /// The line in the original string where the substitution should
        /// be made.
        /// </summary>
        public int StartLine;

        /// <summary>
        /// The string to expect at <see cref="Start"/> in the original
        /// string.
        /// </summary>
        public string OriginalText;

        /// <summary>
        /// The string to replace <see cref="OriginalText"/> with at <see
        /// cref="Start"/>.
        /// </summary>
        public string ReplacementText;

        /// <summary>
        /// A descriptive comment explaining why the substitution is
        /// necessary.
        /// </summary>
        public string Comment;

        /// <summary>
        /// Gets the length of <see cref="OriginalText"/>.
        /// </summary>
        public int OriginalLength => this.OriginalText.Length;

        /// <summary>
        /// Gets the length of <see cref="ReplacementLength"/>.
        /// </summary>
        public int ReplacementLength => this.ReplacementText.Length;
    }

    /// <summary>
    /// Contains methods for upgrading the syntax of Yarn scripts.
    /// </summary>
    public static class LanguageUpgrader
    {
        /// <summary>
        /// Upgrades a Yarn script from one version of the language to
        /// another, producing both the fully upgraded text as well as a
        /// collection of replacements.
        /// </summary>
        /// <param name="originalContents">The Yarn source, in the original
        /// version of the language. This must be syntactically valid for
        /// the <paramref name="upgradeType"/> upgrade operation you are
        /// performing.</param>
        /// <param name="fileName">The name of the file being
        /// converted.</param>
        /// <param name="upgradeType">The type of language conversion to
        /// perform.</param>
        /// <param name="replacements">When this method returns, contains a
        /// collection of <see cref="TextReplacement"/> structs that describe
        /// the changes made in <paramref
        /// name="originalContents"/>.</param>
        /// <returns>The upgraded version of <paramref
        /// name="originalContents"/>.</returns>
        /// <throws cref="ParseException">Thrown when a syntax error exists
        /// in originalText.</throws> <throws
        /// cref="UpgradeException">Thrown when an error occurs during the
        /// upgrade process.</throws>
        public static string UpgradeScript(string originalContents, string fileName, UpgradeType upgradeType, out IEnumerable<TextReplacement> replacements)
        {
            switch (upgradeType)
            {
                case UpgradeType.Version1to2:
                    replacements = new LanguageUpgraderV1().Upgrade(originalContents, fileName);
                    break;
                default:
                    throw new ArgumentException($"Upgrade type {upgradeType} is not supported.");
            }

            return ApplyReplacements(originalContents, replacements);
        }

        /// <summary>
        /// Applies a collection of string replacements to a string.
        /// </summary>
        /// <param name="originalText">The string to modify.</param>
        /// <param name="replacements">A collection of <see
        /// cref="TextReplacement"/>s to make in the <paramref
        /// name="originalText"/>.</param>
        /// <returns>The modified string.</returns>
        /// <throws cref="ArgumentOutOfRangeException">Thrown when a
        /// replacement refers to an invalid position in originalText, or
        /// its original text does not match what exists in the
        /// text.</throws>
        internal static string ApplyReplacements(string originalText, IEnumerable<TextReplacement> replacements)
        {
            // We need this in order of start position because replacements
            // are very likely to change the length the string, which
            // throws off our start points
            var sortedList = replacements.OrderBy((r) => r.Start);

            // Use a string builder so that we can hopefully be a bit more
            // efficient about rearranging the string
            var text = new System.Text.StringBuilder(originalText);

            // As we perform replacements, differences between original
            // length and replacement length mean that the "start position"
            // for replacements after the first one will affect the
            // replacement point. This variable keeps track of how much the
            // preceding content has lengthened or shortened as we do our
            // replacements.
            int offset = 0;

            foreach (var replacement in sortedList)
            {
                // Ensure that the replacement is for a valid position in
                // the original
                if (replacement.Start > originalText.Length)
                {
                    throw new ArgumentOutOfRangeException($"Replacment's start position ({replacement.Start}) exceeds text length ({originalText.Length})");
                }

                // Ensure that the replacement is replacing the text that
                // it expects to (taking into account any previous
                // replacements that may have been made previously by this
                // method)
                var existingSubstring = text.ToString(replacement.Start + offset, replacement.OriginalText.Length);

                if (existingSubstring != replacement.OriginalText)
                {
                    throw new ArgumentOutOfRangeException($@"Replacement at position {replacement.Start} expected to find text ""{replacement.OriginalText}"", but found ""{existingSubstring}"" instead");
                }

                // Perform the replacement!
                text.Remove(replacement.Start + offset, replacement.OriginalLength);
                text.Insert(replacement.Start + offset, replacement.ReplacementText);

                // This replacement has probably changed the length of the
                // string leading up to here, so update our offset
                var lengthDifference = replacement.ReplacementText.Length - replacement.OriginalText.Length;
                offset += lengthDifference;
            }

            return text.ToString();
        }
    }
}
