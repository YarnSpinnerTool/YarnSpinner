using System;
using System.Collections.Generic;

namespace Yarn
{
    internal class VirtualMachine
    {

        internal class State {

            /// The name of the node that we're currently in
            public string currentNodeName;

            /// The instruction number in the current node
            public int programCounter = 0;

            /// List of options, where each option = <string id, destination node>
            public List<KeyValuePair<string,string>> currentOptions = new List<KeyValuePair<string, string>>();

            /// The value stack
            private Stack<Value> stack = new Stack<Value>();

            /// Methods for working with the stack
            public void PushValue(object o) {
                if( o is Value ) {
                    stack.Push(o as Value);
                } else {
                    stack.Push (new Value(o));
                }
            }

            /// Pop a value from the stack
            public Value PopValue() {
                return stack.Pop ();
            }

            /// Peek at a value from the stack
            public Value PeekValue() {
                return stack.Peek ();
            }

            /// Clear the stack
            public void ClearStack() {
                stack.Clear ();
            }
        }

        internal static class SpecialVariables {
            public const string ShuffleOptions = "$Yarn.ShuffleOptions";
        }

        internal VirtualMachine (Dialogue d, Program p)
        {
            program = p;
            dialogue = d;
            state = new State ();
        }

        /// Reset the state of the VM
        void ResetState() {
            state = new State();
        }

        public delegate void LineHandler(Dialogue.LineResult line);
        public delegate void OptionsHandler(Dialogue.OptionSetResult options);
        public delegate void CommandHandler(Dialogue.CommandResult command);
        public delegate void NodeCompleteHandler(Dialogue.NodeCompleteResult complete);

        public LineHandler lineHandler;
        public OptionsHandler optionsHandler;
        public CommandHandler commandHandler;
        public NodeCompleteHandler nodeCompleteHandler;

        private Dialogue dialogue;

        private Program program;
        private State state = new State();

        private Random random = new Random();

        public string currentNodeName {
            get {
                return state.currentNodeName;
            }
        }

        public enum ExecutionState {
            /** Stopped */
            Stopped,
            /** Waiting on option selection */
            WaitingOnOptionSelection,
            /** Running */
            Running
        }

        private ExecutionState _executionState;
        public ExecutionState executionState {
            get {
                return _executionState;
            }
            private set {
                _executionState = value;
                if (_executionState == ExecutionState.Stopped) {
                    ResetState ();
                }
            }
        }

        Node currentNode;

        public bool SetNode(string nodeName) {
            if (program.nodes.ContainsKey(nodeName) == false) {

                var error = "No node named " + nodeName;
                dialogue.LogErrorMessage(error);
                executionState = ExecutionState.Stopped;
                return false;
            }

            dialogue.LogDebugMessage ("Running node " + nodeName);

            // Clear the special variables
            dialogue.continuity.SetValue(SpecialVariables.ShuffleOptions, new Value(false));

            currentNode = program.nodes [nodeName];
            ResetState ();
            state.currentNodeName = nodeName;

            return true;
        }

        public void Stop() {
            executionState = ExecutionState.Stopped;
        }

        /// Executes the next instruction in the current node.
        internal void RunNext() {

            if (executionState == ExecutionState.WaitingOnOptionSelection) {
                dialogue.LogErrorMessage ("Cannot continue running dialogue. Still waiting on option selection.");
                executionState = ExecutionState.Stopped;
                return;
            }

            if (executionState == ExecutionState.Stopped)
                executionState = ExecutionState.Running;

            Instruction currentInstruction = currentNode.instructions [state.programCounter];

            RunInstruction (currentInstruction);

            state.programCounter++;

            if (state.programCounter >= currentNode.instructions.Count) {
                executionState = ExecutionState.Stopped;
                nodeCompleteHandler(new Dialogue.NodeCompleteResult(null));
                dialogue.LogDebugMessage ("Run complete.");
            }

        }

        /// Looks up the instruction number for a named label in the current node.
        internal int FindInstructionPointForLabel(string labelName) {

            if (currentNode.labels.ContainsKey(labelName) == false) {
                // Couldn't find the node..
                throw new IndexOutOfRangeException ("Unknown label " +
                    labelName + " in node " + state.currentNodeName);
            }

            return currentNode.labels [labelName];

        }

        internal void RunInstruction(Instruction i) {
            switch (i.operation) {
            case ByteCode.Label:
                /// - Label
                /** No-op, used as a destination for JumpTo and Jump.
                 */
                break;
            case ByteCode.JumpTo:
                /// - JumpTo
                /** Jumps to a named label
                 */
                state.programCounter = FindInstructionPointForLabel ((string)i.operandA);

                break;
            case ByteCode.RunLine:
                /// - RunLine
                /** Looks up a string from the string table and
                 *  passes it to the client as a line
                 */
                var lineText = program.GetString ((string)i.operandA);

                if (lineText == null) {
                    dialogue.LogErrorMessage("No loaded string table includes line " + i.operandA);
                    break;
                }

                lineHandler (new Dialogue.LineResult (lineText));

                break;
            case ByteCode.RunCommand:
                /// - RunCommand
                /** Passes a string to the client as a custom command
                 */
                commandHandler (
                    new Dialogue.CommandResult ((string)i.operandA)
                );

                break;
            case ByteCode.PushString:
                /// - PushString
                /** Pushes a string value onto the stack. The operand is an index into
                 *  the string table, so that's looked up first.
                 */
                state.PushValue (program.GetString ((string)i.operandA));

                break;
            case ByteCode.PushNumber:
                /// - PushNumber
                /** Pushes a number onto the stack.
                 */
                state.PushValue (Convert.ToSingle(i.operandA));

                break;
            case ByteCode.PushBool:
                /// - PushBool
                /** Pushes a boolean value onto the stack.
                 */
                state.PushValue (Convert.ToBoolean(i.operandA));

                break;
            case ByteCode.PushNull:
                /// - PushNull
                /** Pushes a null value onto the stack.
                 */
                state.PushValue (Value.NULL);

                break;
            case ByteCode.JumpIfFalse:
                /// - JumpIfFalse
                /** Jumps to a named label if the value on the top of the stack
                 *  evaluates to the boolean value 'false'.
                 */
                if (state.PeekValue ().AsBool == false) {
                    state.programCounter = FindInstructionPointForLabel ((string)i.operandA);
                }
                break;

            case ByteCode.Jump:
                /// - Jump
                /** Jumps to a label whose name is on the stack.
                 */
                var jumpDestination = state.PeekValue ().AsString;
                state.programCounter = FindInstructionPointForLabel (jumpDestination);

                break;

            case ByteCode.Pop:
                /// - Pop
                /** Pops a value from the stack.
                 */
                state.PopValue ();
                break;

            case ByteCode.CallFunc:
                /// - CallFunc
                /** Call a function, whose parameters are expected to
                 *  be on the stack. Pushes the function's return value,
                 *  if it returns one.
                 */
                var functionName = (string)i.operandA;

                var function = dialogue.library.GetFunction (functionName);
                {

                    var paramCount = function.paramCount;

                    // If this function takes "-1" parameters, it is variadic.
                    // Expect the compiler to have placed the number of parameters
                    // actually passed at the top of the stack.
                    if (paramCount == -1) {
                        paramCount = (int)state.PopValue ().AsNumber;
                    }

                    Value result;
                    if (paramCount == 0) {
                        result = function.Invoke();
                    } else {
                        // Get the parameters, which were pushed in reverse
                        Value[] parameters = new Value[paramCount];
                        for (int param = paramCount - 1; param >= 0; param--) {
                            parameters [param] = state.PopValue ();
                        }

                        // Invoke the function
                        result = function.InvokeWithArray (parameters);
                    }

                    // If the function returns a value, push it
                    if (function.returnsValue) {
                        state.PushValue (result);
                    }
                }

                break;
            case ByteCode.PushVariable:
                /// - PushVariable
                /** Get the contents of a variable, push that onto the stack.
                 */
                var variableName = (string)i.operandA;
                var loadedValue = dialogue.continuity.GetValue (variableName);
                state.PushValue (loadedValue);

                break;
            case ByteCode.StoreVariable:
                /// - StoreVariable
                /** Store the top value on the stack in a variable.
                 */
                var topValue = state.PeekValue ();
                var destinationVariableName = (string)i.operandA;
                dialogue.continuity.SetValue (destinationVariableName, topValue);

                break;
            case ByteCode.Stop:
                /// - Stop
                /** Immediately stop execution, and report that fact.
                 */
                nodeCompleteHandler (new Dialogue.NodeCompleteResult (null));
                executionState = ExecutionState.Stopped;

                break;
            case ByteCode.RunNode:
                /// - RunNode
                /** Run a node
                 */
                string nodeName;

                if (string.IsNullOrEmpty((string) i.operandA)) {
                    // Get a string from the stack, and jump to a node with that name.
                     nodeName = state.PeekValue ().AsString;
                } else {
                    // jump straight to the node
                    nodeName = (string)i.operandA;
                }

                nodeCompleteHandler (new Dialogue.NodeCompleteResult (nodeName));
                SetNode (nodeName);

                break;
            case ByteCode.AddOption:
                /// - AddOption
                /** Add an option to the current state.
                 */
                state.currentOptions.Add (new KeyValuePair<string, string> ((string)i.operandA, (string)i.operandB));

                break;
            case ByteCode.ShowOptions:
                /// - ShowOptions
                /** If we have no options to show, immediately stop.
                 */
                if (state.currentOptions.Count == 0) {
                    nodeCompleteHandler(new Dialogue.NodeCompleteResult(null));
                    executionState = ExecutionState.Stopped;
                    break;
                }

                /** If we have a single option, and it has no label, select it immediately and continue execution
                 */
                if (state.currentOptions.Count == 1 && state.currentOptions[0].Key == null) {
                    var destinationNode = state.currentOptions[0].Value;
                    state.PushValue(destinationNode);
                    state.currentOptions.Clear();
                    break;
                }

                if (dialogue.continuity.GetValue(SpecialVariables.ShuffleOptions).AsBool) {
                    // Shuffle the dialog options if needed
                    var n = state.currentOptions.Count;
                    for (int opt1 = 0; opt1 < n; opt1++) {
                        int opt2 = opt1 + (int)(random.NextDouble () * (n - opt1)); // r.Next(0, state.currentOptions.Count-1);
                        var temp = state.currentOptions [opt2];
                        state.currentOptions [opt2] = state.currentOptions [opt1];
                        state.currentOptions [opt1] = temp;
                    }
                }

                // Otherwise, present the list of options to the user and let them pick
                var optionStrings = new List<string> ();

                foreach (var option in state.currentOptions) {
                    optionStrings.Add (program.GetString (option.Key));
                }

                // We can't continue until our client tell us which option to pick
                executionState = ExecutionState.WaitingOnOptionSelection;

                // Pass the options set to the client, as well as a delegate for them to call when the
                // user has made a selection
                optionsHandler (new Dialogue.OptionSetResult (optionStrings, delegate (int selectedOption) {

                    // we now know what number option was selected; push the corresponding node name
                    // to the stack
                    var destinationNode = state.currentOptions[selectedOption].Value;
                    state.PushValue(destinationNode);

                    // We no longer need the accumulated list of options; clear it so that it's
                    // ready for the next one
                    state.currentOptions.Clear();

                    // We can now also keep running
                    executionState = ExecutionState.Running;

                }));

                break;
            default:
                /// - default
                /** Whoa, no idea what bytecode this is. Stop the program
                 * and throw an exception.
                */
                executionState = ExecutionState.Stopped;
                throw new ArgumentOutOfRangeException ();
            }
        }

    }
}

