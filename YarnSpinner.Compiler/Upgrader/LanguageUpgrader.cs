// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("YarnSpinner.Tests")]

namespace Yarn.Compiler.Upgrader
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;

    public struct UpgradeJob
    {
        public List<Yarn.Compiler.CompilationJob.File> Files;

        public UpgradeType UpgradeType;

        public UpgradeJob(UpgradeType upgradeType, IEnumerable<CompilationJob.File> files)
        {
            this.Files = new List<CompilationJob.File>(files);
            this.UpgradeType = upgradeType;
        }
    }

    public struct UpgradeResult
    {
        public List<OutputFile> Files;

        /// <summary>
        /// Gets a collection containing all <see cref="Diagnostic"/>
        /// objects across all of the files in <see cref="Files"/>.
        /// </summary>
        public IEnumerable<Diagnostic> Diagnostics => Files.SelectMany(f => f.Diagnostics);

        internal static UpgradeResult Merge(UpgradeResult a, UpgradeResult b)
        {
            var filePairs = a.Files
                .Join(
                b.Files,
                first => first.Path,
                second => second.Path,
                (first, second) => new { FileA = first, FileB = second });

            var onlyResultA = a.Files.Where(f1 => b.Files.Select(f2 => f2.Path).Contains(f1.Path) == false);
            var onlyResultB = b.Files.Where(f1 => a.Files.Select(f2 => f2.Path).Contains(f1.Path) == false);

            var mergedFiles = filePairs.Select(pair => OutputFile.Merge(pair.FileA, pair.FileB));

            var allFiles = onlyResultA.Concat(onlyResultB).Concat(mergedFiles);

            return new UpgradeResult()
            {
                Files = allFiles.ToList(),
            };
        }

        public struct OutputFile
        {
            public string Path;
            public IEnumerable<TextReplacement> Replacements;
            public string OriginalSource;
            public string UpgradedSource => LanguageUpgrader.ApplyReplacements(this.OriginalSource, this.Replacements);

            public IEnumerable<Diagnostic> Diagnostics;

            /// <summary>
            /// Indicates whether this <see cref="OutputFile"/> represents
            /// a new file to be created. If this is <see
            /// langword="true"/>, <see cref="OriginalSource"/> will be the
            /// empty string, and <see cref="Replacements"/> will be empty.
            /// </summary>
            public bool IsNewFile;

            internal OutputFile(
                string path,
                IEnumerable<TextReplacement> replacements,
                string originalSource,
                IEnumerable<Diagnostic> diagnostics = null)
            {
                this.Path = path;
                this.Replacements = replacements;
                this.OriginalSource = originalSource;
                this.IsNewFile = false;
                this.Diagnostics = diagnostics ?? new List<Diagnostic>();
            }

            internal OutputFile(
                string path,
                string newContent,
                IEnumerable<Diagnostic> diagnostics = null)
            {
                this.Path = path;
                this.OriginalSource = newContent;
                this.Replacements = new List<TextReplacement>();
                this.IsNewFile = true;
                this.Diagnostics = diagnostics ?? new List<Diagnostic>();
            }

            /// <summary>
            /// Merges two <see cref="OutputFile"/> objects, producing a
            /// merged result.
            /// </summary>
            /// <param name="a">The first file.</param>
            /// <param name="b">The second file.</param>
            /// <returns>The merged result.</returns>
            internal static OutputFile Merge(OutputFile a, OutputFile b)
            {
                if (a.Path != b.Path)
                {
                    throw new ArgumentException($"Cannot merge {a.Path} and {b.Path}: {nameof(Path)} fields differ");
                }

                if (a.OriginalSource != b.OriginalSource)
                {
                    throw new ArgumentException($"Cannot merge {a.Path} and {b.Path}: {nameof(OriginalSource)} fields differ");
                }

                if (a.IsNewFile || b.IsNewFile)
                {
                    throw new ArgumentException($"Cannot merge {a.Path} and {b.Path}: one or both of them are new files");
                }

                // Combine and sort the list of replacements
                var mergedReplacements = a.Replacements
                    .Concat(b.Replacements)
                    .OrderBy(r => r.StartLine)
                    .ThenBy(r => r.Start);

                // Generate a new output file from the result
                return new OutputFile(a.Path, mergedReplacements, a.OriginalSource);
            }
        }
    }

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
        /// <param name="upgradeJob">The upgrade job to perform.</param>
        /// <throws cref="ArgumentException">Thrown if the 
        /// <see cref="UpgradeJob.UpgradeType"/> is unsupported.</throws>
        /// <returns>An <see cref="UpgradeResult"/> object containing the
        /// results of the upgrade operation.</returns>
        public static UpgradeResult Upgrade(UpgradeJob upgradeJob)
        {
            switch (upgradeJob.UpgradeType)
            {
                case UpgradeType.Version1to2:
                    return new LanguageUpgraderV1().Upgrade(upgradeJob);
                default:
                    throw new ArgumentException($"Upgrade type {upgradeJob.UpgradeType} is not supported.");
            }
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
