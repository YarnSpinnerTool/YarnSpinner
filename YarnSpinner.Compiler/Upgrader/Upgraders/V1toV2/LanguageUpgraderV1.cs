// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler.Upgrader
{
    using System.Linq;
    internal class LanguageUpgraderV1 : ILanguageUpgrader
    {
        public UpgradeResult Upgrade(UpgradeJob upgradeJob)
        {
            var results = new[]
            {
                new FormatFunctionUpgrader().Upgrade(upgradeJob),
                new VariableDeclarationUpgrader().Upgrade(upgradeJob),
                new OptionsUpgrader().Upgrade(upgradeJob),
            };

            return results.Aggregate((result, next) => UpgradeResult.Merge(result, next));
        }
    }
}
