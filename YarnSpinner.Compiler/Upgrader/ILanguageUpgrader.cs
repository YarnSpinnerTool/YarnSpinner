// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler.Upgrader
{
    internal interface ILanguageUpgrader
    {
        UpgradeResult Upgrade(UpgradeJob upgradeJob);
    }
}
