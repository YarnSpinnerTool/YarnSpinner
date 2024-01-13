// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;

    internal class SmartVariableCompiler : ICodeEmitter {
        public Node? CurrentNode { get; private set; }
        public NodeDebugInfo? CurrentNodeDebugInfo { get; private set; }
        public IDictionary<string, Declaration> VariableDeclarations { get; private set; }

        public SmartVariableCompiler(IDictionary<string, Declaration> variableDeclarations)
        {
            this.VariableDeclarations = variableDeclarations;
        }

        /// <summary>
        /// Compiles a Declaration describing a smart variable, and produces a
        /// <see cref="Node"/> and a <see cref="NodeDebugInfo"/>.
        /// </summary>
        /// <param name="decl">The Declaration to generate an implementation
        /// node for.</param>
        /// <param name="node">The resulting implementation node.</param>
        /// <param name="debugInfo">The debug info for <paramref
        /// name="node"/>.</param>
        public void Compile(Declaration decl, out Node node, out NodeDebugInfo debugInfo)
        {
            this.CurrentNode = new Node();
            this.CurrentNodeDebugInfo = new NodeDebugInfo(decl.SourceFileName, decl.Name);

            this.CurrentNode.Name = decl.Name;
            this.CurrentNode.Headers.Add(new Header { Key = "tags", Value = Program.SmartVariableNodeTag });

            var codeGenerator = new CodeGenerationVisitor(this);
            codeGenerator.Visit(decl.InitialValueParserContext);

            node = this.CurrentNode;
            debugInfo = this.CurrentNodeDebugInfo;
        }

        public void Emit(IToken? startToken, Instruction instruction)
        {
            if (this.CurrentNode == null) {
                throw new InvalidOperationException($"{nameof(CurrentNode)} was null when generating a smart variable");
            }
            
            if (this.CurrentNodeDebugInfo == null) {
                throw new InvalidOperationException($"{nameof(CurrentNodeDebugInfo)} was null when generating a smart variable");
            }

            Compiler.Emit(
                this.CurrentNode, 
                this.CurrentNodeDebugInfo, 
                startToken?.Line - 1 ?? -1, 
                startToken?.Column ?? -1, 
                instruction
            );
        }

        public void Emit(IToken startToken, params Instruction[] instructions)
        {
            foreach (var i in instructions) {
                this.Emit(startToken, i);
            }
        }

        public void Emit(Instruction instruction)
        {
            this.Emit(null, instruction);
        }
    }
}
