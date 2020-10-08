namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Utility methods for working with line tags.
    /// </summary>
    public static class Utility
    {
        private static readonly Random Random = new Random();

        /// <summary>
        /// Generates a Yarn script that contains a node that declares
        /// variables.
        /// </summary>
        /// <remarks>This method is intended to be called by tools that let
        /// the user manage variable declarations. Such tools can read the
        /// existing variable declarations in from a script (by compiling
        /// the script with the `DeclarationsOnly` <see
        /// cref="CompilationJob.CompilationType"/>), allow the user to
        /// make changes, and then write the changes to disk by calling
        /// this method and saving the results.</remarks>
        /// <param name="declarations">The collection of <see
        /// cref="Declaration"/> objects to include in the output.</param>
        /// <param name="title">The title of the node that should be
        /// generated.</param>
        /// <param name="tags">The collection of tags that should be
        /// generated for the node. If this is <see langword="null"/>, no
        /// tags will be generated.</param>
        /// <param name="headers">The collection of additional headers that
        /// should be generated for the node. If this is <see
        /// langword="null"/>, no additional headers will be
        /// generated.</param>
        /// <returns>A string containing a Yarn script that declares the
        /// specified variables.</returns>
        /// <throws cref="ArgumentOutOfRangeException">Thrown when any of
        /// the <see cref="Declaration"/> objects in <paramref
        /// name="declarations"/> is not a variable declaration, or if the
        /// <see cref="Declaration.ReturnType"/> of any of the declarations
        /// is an invalid value.</throws>
        public static string GenerateYarnFileWithDeclarations(
            IEnumerable<Yarn.Compiler.Declaration> declarations,
            string title = "Program",
            IEnumerable<string> tags = null,
            IDictionary<string, string> headers = null)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"title: {title}");

            if (tags != null)
            {
                stringBuilder.AppendLine($"tags: {string.Join(" ", tags)}");
            }

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    stringBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
                }
            }

            stringBuilder.AppendLine("---");

            foreach (var decl in declarations)
            {
                if (decl.DeclarationType != Declaration.Type.Variable)
                {
                    throw new ArgumentOutOfRangeException($"Declaration {decl.name} is a {decl.DeclarationType}; it must be a {nameof(Declaration.Type.Variable)}.");
                }

                stringBuilder.Append($"<<declare {decl.name} = ");

                switch (decl.ReturnType)
                {
                    case Yarn.Type.Number:
                        stringBuilder.Append(decl.defaultValue);
                        break;
                    case Yarn.Type.String:
                        stringBuilder.Append('"' + (string)decl.defaultValue + '"');
                        break;
                    case Yarn.Type.Bool:
                        stringBuilder.Append((bool)decl.defaultValue ? "true" : "false");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Declaration {decl.name}'s return type must not be {decl.ReturnType}.");
                }

                if (string.IsNullOrEmpty(decl.description) == false)
                {
                    stringBuilder.Append($" \"{decl.description}\"");
                }

                stringBuilder.AppendLine(">>");
            }

            stringBuilder.AppendLine("===");

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Given Yarn source code, adds line tags to the ends of all lines
        /// that need one and do not already have one.
        /// </summary>
        /// <remarks>This method ensures that it does not generate line
        /// tags that are already present in the file, or present in the
        /// `existingLineTags` collection.
        ///
        /// Line tags are added to any line of source code that contains
        /// user-visible text: lines, options, and shortcut options.
        /// </remarks>
        /// <param name="contents">The source code to add line tags
        /// to.</param>
        /// <param name="existingLineTags">The collection of line tags
        /// already exist elsewhere in the source code; the newly added
        /// line tags will not be duplicates of any in this
        /// collection.</param>
        /// <returns>The modified source code, with line tags
        /// added.</returns>
        public static string AddTagsToLines(string contents, ICollection<string> existingLineTags)
        {
            var compileJob = CompilationJob.CreateFromString("input", contents);

            compileJob.CompilationType = CompilationJob.Type.StringsOnly;

            var result = Compiler.Compile(compileJob);

            var untaggedLines = result.StringTable.Where(entry => entry.Value.isImplicitTag);

            var allSourceLines = contents.Split(new[] { "\n", "\r\n", "\n" }, StringSplitOptions.None);

            var existingLines = new HashSet<string>(existingLineTags);

            foreach (var untaggedLine in untaggedLines)
            {
                var lineNumber = untaggedLine.Value.lineNumber;
                var tag = "#" + GenerateString(existingLines);

                allSourceLines[lineNumber - 1] += $" {tag}";

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
        private static string GenerateString(ICollection<string> existingKeys)
        {
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
