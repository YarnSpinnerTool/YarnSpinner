using System.Collections.Generic;

namespace Yarn.Compiler.Upgrader
{
    internal class LanguageUpgraderV1 : ILanguageUpgrader
    {
        public ICollection<TextReplacement> Upgrade(string contents, string fileName)
        {
            var replacements = new List<TextReplacement>();

            replacements.AddRange(new FormatFunctionUpgrader().Upgrade(contents, fileName));

            return replacements;
        }
    }
}
