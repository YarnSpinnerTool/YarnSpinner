// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;
    using static Yarn.Instruction.Types;

    internal class SmartVariableCompiler : ICodeEmitter {
        private int labelCount;

        public Node? CurrentNode { get; private set; }
        public NodeDebugInfo? CurrentDebugInfo { get; private set; }
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
            this.CurrentDebugInfo = new NodeDebugInfo(decl.SourceFileName, decl.Name);

            this.CurrentNode.Name = decl.Name;
            this.CurrentNode.Tags.Add(Program.SmartVariableNodeTag);

            var codeGenerator = new CodeGenerationVisitor(this, trackingVariableName: null);
            codeGenerator.Visit(decl.InitialValueParserContext);

            node = this.CurrentNode;
            debugInfo = this.CurrentDebugInfo;
        }

        /// <inheritdoc />
        public void AddLabel(string name, int position)
        {
            this.CurrentNode?.Labels.Add(name, position);
        }

        /// <inheritdoc />
        public string RegisterLabel(string? commentary = null)
        {
            return $"L{this.labelCount++}{commentary ?? string.Empty}";
        }

        /// <inheritdoc />
        void ICodeEmitter.Emit(OpCode code, IToken startToken, params Operand[] operands)
        {
            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was emitting code for a smart variable");
            }

            if (this.CurrentDebugInfo == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentDebugInfo)} was null was emitting code for a smart variable");
            }
            
            Compiler.Emit(this.CurrentNode, this.CurrentDebugInfo, startToken?.Line - 1 ?? -1, startToken?.Column ?? -1, code, operands);
        }

        /// <inheritdoc />
        void ICodeEmitter.Emit(OpCode code, params Operand[] operands)
        {
            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was emitting code for a smart variable");
            }

            if (this.CurrentDebugInfo == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentDebugInfo)} was null was emitting code for a smart variable");
            }
            
            Compiler.Emit(this.CurrentNode, this.CurrentDebugInfo, -1, -1, code, operands);
        }
    }
}
