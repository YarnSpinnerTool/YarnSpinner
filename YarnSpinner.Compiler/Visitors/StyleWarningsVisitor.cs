// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal class StyleWarningsVisitor : DiagnosticsGeneratorVisitor
    {
        private readonly CommonTokenStream tokenStream;

        public StyleWarningsVisitor(string fileName, CommonTokenStream tokenStream) : base(fileName)
        {
            this.tokenStream = tokenStream;
        }
    }
}
