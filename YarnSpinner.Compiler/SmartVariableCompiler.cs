// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using System;
    using System.Collections.Generic;

    internal class SmartVariableCompiler : ICodeEmitter
    {
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
        /// <returns>A <see cref="NodeCompilationResult"/> that contains the
        /// generated node for this smart variable and its debugger
        /// information.</returns>
        public NodeCompilationResult Compile(Declaration decl)
        {
            if (decl.InitialValueParserContext == null)
            {
                throw new InvalidOperationException("Internal error: decl had no expression tree");
            }

            return Compile(decl.SourceFileName, decl.Name, decl.InitialValueParserContext);
        }

        public NodeCompilationResult Compile(string sourceFileName, string nodeName, YarnSpinnerParser.ExpressionContext? expression)
        {
            this.CurrentNode = new Node();
            this.CurrentNodeDebugInfo = new NodeDebugInfo(sourceFileName, nodeName)
            {
                IsImplicit = true
            };

            this.CurrentNode.Name = nodeName;
            this.CurrentNode.Headers.Add(new Header { Key = "tags", Value = Program.SmartVariableNodeTag });

            if (expression != null)
            {
                // If we have an expression to generate code from, then create a
                // code generator and use it now. Otherwise, just produce an
                // empty node with the appropriate tags.
                var codeGenerator = new CodeGenerationVisitor(this);
                codeGenerator.Visit(expression);

                this.CurrentNodeDebugInfo.Range = new Range(expression.Start.Line - 1, expression.Start.Column, expression.Stop.Line - 1, expression.Stop.Column + expression.Stop.Text.Length);
            }

            return new NodeCompilationResult
            {
                Node = this.CurrentNode,
                NodeDebugInfo = this.CurrentNodeDebugInfo,
            };
        }

        public void Emit(IToken? startToken, IToken? endToken, Instruction instruction)
        {
            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"{nameof(CurrentNode)} was null when generating a smart variable");
            }

            if (this.CurrentNodeDebugInfo == null)
            {
                throw new InvalidOperationException($"{nameof(CurrentNodeDebugInfo)} was null when generating a smart variable");
            }

            Compiler.Emit(
                this.CurrentNode,
                this.CurrentNodeDebugInfo,
                Utility.GetRange(startToken, endToken),
                instruction
            );
        }

        public void Emit(IToken startToken, IToken endToken, params Instruction[] instructions)
        {
            foreach (var i in instructions)
            {
                this.Emit(startToken, endToken, i);
            }
        }

        public void Emit(Instruction instruction)
        {
            this.Emit(null, null, instruction);
        }
    }
}
