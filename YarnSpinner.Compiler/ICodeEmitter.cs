namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using Antlr4.Runtime;

    internal interface ICodeEmitter
    {
        Node? CurrentNode { get; }
        NodeDebugInfo? CurrentNodeDebugInfo { get; }

        IDictionary<string, Declaration> VariableDeclarations { get; }

        /// <summary>
        /// Creates a new instruction, and appends it to the current node in the
        /// <see cref="Program"/>.
        /// </summary>
        /// <remarks>
        /// Called by instances of <see
        /// cref="CodeGenerationVisitor"/> while walking the parse tree.
        /// </remarks>
        /// <param name="instruction">The instruction to add.</param>
        /// <param name="startToken">The first token in the expression or
        /// statement that was responsible for emitting this
        /// instruction.</param>
        void Emit(IToken startToken, Instruction instruction);

        /// <summary>
        /// Adds instructions to the current node in the <see cref="Program"/>.
        /// </summary>
        /// <remarks>
        /// Called by instances of <see cref="CodeGenerationVisitor"/> while
        /// walking the parse tree.
        /// </remarks>
        /// <param name="instructions">The instructions to add.</param>
        /// <param name="startToken">The first token in the expression or
        /// statement that was responsible for emitting this
        /// instruction.</param>
        void Emit(IToken startToken, params Instruction[] instructions);

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
        /// <param name="instruction">The instruction to add.</param>
        void Emit(Instruction instruction);
    }
}
