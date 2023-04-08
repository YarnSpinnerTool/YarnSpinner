namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using Antlr4.Runtime;
    using static Yarn.Instruction.Types;

    internal interface ICodeEmitter
    {
        Node CurrentNode { get; }
        IDictionary<string, Declaration> VariableDeclarations { get; }

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
        void Emit(OpCode code, IToken startToken, params Operand[] operands);

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
        void Emit(OpCode code, params Operand[] operands);

        /// <summary>
        /// Generates a unique label name to use in the program.
        /// </summary>
        /// <param name="commentary">Any additional text to append to the
        /// end of the label.</param>
        /// <returns>The new label name.</returns>
        string RegisterLabel(string commentary = null);

        void AddLabel(string name, int position);
    }
}
