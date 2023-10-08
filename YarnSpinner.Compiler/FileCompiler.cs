// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using System;
    using System.Collections.Generic;
    using static Yarn.Instruction.Types;

    internal class FileCompiler : YarnSpinnerParserBaseListener, ICodeEmitter {

        internal struct CompilationContext {
            internal Library Library;
            internal FileParseResult FileParseResult;
            internal HashSet<string> TrackingNodes;
            internal Dictionary<string, Declaration> VariableDeclarations;
        }

        private FileCompilationResult CompilationResult { get; set; }

         private int labelCount = 0;

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

        /// <summary>
        /// Gets or sets a value indicating whether we are currently parsing the
        /// current node as a 'raw text' node, or as a fully syntactic node.
        /// </summary>
        /// <value>Whether this is a raw text node or not.</value>
        internal bool RawTextNode { get; set; } = false;

        internal FileParseResult FileParseResult { get; private set; }

        /// <summary>
        /// The collection of variable declarations known to the compiler.
        /// </summary>
        /// <remarks>
        /// This is supplied as part of a <see cref="CompilationJob"/>.
        /// </remarks>
        public IDictionary<string, Declaration> VariableDeclarations { get; set; } = new Dictionary<string, Declaration>();

        /// <summary>
        /// The Library, which contains the function declarations known to the
        /// compiler.
        /// </summary>
        /// <remarks>
        /// This is supplied as part of a <see cref="CompilationJob"/>.
        /// </remarks>
        internal Library Library { get; set; }

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
            this.TrackingNodes  = compilationContext.TrackingNodes;
            this.VariableDeclarations = compilationContext.VariableDeclarations;
        }

        void ICodeEmitter.AddLabel(string name, int position) {
            this.CurrentNode?.Labels.Add(name, position);
        }


        /// <summary>
        /// Generates a unique label name to use in the program.
        /// </summary>
        /// <param name="commentary">Any additional text to append to the
        /// end of the label.</param>
        /// <returns>The new label name.</returns>
        public string RegisterLabel(string? commentary = null)
        {
            return "L" + this.labelCount++ + commentary;
        }


        /// <summary>
        /// Creates a new instruction, and appends it to the current node in the
        /// <see cref="Program"/>.
        /// </summary>
        /// <remarks>
        /// Called by instances of <see
        /// cref="CodeGenerationVisitor"/> while walking the parse tree.
        /// </remarks>
        /// <param name="code">The opcode of the instruction.</param>
        /// <param name="startToken">The first token in the expression or
        /// statement that was responsible for emitting this
        /// instruction.</param>
        /// <param name="operands">The operands to associate with the
        /// instruction.</param>
        void ICodeEmitter.Emit(OpCode code, IToken startToken, params Operand[] operands)
        {
            Compiler.Emit(this.CurrentNode ?? throw new InvalidOperationException(),
                          this.CurrentNodeDebugInfo  ?? throw new InvalidOperationException(),
                          startToken?.Line - 1 ?? -1,
                          startToken?.Column ?? -1,
                          code,
                          operands);
        }

        /// <summary>
        /// Creates a new instruction, and appends it to the current node in the
        /// <see cref="Program"/>.
        /// Differs from the other Emit call by not requiring a start token.
        /// This enables its use in pure synthesised elements of the Yarn.
        /// </summary>
        /// <remarks>
        /// Called by instances of <see
        /// cref="CodeGenerationVisitor"/> while walking the parse tree.
        /// </remarks>
        /// <param name="code">The opcode of the instruction.</param>
        /// <param name="operands">The operands to associate with the
        /// instruction.</param>
        void ICodeEmitter.Emit(OpCode code, params Operand[] operands)
        {
            Compiler.Emit(this.CurrentNode ?? throw new InvalidOperationException(),
                          this.CurrentNodeDebugInfo ?? throw new InvalidOperationException(),
                          sourceLine: -1,
                          sourceCharacter: -1,
                          code,
                          operands);
        }

        // this replaces the CompileNode from the old compiler will start
        // walking the parse tree emitting byte code as it goes along this
        // will all get stored into our program var needs a tree to walk,
        // this comes from the ANTLR Parser/Lexer steps
        internal FileCompilationResult Compile()
        {
            this.CompilationResult = new FileCompilationResult();
            Antlr4.Runtime.Tree.ParseTreeWalker walker = new Antlr4.Runtime.Tree.ParseTreeWalker();
            walker.Walk(this, this.FileParseResult.Tree);
            return this.CompilationResult;
        }
        
        /// <summary>
        /// we have found a new node set up the currentNode var ready to
        /// hold it and otherwise continue
        /// </summary>
        /// <inheritdoc/>
        public override void EnterNode(YarnSpinnerParser.NodeContext context)
        {
            this.CurrentNode = new Node();
            this.CurrentNodeDebugInfo = new NodeDebugInfo(FileParseResult.Name, "<unknown>");
            this.RawTextNode = false;
        }

        /// <summary>
        /// have left the current node store it into the program wipe the
        /// var and make it ready to go again
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
                if (this.CurrentNode == null) {
                    throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was null when exiting a node");
                }
                
                if (this.CurrentNodeDebugInfo == null) {
                    throw new InvalidOperationException($"Internal error: {nameof(CurrentNodeDebugInfo)} was null when exiting a node");
                }

                CompilationResult.Nodes.Add(this.CurrentNode);
                CompilationResult.DebugInfos.Add(this.CurrentNodeDebugInfo);
                

                
            }

            this.CurrentNode = null;
            this.RawTextNode = false;
        }

        /// <summary> 
        /// have finished with the header so about to enter the node body
        /// and all its statements do the initial setup required before
        /// compiling that body statements eg emit a new startlabel
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

            // Use the header value if provided, else fall back to the
            // empty string. This means that a header like "foo: \n" will
            // be stored as 'foo', '', consistent with how it was typed.
            // That is, it's not null, because a header was provided, but
            // it was written as an empty line.
            var headerValue = context.header_value?.Text ?? String.Empty;

            if (headerKey.Equals("title", StringComparison.InvariantCulture))
            {
                // Set the name of the node
                this.CurrentNode.Name = headerValue;
                this.CurrentNodeDebugInfo.NodeName = this.CurrentNode.Name;
            }

            if (headerKey.Equals("tags", StringComparison.InvariantCulture))
            {
                // Split the list of tags by spaces, and use that
                var tags = headerValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                this.CurrentNode.Tags.Add(tags);

                if (this.CurrentNode.Tags.Contains("rawText"))
                {
                    // This is a raw text node. Flag it as such for future
                    // compilation.
                    this.RawTextNode = true;
                }
            }

            var header = new Header
            {
                Key = headerKey,
                Value = headerValue
            };
            this.CurrentNode.Headers.Add(header);
        }

        /// <summary>
        /// have entered the body the header should have finished being
        /// parsed and currentNode ready all we do is set up a body visitor
        /// and tell it to run through all the statements it handles
        /// everything from that point onwards
        /// </summary>
        /// <inheritdoc />
        public override void EnterBody(YarnSpinnerParser.BodyContext context)
        {
            // ok so something in here needs to be a bit different
            // also need to emit tracking code here for when we fall out of a node that needs tracking?
            // or should do I do in inside the codegenvisitor?

            if (this.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNode)} was null when entering a body");
            }

            if (this.CurrentNodeDebugInfo == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(CurrentNodeDebugInfo)} was null when entering a body");
            }

            // if it is a regular node
            if (!this.RawTextNode)
            {
                // This is the start of a node that we can jump to. Add a
                // label at this point.
                this.CurrentNode.Labels.Add(this.RegisterLabel(), this.CurrentNode.Instructions.Count);

                string? track = TrackingNodes.Contains(CurrentNode.Name) ? Yarn.Library.GenerateUniqueVisitedVariableForNode(CurrentNode.Name) : null;

                CodeGenerationVisitor visitor = new CodeGenerationVisitor(this, track);

                foreach (var statement in context.statement())
                {
                    visitor.Visit(statement);
                }
            }

            // We are a rawText node. Don't compile it; instead, note the
            // string
            else
            {
                this.CurrentNode.SourceTextStringID = Compiler.GetLineIDForNodeName(this.CurrentNode.Name);
            }
        }


        /// <summary>
        /// Cleans up any remaining node tracking values and emits necessary instructions to support visitation and close off the node
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

            // this gives us the final increment at the end of the node
            // this is for when we visit and complete a node without a jump
            // theoretically this does mean that there might be redundant increments
            // but I don't think it will matter because a jump always prevents
            // the extra increment being reached
            // a bit inelegant to do it this way but the codegen visitor doesn't exit a node
            // will do for now, shouldn't be hard to refactor this later
            string? track = TrackingNodes.Contains(CurrentNode.Name) ? Yarn.Library.GenerateUniqueVisitedVariableForNode(CurrentNode.Name) : null;
            if (track != null)
            {
                CodeGenerationVisitor.GenerateTrackingCode(this, track);
            }

            // We have exited the body; emit a 'stop' opcode here.
            Compiler.Emit(this.CurrentNode, this.CurrentNodeDebugInfo, context.Stop.Line - 1, 0, OpCode.Stop);
        }

    }
}
