using System.Collections.Generic;

namespace Yarn.Compiler.Upgrader
{
    internal interface ILanguageUpgrader
    {
        ICollection<TextReplacement> Upgrade(string contents, string fileName);
    }
}
