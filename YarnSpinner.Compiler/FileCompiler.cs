// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using System;
    using System.Collections.Generic;
    using static Yarn.Instruction.Types;

    /// <summary>
    /// An <see cref="ICodeEmitter"/> that generates code for a parsed file.
    /// </summary>
    internal class FileCompiler : YarnSpinnerParserBaseListener, ICodeEmitter
    {

        internal struct CompilationContext
        {
            internal ILibrary? Library;
            internal FileParseResult FileParseResult;
            internal HashSet<string> TrackingNodes;
            internal Dictionary<string, Declaration> VariableDeclarations;

            internal HashSet<string> NodesToSkip;
        }

        private FileCompilationResult CompilationResult { get; set; }

        /// <summary>
        /// Gets the current node to which instructions are being added.
        /// </summary>
        /// <value>The current node.</value>
        public Node? CurrentNode { get; private set; }

        /// <summary>
        /// Gets the current debug information that describes <see
        /// cref="CurrentNode"/>.
        /// </summary>
        private NodeDebugInfo? CurrentNodeDebugInfo { get; set; }

        internal FileParseResult FileParseResult { get; private set; }

        /// <summary>
        /// The collection of variable declarations known to the compiler.
        /// </summary>
        /// <remarks>
        /// This is supplied as part of a <see cref="CompilationJob"/>.
        /// </remarks>
        public IDictionary<string, Declaration> VariableDeclarations { get; set; } = new Dictionary<string, Declaration>();

        public HashSet<string> NodesToSkip { get; }

        /// <summary>
        /// The Library, which contains the function declarations known to the
        /// compiler.
        /// </summary>
        /// <remarks>
        /// This is supplied as part of a <see cref="CompilationJob"/>.
        /// </remarks>
        internal ILibrary? Library { get; set; }

        NodeDebugInfo? ICodeEmitter.CurrentNodeDebugInfo => this.CurrentNodeDebugInfo;

        // the list of nodes we have to ensure we track visitation
        internal HashSet<string> TrackingNodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Compiler"/> class.
        /// </summary>
        internal FileCompiler(CompilationContext compilationContext)
        {
            this.CompilationResult = new FileCompilationResult();
            this.FileParseResult = compilationContext.FileParseResult;
            this.Library = compilationContext.Library;
            this.TrackingNodes = compilationContext.TrackingNodes;
            this.VariableDeclarations = compilationContext.VariableDeclarations;
            this.NodesToSkip = compilationContext.NodesToSkip;
        }


        /// <summary>
        /// Adds the specified instruction it to the current node in the <see
        /// cref="Program"/>, and associates it with the position of <paramref
        /// name="startToken"/>.
        /// </summary>
        /// <remarks>
        /// Called by instances of <see cref="CodeGenerationVisitor"/> while
        /// walking the parse tree.
        /// </remarks>
        /// <param name="startToken">The first token in the expression or
        /// statement that was responsible for emitting this
        /// instruction.</param>
        /// <param name="endToken">The last token in the expression or
        /// statement that was responsible for emitting this
        /// instruction.</param>
        /// <param name="instruction">The instruction to emit.</param>
        public void Emit(IToken? startToken, IToken? endToken, Instruction instruction)
        {
            Compiler.Emit(this.CurrentNode ?? throw new InvalidOperationException(),
                          this.CurrentNodeDebugInfo ?? throw new InvalidOperationException(),
                          Utility.GetRange(startToken, endToken),
                          instruction);
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

        // This replaces the CompileNode from the old compiler. We will start
        // walking the parse tree, emitting byte code as it goes along. This
        // will all get stored into our program. Var needs a tree to walk, this
        // comes from the ANTLR Parser/Lexer steps
        internal FileCompilationResult Compile()
        {
            this.CompilationResult = new FileCompilationResult();
            Antlr4.Runtime.Tree.ParseTreeWalker walker = new Antlr4.Runtime.Tree.ParseTreeWalker();
            walker.Walk(this, this.FileParseResult.Tree);
            return this.CompilationResult;
        }

        /// <summary>
        /// We have found a new node. Set up the currentNode var ready to hold
        /// it, and otherwise continue.
        /// </summary>
        /// <inheritdoc/>
        public override void EnterNode(YarnSpinnerParser.NodeContext context)
        {
            this.CurrentNode = new Node();

            if (context.NodeTitle == null)
            {
                throw new InvalidOperationException("Internal error: node context has no title");
            }

            this.CurrentNodeDebugInfo = new NodeDebugInfo(FileParseResult.Name, "<unknown>")
            {
                Range = new Range(
                    context.Start.Line - 1,
                    context.Start.Column,
                    context.Stop.Line - 1,
                    context.Stop.Column + (context.Stop.StopIndex - context.Stop.StartIndex)
                )
            };

            // Set the name of the node
            this.CurrentNode.Name = context.NodeTitle;
            this.CurrentNodeDebugInfo.NodeName = context.NodeTitle;

            this.CurrentNode.Headers.Add(new Header
            {
                Key = "title",
                Value = context.NodeTitle,
            });
            if (context.NodeGroup != null)
            {
                this.CurrentNode.Headers.Add(new Header
                {
                    Key = Node.NodeGroupHeader,
                    Value = context.NodeGroup,
                });
            }

        }

        /// <summary>
        /// We have left the current node. Store it into the program, wipe the
        /// var, and make it ready to go again.
        /// </summary>
        /// <inheritdoc />
        public override void ExitNode(YarnSpinnerParser.NodeContext context)
        {
            if (string.IsNullOrEmpty(this.CurrentNode?.Name))
            {
                // We don't have a name for this node. We can't emit code for
                // it.
                this.CompilationResult.Diagnostics.Add(
                    new Diagnostic(
                        this.FileParseResult.Name,
                        context,
                        "Missing title header for node"
                    )
                );
            }
            else
            {
                if (this.CurrentNode == null)
                {
                    throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was null when exiting a node");
                }

                if (this.CurrentNodeDebugInfo == null)
                {
                    throw new InvalidOperationException($"Internal error: {nameof(CurrentNodeDebugInfo)} was null when exiting a node");
                }

                if (TrackingNodes.Contains(this.CurrentNode.Name))
                {
                    // This node needs to be tracked. Add a header that
                    // describes which variable the virtual machine should use
                    // for tracking.
                    this.CurrentNode.Headers.Add(
                        new Header
                        {
                            Key = Node.TrackingVariableNameHeader,
                            Value = StandardLibrary.GenerateUniqueVisitedVariableForNode(CurrentNode.Name)
                        }
                    );
                }

                if (this.NodesToSkip.Contains(this.CurrentNode.Name))
                {
                    // We've been told to not include this node.
                }
                else
                {
                    // Add the node to our result.
                    CompilationResult.Nodes.Add(this.CurrentNode);
                    CompilationResult.DebugInfos.Add(this.CurrentNodeDebugInfo);
                }

            }

            this.CurrentNode = null;
        }

        /// <summary> 
        /// We have finished with the header, so we're about to enter the node
        /// body, and all its statements. Do the initial setup required before
        /// compiling.
        /// </summary>
        /// <inheritdoc />
        public override void ExitHeader(YarnSpinnerParser.HeaderContext context)
        {
            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was null when exiting a header");
            }

            if (this.CurrentNodeDebugInfo == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNodeDebugInfo)} was null when exiting a header");
            }

            var headerKey = context.header_key.Text;

            // Use the header value if provided, else fall back to the empty
            // string. This means that a header like "foo: \n" will be stored as
            // 'foo', '', consistent with how it was typed. That is, it's not
            // null, because a header was provided, but it was written as an
            // empty line.
            var headerValue = context.header_value?.Text ?? String.Empty;

            var header = new Header
            {
                Key = headerKey,
                Value = headerValue
            };
            this.CurrentNode.Headers.Add(header);
        }

        /// <summary>
        /// Have entered the body. The header should have finished being parsed,
        /// and the currentNode is ready. All we do is set up a body visitor and
        /// tell it to run through all the statements. It handles everything
        /// from that point onwards.
        /// </summary>
        /// <inheritdoc />
        public override void EnterBody(YarnSpinnerParser.BodyContext context)
        {
            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was null when entering a body");
            }

            if (this.CurrentNodeDebugInfo == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNodeDebugInfo)} was null when entering a body");
            }

            CodeGenerationVisitor visitor = new CodeGenerationVisitor(this);

            foreach (var statement in context.statement())
            {
                visitor.Visit(statement);
            }
        }


        /// <summary>
        /// Closes off the node.
        /// </summary>
        /// <inheritdoc />
        public override void ExitBody(YarnSpinnerParser.BodyContext context)
        {
            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was null when entering a body");
            }

            if (this.CurrentNodeDebugInfo == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNodeDebugInfo)} was null when entering a body");
            }

            // We have exited the body; emit a 'return' instruction here.
            Compiler.Emit(
                this.CurrentNode,
                this.CurrentNodeDebugInfo,
                Utility.GetRange(context.Stop, context.Stop),
                new Instruction { Return = new ReturnInstruction { } }
            );
        }
    }
}
