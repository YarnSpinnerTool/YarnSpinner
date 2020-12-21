using System.Collections.Generic;

namespace Yarn.Compiler.Upgrader
{
    internal interface ILanguageUpgrader
    {
        UpgradeResult Upgrade(UpgradeJob upgradeJob);
    }
}
