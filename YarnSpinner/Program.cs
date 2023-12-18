// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using static Yarn.Instruction.Types;

    /// <summary>
    /// A compiled Yarn program.
    /// </summary>
    public partial class Program
    {
        internal const string SmartVariableNodeTag = "Yarn.SmartVariable";

        internal string DumpCode(Library? l, System.Func<string,string>? stringLookupHelper = null)
        {
            var sb = new System.Text.StringBuilder();

            foreach (var entry in Nodes)
            {
                sb.AppendLine("Node " + entry.Key + ":");

                Dictionary<int, string> labels = new Dictionary<int, string>();
                foreach (var label in entry.Value.Labels) {
                    labels[label.Value] = label.Key;
                }

                int instructionCount = 0;
                foreach (var instruction in entry.Value.Instructions)
                {
                    if (labels.TryGetValue(instructionCount, out var labelName)) {
                        sb.AppendLine(labelName + ":");
                    }
                    string instructionText;

                    instructionText = "    " + instruction.ToString(this, l, stringLookupHelper);

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

        /// <summary>
        /// Gets the collection of nodes that contain the code for evaluating a
        /// smart variable.
        /// </summary>
        internal IEnumerable<Node> SmartVariableNodes {
            get {
                foreach (var node in this.Nodes.Values) {
                    if (node.Tags.Contains(SmartVariableNodeTag)) {
                        yield return node;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to fetch a value for a variable named <paramref
        /// name="variableName"/> from this program's collection of initial
        /// values.
        /// </summary>
        /// <typeparam name="T">The type of variable to retrieve.</typeparam>
        /// <param name="variableName">The name of the variable to retrieve a
        /// value for.</param>
        /// <param name="result">On return, contains the value of the variable,
        /// or the default value of <typeparamref name="T"/> if not known.
        /// Depending on what <typeparamref name="T"/> is, this value may be
        /// <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if an initial value for <paramref
        /// name="variableName"/> was found; <see langword="false"/>
        /// otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown the stored
        /// initial value found for <paramref name="variableName"/> is not known
        /// to this version of Yarn Spinner.</exception>
        /// <exception cref="InvalidCastException">Thrown when the initial value
        /// found for <paramref name="variableName"/> cannot be cast or
        /// converted to <typeparamref name="T"/>.</exception>
        public bool TryGetInitialValue<T>(string variableName, out T result)
        {
            // Attempt to fetch it from the program's initial values.
            if (this.InitialValues.ContainsKey(variableName) == false)
            {
                // This variable isn't known to this program.
                result = default!;
                return false;
            }
            var initialValue = this.InitialValues[variableName];
            try
            {
                object convertObject;
                switch (initialValue.ValueCase)
                {
                    case Operand.ValueOneofCase.StringValue:
                        convertObject = initialValue.StringValue;
                        break;
                    case Operand.ValueOneofCase.BoolValue:
                        convertObject = initialValue.BoolValue;
                        break;
                    case Operand.ValueOneofCase.FloatValue:
                        convertObject = initialValue.FloatValue;
                        break;
                    default:
                        throw new InvalidOperationException($"Internal error: invalid value type {initialValue.ValueCase}");
                }
                if (typeof(T).IsInterface)
                {
                    // T is an interface, so we can't directly convert to it.
                    // Instead, attempt to cast to whatever type T is. If this
                    // is invalid, we'll throw an InvalidCastException (which we
                    // catch below.).
                    result = (T)convertObject;
                }
                else
                {
                    // This is a concrete type. Use Convert to convert to that
                    // type, if we're able.
                    result = (T)Convert.ChangeType(convertObject, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
                return true;
            }
            catch (InvalidCastException e)
            {
                throw new InvalidCastException($"Can't fetch variable {variableName} (a {initialValue.ValueCase}) as {typeof(T)}", e);
            }
        }

        /// <summary>
        /// Gets a value indicating the kind of variable <paramref name="name"/>
        /// represents.
        /// </summary>
        /// <param name="name">The name of a variable.</param>
        /// <returns>The kind of variable that <paramref name="name"/>
        /// represents.</returns>
        public VariableKind GetVariableKind(string name)
        {
            // If 'name' has an initial value, it is a stored variable
            if (this.InitialValues.ContainsKey(name)) {
                return VariableKind.Stored;
            }
            // If 'name' is the name of a node in our smart variable nodes, then 
            foreach (var node in this.SmartVariableNodes) {
                if (node.Name == name) {
                    return VariableKind.Smart;
                }
            }
            // We don't know what kind it is.
            return VariableKind.Unknown;
        }
    }

    /// <summary>
    /// An instruction in a Yarn Program.
    /// </summary>
    public partial class Instruction
    {
        internal string ToString(Program p, Library? l, System.Func<string,string>? stringLookupHelper = null)
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
                    if (l == null) {
                        pops = -1;
                        pushes = -1;
                        break;
                    }
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

                    string actualString = Operands[0].StringValue;

                    if (stringLookupHelper != null) {
                        actualString = stringLookupHelper(actualString);
                    }

                    // Add the string for this option, if it has one
                    if (actualString != null)
                    {
                        comment += string.Format(CultureInfo.InvariantCulture, "\"{0}\"", actualString);
                    }

                    break;

            }

            if (comment != "")
            {
                comment = "; " + comment;
            }


            var operandsAsStrings = new List<string>();
            foreach (var op in Operands) {
                operandsAsStrings.Add(op.AsString);
            }
            

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0,-15} {1,-40} {2, -10}",
                Opcode.ToString(),
                string.Join(" ", operandsAsStrings),
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

    public partial class Operand {
        internal string AsString
        {
            get
            {
                switch (this.ValueCase)
                {
                    case ValueOneofCase.StringValue:
                        return this.StringValue;
                    case ValueOneofCase.BoolValue:
                        return this.BoolValue.ToString();
                    case ValueOneofCase.FloatValue:
                        return this.FloatValue.ToString(CultureInfo.InvariantCulture);
                    default:
                        return "<unknown>";
                }
            }

        }
    }
}
