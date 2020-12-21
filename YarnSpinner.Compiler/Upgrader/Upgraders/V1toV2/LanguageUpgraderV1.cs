using System.Collections.Generic;
using System.Linq;

namespace Yarn.Compiler.Upgrader
{
    internal class LanguageUpgraderV1 : ILanguageUpgrader
    {
     
        public UpgradeResult Upgrade(UpgradeJob upgradeJob)
        {
            var results = new[] {
                new FormatFunctionUpgrader().Upgrade(upgradeJob),
                new VariableDeclarationUpgrader().Upgrade(upgradeJob),
            };

            return results.Aggregate((result, next) => UpgradeResult.Merge(result, next));
        }
    }
}
