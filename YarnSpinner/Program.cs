// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Globalization;
    using static Yarn.Instruction.Types;
    
    /// <summary>
    /// A compiled Yarn program.
    /// </summary>
    public partial class Program
    {
        internal string DumpCode(Library l)
        {
            var sb = new System.Text.StringBuilder();

            foreach (var entry in Nodes)
            {
                sb.AppendLine("Node " + entry.Key + ":");

                int instructionCount = 0;
                foreach (var instruction in entry.Value.Instructions)
                {
                    string instructionText;

                    instructionText = "    " + instruction.ToString(this, l);

                    string preface;

                    if (instructionCount % 5 == 0 || instructionCount == entry.Value.Instructions.Count - 1)
                    {
                        preface = string.Format(CultureInfo.InvariantCulture, "{0,6}   ", instructionCount);
                    }
                    else
                    {
                        preface = string.Format(CultureInfo.InvariantCulture, "{0,6}   ", " ");
                    }

                    sb.AppendLine(preface + instructionText);

                    instructionCount++;
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Identifies and returns a list of all line and option IDs inside the node.
        /// </summary>
        /// <param name="nodeName">The name of the node whos line IDs you covet.</param>
        /// <returns>The line IDs of all lines and options inside the node, or null if <paramref name="nodeName"/> doesn't exist in the program.</returns>
        public List<string> LineIDsForNode(string nodeName)
        {
            // if there is no node matching the name bail out
            var node = this.Nodes[nodeName];
            if (node == null)
            {
                return null;
            }

            // Create a list; we will never have more lines and options
            // than total instructions, so that's a decent capacity for
            // the list (TODO: maybe this list could be reused to save
            // on allocations?)
            var stringIDs = new List<string>(node.Instructions.Count);

            // Loop over every instruction and find the ones that run a
            // line or add an option; these are the two instructions
            // that will signal a line can appear to the player
            foreach (var instruction in node.Instructions)
            {
                if (instruction.Opcode == OpCode.RunLine || instruction.Opcode == OpCode.AddOption)
                {
                    // Both RunLine and AddOption have the string ID
                    // they want to show as their first operand, so
                    // store that
                    stringIDs.Add(instruction.Operands[0].StringValue);
                }
            }
            return stringIDs;
        }

        internal IEnumerable<string> GetTagsForNode(string nodeName)
        {
            return Nodes[nodeName].Tags;
        }

        // TODO: this behaviour belongs in the VM as a "load additional program" feature, not in the Program data object

		/// <summary>
		/// Creates a new Program by merging multiple Programs together.
		/// </summary>
		/// <remarks>
		/// The new program will contain every node from every input
		/// program.
		/// </remarks>
		/// <param name="programs">The Programs to combine
		/// together.</param>
		/// <returns>The new, combined program.</returns>
		/// <throws cref="ArgumentException">Thrown when no Programs are
		/// provided as parameters.</throws>
		/// <throws cref="InvalidOperationException">Thrown when more than
		/// one Program contains a node of the same name.</throws>
        public static Program Combine(params Program[] programs)
        {
            if (programs == null)
            {
                throw new ArgumentNullException("At least one program must be provided");
            }
            if (programs.Length == 0)
            {
                throw new ArgumentException(nameof(programs), "At least one program must be provided.");
            }

            var output = new Program();

            foreach (var otherProgram in programs)
            {
                foreach (var otherNodeName in otherProgram.Nodes)
                {

                    if (output.Nodes.ContainsKey(otherNodeName.Key))
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "This program already contains a node named {0}", otherNodeName.Key));
                    }

                    output.Nodes[otherNodeName.Key] = otherNodeName.Value.Clone();
                }

                output.InitialValues.Add(otherProgram.InitialValues);
            }
            return output;
        }
    }

    /// <summary>
    /// An instruction in a Yarn Program.
    /// </summary>
    public partial class Instruction
    {
        internal string ToString(Program p, Library l)
        {
            // Generate a comment, if the instruction warrants it
            string comment = "";

            // Stack manipulation comments
            var pops = 0;
            var pushes = 0;

            switch (Opcode)
            {

                // These operations all push a single value to the stack
                case OpCode.PushBool:
                case OpCode.PushNull:
                case OpCode.PushFloat:
                case OpCode.PushString:
                case OpCode.PushVariable:
                case OpCode.ShowOptions:
                    pushes = 1;
                    break;

                // Functions pop 0 or more values, and pop 0 or 1
                case OpCode.CallFunc:
                    var function = l.GetFunction(Operands[0].StringValue);

                    pops = function.Method.GetParameters().Length;

                    var returnsValue = function.Method.ReturnType != typeof(void);

                    if (returnsValue)
                    {
                        pushes = 1;
                    }

                    break;

                // Pop always pops a single value
                case OpCode.Pop:
                    pops = 1;
                    break;

                // Switching to a different node will always clear the stack
                case OpCode.RunNode:
                    comment += "Clears stack";
                    break;
            }

            // If we had any pushes or pops, report them
            if (pops > 0 && pushes > 0)
            {
                comment += string.Format(CultureInfo.InvariantCulture, "Pops {0}, Pushes {1}", pops, pushes);
            }
            else if (pops > 0)
            {
                comment += string.Format(CultureInfo.InvariantCulture, "Pops {0}", pops);
            }
            else if (pushes > 0)
            {
                comment += string.Format(CultureInfo.InvariantCulture, "Pushes {0}", pushes);
            }

            // String lookup comments
            switch (Opcode)
            {
                case OpCode.PushString:
                case OpCode.RunLine:
                case OpCode.AddOption:

                    // Add the string for this option, if it has one
                    if (Operands[0].StringValue != "")
                    {
                        comment += string.Format(CultureInfo.InvariantCulture, "\"{0}\"", Operands[0].StringValue);
                    }

                    break;

            }

            if (comment != "")
            {
                comment = "; " + comment;
            }

            string opAString = Operands.Count > 0 ? Operands[0].ToString() : "";
            string opBString = Operands.Count > 1 ? Operands[1].ToString() : "";

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0,-15} {1,-10} {2,-10} {3, -10}",
                Opcode.ToString(),
                opAString,
                opBString,
                comment);
        }
    }


    /// <summary>
    /// A node of Yarn script, contained within a <see cref="Program"/>, and
    /// containing <see cref="Instruction"/>s.
    /// </summary>
    public partial class Node
    {

    }
}
