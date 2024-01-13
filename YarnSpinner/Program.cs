// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using NodeLabelsCollection = System.Collections.Generic.IReadOnlyDictionary<int, string>;

    internal interface ICodeDumpHelper {
        public string GetStringForKey(string key);
        public string GetDescriptionForVariable(string variableName);
        public NodeLabelsCollection GetLabelsForNode(string node);
    }

    /// <summary>
    /// A compiled Yarn program.
    /// </summary>
    public partial class Program
    {
        internal const string SmartVariableNodeTag = "Yarn.SmartVariable";

        internal string DumpCode(Library? library, ICodeDumpHelper? helper)
        {
            var sb = new System.Text.StringBuilder();

            foreach (var entry in Nodes)
            {
                sb.AppendLine("Node " + entry.Key + ":");

                var nodeLabels = helper?.GetLabelsForNode(entry.Key);

                int instructionCount = 0;
                foreach (var instruction in entry.Value.Instructions)
                {
                    if (nodeLabels?.TryGetValue(instructionCount, out var labelName) ?? false) {
                        sb.AppendLine(labelName + ":");
                    }
                    string instructionText;

                    instructionText = "    " + instruction.ToString(this, entry.Value, library, helper);

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
        public List<string>? LineIDsForNode(string nodeName)
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
                // Both RunLine and AddOption have the string ID they want to
                // show, so store that
                if (
                    instruction.InstructionTypeCase == Instruction.InstructionTypeOneofCase.RunLine
)
                {
                    stringIDs.Add(instruction.RunLine.LineID);
                }
                else if (instruction.InstructionTypeCase == Instruction.InstructionTypeOneofCase.AddOption)
                {
                    
                    stringIDs.Add(instruction.AddOption.LineID);
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
        internal IEnumerable<Node> SmartVariableNodes
        {
            get
            {
                foreach (var node in this.Nodes.Values)
                {
                    foreach (var tag in node.Tags)
                    {
                        if (tag.Equals(SmartVariableNodeTag, StringComparison.Ordinal))
                        {
                            yield return node;
                        }
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
        partial void OnConstruction() {
            
        }
        internal string ToString(Program p, Node? containingNode, Library? library, ICodeDumpHelper? helper)
        {
            // Generate a comment, if the instruction warrants it
            List<string> comments = new List<string>();

            // Stack manipulation comments
            var pops = 0;
            var pushes = 0;

            switch (InstructionTypeCase)
            {

                // These operations all push a single value to the stack
                case InstructionTypeOneofCase.PushBool:
                case InstructionTypeOneofCase.PushFloat:
                case InstructionTypeOneofCase.PushString:
                case InstructionTypeOneofCase.PushVariable:
                case InstructionTypeOneofCase.ShowOptions:
                    pushes = 1;
                    break;

                // Functions pop 0 or more values, and pop 0 or 1
                case InstructionTypeOneofCase.CallFunc:
                    if (library == null) {
                        pops = -1;
                        pushes = -1;
                        break;
                    }
                    
                    var function = library.GetFunction(this.CallFunc.FunctionName);

                    pops = function.Method.GetParameters().Length;

                    var returnsValue = function.Method.ReturnType != typeof(void);

                    if (returnsValue)
                    {
                        pushes = 1;
                    }

                    break;

                // Pop always pops a single value
                case InstructionTypeOneofCase.Pop:
                    pops = 1;
                    break;

                // Switching to a different node will always clear the stack
                case InstructionTypeOneofCase.RunNode:
                    comments.Add("Clears stack");
                    break;
            }

            // If we had any pushes or pops, report them
            if (pops > 0 && pushes > 0)
            {
                comments.Add(string.Format(CultureInfo.InvariantCulture, "Pops {0}, Pushes {1}", pops, pushes));
            }
            else if (pops > 0)
            {
                comments.Add(string.Format(CultureInfo.InvariantCulture, "Pops {0}", pops));
            }
            else if (pushes > 0)
            {
                comments.Add(string.Format(CultureInfo.InvariantCulture, "Pushes {0}", pushes));
            }

            // String lookup comments
            switch (InstructionTypeCase)
            {
                case InstructionTypeOneofCase.PushString:
                    comments.Add($@"""{PushString.Value}""");
                    break;
                case InstructionTypeOneofCase.RunLine:
                case InstructionTypeOneofCase.AddOption:

                    string actualString;

                    if (InstructionTypeCase == InstructionTypeOneofCase.RunLine) {
                        actualString = RunLine.LineID;
                    } else if (InstructionTypeCase == InstructionTypeOneofCase.AddOption) {
                        actualString = AddOption.LineID;
                    } else {
                        throw new InvalidOperationException();
                    }

                    if (helper != null) {
                        actualString = helper.GetStringForKey(actualString);
                    }

                    // Add the string for this option, if it has one
                    if (actualString != null)
                    {
                        comments.Add(string.Format(CultureInfo.InvariantCulture, "\"{0}\"", actualString));
                    }

                    break;
                
                case InstructionTypeOneofCase.PushVariable:
                    comments.Add(helper?.GetDescriptionForVariable(PushVariable.VariableName) ?? "<no variable info available>");
                    break;
                case InstructionTypeOneofCase.StoreVariable:
                    comments.Add(helper?.GetDescriptionForVariable(StoreVariable.VariableName) ?? "<no variable info available>");
                    break;

            }

            
            var operands = new List<object>();

            string GetLabel(int instruction)
            {
                if (containingNode == null) { 
                    return instruction.ToString(CultureInfo.InvariantCulture);
                }

                return helper?.GetLabelsForNode(containingNode.Name)[instruction] ?? instruction.ToString(CultureInfo.InvariantCulture);
            }

            switch (this.InstructionTypeCase)
            {
                case InstructionTypeOneofCase.JumpTo:
                    operands.Add(GetLabel(this.JumpTo.Destination));
                    break;
                case InstructionTypeOneofCase.PeekAndJump:
                    break;
                case InstructionTypeOneofCase.RunLine:
                    operands.Add(this.RunLine.LineID);
                    break;
                case InstructionTypeOneofCase.RunCommand:
                    operands.Add(this.RunCommand.CommandText);
                    break;
                case InstructionTypeOneofCase.AddOption:
                    operands.Add(this.AddOption.LineID);
                    break;
                case InstructionTypeOneofCase.ShowOptions:
                    break;
                case InstructionTypeOneofCase.PushString:
                    operands.Add(this.PushString.Value);
                    break;
                case InstructionTypeOneofCase.PushFloat:
                    operands.Add(this.PushFloat.Value);
                    break;
                case InstructionTypeOneofCase.PushBool:
                    operands.Add(this.PushBool.Value);
                    break;
                case InstructionTypeOneofCase.JumpIfFalse:
                    operands.Add(GetLabel(this.JumpIfFalse.Destination));
                    break;
                case InstructionTypeOneofCase.Pop:
                    break;
                case InstructionTypeOneofCase.CallFunc:
                    operands.Add(this.CallFunc.FunctionName);
                    break;
                case InstructionTypeOneofCase.PushVariable:
                    operands.Add(this.PushVariable.VariableName);
                    break;
                case InstructionTypeOneofCase.StoreVariable:
                    operands.Add(this.StoreVariable.VariableName);
                    break;
                case InstructionTypeOneofCase.Stop:
                    break;
                case InstructionTypeOneofCase.RunNode:
                    operands.Add(this.RunNode.NodeName);
                    break;
                case InstructionTypeOneofCase.PeekAndRunNode:
                    break;
                case InstructionTypeOneofCase.DetourToNode:
                    operands.Add(this.DetourToNode.NodeName);
                    break;
            }

            string operandText = string.Join(", ", operands);
            string commentText = string.Join(", ", comments);
            if (comments.Count > 0) {
                commentText = "; " + commentText;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0,-15} {1,-40} {2, -10}",
                InstructionTypeCase.ToString(),
                operandText,
                commentText);
        }

        internal bool HasDestination {
            get {
                switch (this.InstructionTypeCase) {
                    case InstructionTypeOneofCase.AddOption:
                    case InstructionTypeOneofCase.JumpTo:
                    case InstructionTypeOneofCase.JumpIfFalse:
                    case InstructionTypeOneofCase.PushFloat:
                        return true;
                    default:
                        return false;
                }
            }
        }

        internal int Destination {
            get {
                switch (this.InstructionTypeCase) {
                    case InstructionTypeOneofCase.AddOption:
                        return this.AddOption.Destination;
                    case InstructionTypeOneofCase.JumpTo:
                        return this.JumpTo.Destination;
                    case InstructionTypeOneofCase.JumpIfFalse:
                        return this.JumpIfFalse.Destination;
                    case InstructionTypeOneofCase.PushFloat:
                        return (int)this.PushFloat.Value;
                    default:
                        throw new ArgumentOutOfRangeException($"Instruction {this} does not have a Destination");
                }
            }
            
            set {
                switch (this.InstructionTypeCase) {
                    case InstructionTypeOneofCase.AddOption:
                        this.AddOption.Destination = value;
                        break;
                    case InstructionTypeOneofCase.JumpTo:
                        this.JumpTo.Destination = value;
                        break;
                    case InstructionTypeOneofCase.JumpIfFalse:
                        this.JumpIfFalse.Destination = value;
                        break;
                    case InstructionTypeOneofCase.PushFloat:
                        this.PushFloat.Value = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Instruction {this} does not have a Destination");
                }

            }
        }
    }


    /// <summary>
    /// A node of Yarn script, contained within a <see cref="Program"/>, and
    /// containing <see cref="Instruction"/>s.
    /// </summary>
    public partial class Node
    {
        /// <summary>
        /// Gets the collection of tags defined for this node, if any. If no
        /// tags are defined, returns an empty collection.
        /// </summary>
        public IEnumerable<string> Tags {
            get
            {
                foreach (var header in this.Headers)
                {
                    if (header.Key == "tags")
                    {
                        return header.Value.Split(' ');
                    }
                }
                return Array.Empty<string>();
            }
        }
    }


}
