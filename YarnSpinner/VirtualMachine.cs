// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

using Yarn.Saliency;

namespace Yarn
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A value used by an Instruction.
    /// </summary>
    public partial class Operand
    {
        /// <summary>
        /// Convenience constructor for the Operand type.
        /// </summary>
        /// <remarks>
        /// so that we don't need to have two separate steps for creating and then preparing the Operand
        /// </remarks>
        /// <param name="value">The boolean value to be made into the operand</param>
        public Operand(bool value) : base()
        {
            this.BoolValue = value;
        }

        /// <summary>
        /// Convenience constructor for the Operand type.
        /// </summary>
        /// <remarks>
        /// so that we don't need to have two separate steps for creating and then preparing the Operand
        /// </remarks>
        /// <param name="value">The string value to be made into the operand</param>
        public Operand(string value) : base()
        {
            this.StringValue = value;
        }

        /// <summary>
        /// Convenience constructor for the Operand type.
        /// </summary>
        /// <remarks>
        /// so that we don't need to have two separate steps for creating and then preparing the Operand
        /// </remarks>
        /// <param name="value">The float value to be made into the operand</param>
        public Operand(float value) : base()
        {
            this.FloatValue = value;
        }
    }

    /// <summary>
    /// Lists the available operators that can be used with Yarn values.
    /// </summary>
    internal enum Operator
    {
        /// <summary>A unary operator that returns its input.</summary>
        None,

        /// <summary>A binary operator that represents equality.</summary>
        EqualTo,

        /// <summary>A binary operator that represents a value being
        /// greater than another.</summary>
        GreaterThan,

        /// <summary>A binary operator that represents a value being
        /// greater than or equal to another.</summary>
        GreaterThanOrEqualTo,

        /// <summary>A binary operator that represents a value being less
        /// than another.</summary>
        LessThan,

        /// <summary>A binary operator that represents a value being less
        /// than or equal to another.</summary>
        LessThanOrEqualTo,

        /// <summary>A binary operator that represents
        /// inequality.</summary>
        NotEqualTo,

        /// <summary>A binary operator that represents a logical
        /// or.</summary>
        Or,

        /// <summary>A binary operator that represents a logical
        /// and.</summary>
        And,

        /// <summary>A binary operator that represents a logical exclusive
        /// or.</summary>
        Xor,

        /// <summary>A binary operator that represents a logical
        /// not.</summary>
        Not,

        /// <summary>A unary operator that represents negation.</summary>
        UnaryMinus,

        /// <summary>A binary operator that represents addition.</summary>
        Add,

        /// <summary>A binary operator that represents
        /// subtraction.</summary>
        Minus,

        /// <summary>A binary operator that represents
        /// multiplication.</summary>
        Multiply,

        /// <summary>A binary operator that represents division.</summary>
        Divide,

        /// <summary>A binary operator that represents the remainder
        /// operation.</summary>
        Modulo,
    }

    /// <summary>
    /// Contains methods for evaluating the value of smart variables
    /// </summary>
    public interface ISmartVariableEvaluator {
        /// <summary>
        /// Evaluate the value of a smart variable named <paramref
        /// name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned value.</typeparam>
        /// <param name="name">The name of the variable.</param>
        /// <param name="result">On return, contains the returned value of the
        /// smart variable, or the <see langword="default"/> value of
        /// <typeparamref name="T"/> if a smart variable named <paramref
        /// name="name"/> could not be found or its value could not be returned
        /// as type <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> if the smart variable was evaluated,
        /// <see langword="false"/> otherwise.</returns>
        bool TryGetSmartVariable<T>(string name, out T result);
    }

    internal class VirtualMachine
    {
        internal class State
        {
            /// <summary>The name of the node that we're currently
            /// in.</summary>
            public string? currentNodeName;

            /// <summary>The instruction number in the current
            /// node.</summary>
            public int programCounter = 0;

            /// <summary>The current list of options that will be delivered
            /// when the next RunOption instruction is
            /// encountered.</summary>
            public List<PendingOption> currentOptions = new List<PendingOption>();

            /// <summary>The value stack.</summary>
            private Stack<Value> stack = new Stack<Value>();

            internal struct CallSite {
                public string nodeName;
                public int instruction;
            }
            
            private Stack<CallSite> callStack = new Stack<CallSite>();

            /// <summary>Pushes a <see cref="Value"/> object onto the
            /// stack.</summary>
            /// <param name="v">The value to push onto the stack.</param>
            public void PushValue(Value v)
            {
                stack.Push(v);
            }

            public void PushValue(string s)
            {
                stack.Push(new Value(Types.String, s));
            }

            public void PushValue(float f)
            {
                stack.Push(new Value(Types.Number, f));
            }

            public void PushValue(bool b)
            {
                stack.Push(new Value(Types.Boolean, b));
            }

            /// <summary>Removes a value from the top of the stack, and
            /// returns it.</summary>
            /// <returns>The value that was at the top of the stack when
            /// this method was called.</returns>
            public Value PopValue()
            {
                return stack.Pop();
            }

            /// <summary>Peeks at a value from the stack.</summary>
            /// <returns>The value at the top of the stack.</returns>
            public Value PeekValue()
            {
                return stack.Peek();
            }

            /// <summary>Clears the stack.</summary>
            public void ClearStack()
            {
                stack.Clear();
            }

            internal void PushCallStack()
            {
                callStack.Push(new CallSite
                {
                    nodeName = currentNodeName,
                    instruction = programCounter
                });
            }

            internal bool CanReturn => this.callStack.Count > 0;

            internal CallSite PopCallStack() {
                return callStack.Pop();
            }
        }

        internal VirtualMachine(Library library, IVariableStorage storage)
        {
            this.Library = library;
            this.VariableStorage = storage;
            state = new State();
        }

        /// Reset the state of the VM
        internal void ResetState()
        {
            state = new State();
        }

        public LineHandler? LineHandler;
        public OptionsHandler? OptionsHandler;
        public CommandHandler? CommandHandler;
        public NodeStartHandler? NodeStartHandler;
        public NodeCompleteHandler? NodeCompleteHandler;
        public DialogueCompleteHandler? DialogueCompleteHandler;
        public PrepareForLinesHandler? PrepareForLinesHandler;

        public IVariableStorage VariableStorage { get; set; }
        public Library Library { get; set; }
        public Logger? LogDebugMessage { get; set; }
        public Logger? LogErrorMessage { get; set; }

        /// <summary>
        /// The <see cref="Program"/> that this virtual machine is running.
        /// </summary>
        internal Program? Program { get; set; }

        internal State state = new State();

        public string? CurrentNodeName => state.currentNodeName;

        [Obsolete("Use CurrentNodeName")]
        public string? currentNodeName => CurrentNodeName;

        public enum ExecutionState
        {
            /// <summary>
            /// The VirtualMachine is not running a node.
            /// </summary>
            Stopped,

            /// <summary>
            /// The VirtualMachine is waiting on option selection. Call
            /// <see cref="SetSelectedOption(int)"/> before calling <see
            /// cref="Continue"/>.
            /// </summary>
            WaitingOnOptionSelection,

            /// <summary>
            /// The VirtualMachine has finished delivering content to the
            /// client game, and is waiting for <see cref="Continue"/> to
            /// be called.
            /// </summary>
            WaitingForContinue,

            /// <summary>
            /// The VirtualMachine is delivering a line, options, or a
            /// commmand to the client game.
            /// </summary>
            DeliveringContent,

            /// <summary>
            /// The VirtualMachine is in the middle of executing code.
            /// </summary>
            Running,
        }

        internal ExecutionState _executionState;
        public ExecutionState CurrentExecutionState
        {
            get
            {
                return _executionState;
            }
            private set
            {
                _executionState = value;
                if (_executionState == ExecutionState.Stopped)
                {
                    ResetState();
                }
            }
        }

        public IContentSaliencyStrategy? ContentSaliencyStrategy { get; internal set; }

        internal Node? currentNode;
        public const string AddLineGroupCandidateFunctionName = "Yarn.Internal.add_line_group_candidate";
        public const string SelectLineGroupCandidateFunctionName = "Yarn.Internal.select_line_group_candidate";

        public bool SetNode(string nodeName)
        {
            return SetNode(nodeName, clearState: true);
        }

        internal bool SetNode(string nodeName, bool clearState) {
            if (Program == null || Program.Nodes.Count == 0)
            {
                throw new DialogueException($"Cannot load node {nodeName}: No nodes have been loaded.");
            }

            if (Program.Nodes.ContainsKey(nodeName) == false)
            {
                CurrentExecutionState = ExecutionState.Stopped;
                throw new DialogueException($"No node named {nodeName} has been loaded.");
            }

            LogDebugMessage?.Invoke("Running node " + nodeName);

            currentNode = Program.Nodes[nodeName];
            
            if (clearState) {
                ResetState();
            }

            state.currentNodeName = nodeName;
            state.programCounter = 0;

            NodeStartHandler?.Invoke(nodeName);

            // Do we have a way to let the client know that certain lines
            // might be run?
            if (this.PrepareForLinesHandler != null)
            {
                // If we have a prepare-for-lines handler, figure out what
                // lines we anticipate running
                var stringIDs = Program.LineIDsForNode(nodeName);

                // Deliver the string IDs
                this.PrepareForLinesHandler(stringIDs);
            }

            return true;
        }

        public void Stop()
        {
            CurrentExecutionState = ExecutionState.Stopped;
            currentNode = null;
            DialogueCompleteHandler?.Invoke();
        }

        public void SetSelectedOption(int selectedOptionID)
        {
            if (CurrentExecutionState != ExecutionState.WaitingOnOptionSelection)
            {

                throw new DialogueException(@"SetSelectedOption was called, but Dialogue wasn't waiting for a selection.
                This method should only be called after the Dialogue is waiting for the user to select an option.");
            }

            if (selectedOptionID < 0 || selectedOptionID >= state.currentOptions.Count)
            {
                throw new ArgumentOutOfRangeException($"{selectedOptionID} is not a valid option ID (expected a number between 0 and {state.currentOptions.Count - 1}.");
            }

            // We now know what number option was selected; push the
            // corresponding node name to the stack
            var destinationNode = state.currentOptions[selectedOptionID].destination;
            state.PushValue(destinationNode);

            // We no longer need the accumulated list of options; clear it
            // so that it's ready for the next one
            state.currentOptions.Clear();

            // We're no longer in the WaitingForOptions state; we are now waiting for our game to let us continue
            CurrentExecutionState = ExecutionState.WaitingForContinue;
        }

        /// Resumes execution.
        internal void Continue()
        {
            CheckCanContinue();

            if (CurrentExecutionState == ExecutionState.DeliveringContent)
            {
                // We were delivering a line, option set, or command, and
                // the client has called Continue() on us. We're still
                // inside the stack frame of the client callback, so to
                // avoid recursion, we'll note that our state has changed
                // back to Running; when we've left the callback, we'll
                // continue executing instructions.
                CurrentExecutionState = ExecutionState.Running;
                return;
            }

            CurrentExecutionState = ExecutionState.Running;

            // Execute instructions until something forces us to stop
            while (currentNode != null && CurrentExecutionState == ExecutionState.Running)
            {
                Instruction currentInstruction = currentNode.Instructions[state.programCounter];

                RunInstruction(currentInstruction);

                state.programCounter++;

                if (currentNode != null && state.programCounter >= currentNode.Instructions.Count)
                {
                    ReturnFromNode(currentNode);
                    CurrentExecutionState = ExecutionState.Stopped;
                    DialogueCompleteHandler?.Invoke();
                    LogDebugMessage?.Invoke("Run complete.");
                }
            }
        }

        private void ReturnFromNode(Node? node) {
            if (node == null) {
                // Nothing to do.
                return;
            }
            NodeCompleteHandler?.Invoke(node.Name);

            string? nodeTrackingVariable = node.TrackingVariableName;
            if (nodeTrackingVariable != null) {
                if (this.VariableStorage.TryGetValue(nodeTrackingVariable, out float result)) {
                    result += 1;
                    this.VariableStorage.SetValue(nodeTrackingVariable, result);
                } else {
                    this.LogErrorMessage?.Invoke($"Failed to get the tracking variable for node {node.Name}");
                }
            }

        }

        /// <summary>
        /// Runs a series of tests to see if the <see
        /// cref="VirtualMachine"/> is in a state where <see
        /// cref="Continue"/> can be called. Throws an exception if it
        /// can't.
        /// </summary>
        /// <throws cref="DialogueException">Thrown when the <see
        /// cref="VirtualMachine"/> is not in a state where <see
        /// cref="Continue"/> could be called.</throws>
        private void CheckCanContinue()
        {
            if (currentNode == null)
            {
                throw new DialogueException("Cannot continue running dialogue. No node has been selected.");
            }

            if (CurrentExecutionState == ExecutionState.WaitingOnOptionSelection)
            {
                throw new DialogueException("Cannot continue running dialogue. Still waiting on option selection.");
            }

            if (OptionsHandler == null)
            {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(OptionsHandler)} has not been set.");
            }
            if (Library == null) {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(Library)} has not been set.");
            }
        }

        internal void RunInstruction(Instruction i)
        {
            switch (i.InstructionTypeCase)
            {
                case Instruction.InstructionTypeOneofCase.JumpTo:
                    state.programCounter = i.JumpTo.Destination - 1;
                    break;
                case Instruction.InstructionTypeOneofCase.PeekAndJump:
                    {
                        state.programCounter = state.PeekValue().ConvertTo<int>() - 1;
                    }
                    break;
                case Instruction.InstructionTypeOneofCase.RunLine:
                    {
                        // Looks up a string from the string table and
                        // passes it to the client as a line
                        string stringKey = i.RunLine.LineID;

                        Line line = new Line(stringKey);

                        var expressionCount = i.RunLine.SubstitutionCount;

                        var strings = new string[expressionCount];

                        for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--)
                        {
                            strings[expressionIndex] = state.PopValue().ConvertTo<string>();
                        }

                        line.Substitutions = strings;

                        // Suspend execution, because we're about to deliver content
                        CurrentExecutionState = ExecutionState.DeliveringContent;

                        LineHandler?.Invoke(line);

                        if (CurrentExecutionState == ExecutionState.DeliveringContent)
                        {
                            // The client didn't call Continue, so we'll
                            // wait here.
                            CurrentExecutionState = ExecutionState.WaitingForContinue;
                        }

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.RunCommand:
                    {
                        // Passes a string to the client as a custom command
                        string commandText = i.RunCommand.CommandText;

                        var expressionCount = i.RunCommand.SubstitutionCount;

                        // we create a list of replacements, these are: (startIndex, length, newVal) tuples
                        // where the startIndex and length come directly from the command itself,
                        // and the new value comes from the stack
                        var replacements = new List<(int StartIndex, int Length, string Value)>();
                        for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--)
                        {
                            var substitution = state.PopValue().ConvertTo<string>();

                            var marker = "{" + expressionIndex + "}";
                            var replacementIndex = commandText.LastIndexOf(marker, StringComparison.Ordinal);
                            if (replacementIndex != -1)
                            {
                                replacements.Add((replacementIndex, marker.Length, substitution));
                            }
                        }
                        // now we make those changes on the command string
                        foreach (var replacement in replacements)
                        {
                            commandText = commandText.Remove(replacement.StartIndex, replacement.Length).Insert(replacement.StartIndex, replacement.Value);
                        }

                        CurrentExecutionState = ExecutionState.DeliveringContent;

                        var command = new Command(commandText);

                        CommandHandler?.Invoke(command);

                        if (CurrentExecutionState == ExecutionState.DeliveringContent)
                        {
                            // The client didn't call Continue, so we'll
                            // wait here.
                            CurrentExecutionState = ExecutionState.WaitingForContinue;
                        }

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.AddOption:
                    {
                        // Add an option to the current state.

                        var lineID = i.AddOption.LineID;

                        var line = new Line(lineID);

                        // get the number of expressions that we're
                        // working with
                        var expressionCount = i.AddOption.SubstitutionCount;

                        var strings = new string[expressionCount];

                        // pop the expression values off the stack in
                        // reverse order, and store the list of substitutions
                        for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--)
                        {
                            string substitution = state.PopValue().ConvertTo<string>();
                            strings[expressionIndex] = substitution;
                        }

                        line.Substitutions = strings;


                        // Indicates whether the VM believes that the
                        // option should be shown to the user, based on any
                        // conditions that were attached to the option.
                        var lineConditionPassed = true;

                        // Get a bool that indicates
                        // whether this option had a condition or not.
                        // If it does, then a bool value will exist on
                        // the stack indiciating whether the condition
                        // passed or not. We pass that information to
                        // the game.

                        var hasLineCondition = i.AddOption.HasCondition;

                        if (hasLineCondition)
                        {
                            // This option has a condition. Get it from
                            // the stack.
                            lineConditionPassed = state.PopValue().ConvertTo<bool>();
                        }

                        state.currentOptions.Add(new PendingOption
                        {
                            line = line,
                            destination = i.AddOption.Destination,
                            enabled = lineConditionPassed,
                        });

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.ShowOptions:
                    {
                        // If we have no options to show, immediately stop.
                        if (state.currentOptions.Count == 0)
                        {
                            CurrentExecutionState = ExecutionState.Stopped;
                            DialogueCompleteHandler?.Invoke();
                            break;
                        }

                        // Present the list of options to the user and let them pick
                        var optionChoices = new List<OptionSet.Option>();

                        for (int optionIndex = 0; optionIndex < state.currentOptions.Count; optionIndex++)
                        {
                            var option = state.currentOptions[optionIndex];
                            optionChoices.Add(new OptionSet.Option(option.line, optionIndex, option.destination, option.enabled));
                        }

                        // We can't continue until our client tell us which
                        // option to pick
                        CurrentExecutionState = ExecutionState.WaitingOnOptionSelection;

                        // Pass the options set to the client, as well as a
                        // delegate for them to call when the user has made
                        // a selection
                        OptionsHandler?.Invoke(new OptionSet(optionChoices.ToArray()));

                        if (CurrentExecutionState == ExecutionState.WaitingForContinue)
                        {
                            // we are no longer waiting on an option
                            // selection - the options handler must have
                            // called SetSelectedOption! Continue running
                            // immediately.
                            CurrentExecutionState = ExecutionState.Running;
                        }

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.PushString:
                    state.PushValue(i.PushString.Value);
                    break;
                case Instruction.InstructionTypeOneofCase.PushFloat:
                    state.PushValue(i.PushFloat.Value);
                    break;
                case Instruction.InstructionTypeOneofCase.PushBool:
                    state.PushValue(i.PushBool.Value);
                    break;
                case Instruction.InstructionTypeOneofCase.JumpIfFalse:
                    {
                        if (state.PeekValue().ConvertTo<bool>() == false)
                        {
                            state.programCounter = i.JumpIfFalse.Destination - 1;
                        }
                    }
                    break;
                case Instruction.InstructionTypeOneofCase.Pop:
                    state.PopValue();
                    break;
                case Instruction.InstructionTypeOneofCase.CallFunc:
                    {
                        // Call a function, whose parameters are expected to
                        // be on the stack. Pushes the function's return value,
                        // if it returns one.
                        var functionName = i.CallFunc.FunctionName;

                        // If functionName is a special-cased internal compiler
                        // function, handle that
                        if (functionName.Equals(AddLineGroupCandidateFunctionName, StringComparison.Ordinal))
                        {
                            this.HandleAddLineGroupCandidate();
                            break;
                        }
                        if (functionName.Equals(SelectLineGroupCandidateFunctionName, StringComparison.Ordinal))
                        {
                            this.HandleSelectLineGroupCandidate();
                            break;
                        }

                        var function = Library.GetFunction(functionName);

                        var parameterInfos = function.Method.GetParameters();

                        var expectedParamCount = parameterInfos.Length;

                        // Expect the compiler to have placed the number of parameters
                        // actually passed at the top of the stack.
                        var actualParamCount = (int)state.PopValue().ConvertTo<int>();

                        if (expectedParamCount != actualParamCount)
                        {
                            throw new InvalidOperationException($"Function {functionName} expected {expectedParamCount} parameters, but received {actualParamCount}");
                        }

                        // Get the parameters, which were pushed in reverse
                        Value[] parameters = new Value[actualParamCount];
                        var parametersToUse = new object[actualParamCount];

                        for (int param = actualParamCount - 1; param >= 0; param--)
                        {
                            var value = state.PopValue();
                            var parameterType = parameterInfos[param].ParameterType;
                            // Perform type checking on this parameter
                            parametersToUse[param] = value.ConvertTo(parameterType);
                        }

                        // Invoke the function
                        try
                        {
                            IConvertible returnValue = (IConvertible)function.DynamicInvoke(parametersToUse);
                            // If the function returns a value, push it
                            bool functionReturnsValue = function.Method.ReturnType != typeof(void);

                            if (functionReturnsValue)
                            {
                                if (Types.TypeMappings.TryGetValue(returnValue.GetType(), out var yarnType))
                                {
                                    Value yarnValue = new Value(yarnType, returnValue);

                                    this.state.PushValue(yarnValue);
                                }
                            }
                        }
                        catch (System.Reflection.TargetInvocationException ex)
                        {
                            // The function threw an exception. Re-throw the exception it threw.
                            throw ex.InnerException;
                        }

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.PushVariable:
                    {

                        // Get the contents of a variable, push that onto the stack.
                        var variableName = i.PushVariable.VariableName;

                        Value loadedValue;

                        var didLoadValue = VariableStorage.TryGetValue<IConvertible>(variableName, out var loadedObject);


                        if (didLoadValue)
                        {
                            System.Type loadedObjectType = loadedObject.GetType();

                            var hasType = Types.TypeMappings.TryGetValue(loadedObjectType, out var yarnType);

                            if (hasType)
                            {
                                loadedValue = new Value(yarnType, loadedObject);
                            }
                            else
                            {
                                throw new InvalidOperationException($"No Yarn type found for {loadedObjectType}");
                            }
                        }
                        else
                        {
                            if (Program == null)
                            {
                                throw new InvalidOperationException("Program is null");
                            }
                            // We don't have a value for this. The initial
                            // value may be found in the program. (If it's
                            // not, then the variable's value is undefined,
                            // which isn't allowed.)
                            if (Program.InitialValues.TryGetValue(variableName, out var value))
                            {
                                switch (value.ValueCase)
                                {
                                    case Operand.ValueOneofCase.StringValue:
                                        loadedValue = new Value(Types.String, value.StringValue);
                                        break;
                                    case Operand.ValueOneofCase.BoolValue:
                                        loadedValue = new Value(Types.Boolean, value.BoolValue);
                                        break;
                                    case Operand.ValueOneofCase.FloatValue:
                                        loadedValue = new Value(Types.Number, value.FloatValue);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException($"Unknown initial value type {value.ValueCase} for variable {variableName}");
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException($"Variable storage returned a null value for variable {variableName}");
                            }
                        }

                        state.PushValue(loadedValue);

                        break;

                    }
                case Instruction.InstructionTypeOneofCase.StoreVariable:
                    {
                        // Store the top value on the stack in a variable.
                        var topValue = state.PeekValue();
                        var destinationVariableName = i.StoreVariable.VariableName;

                        if (topValue.Type == Types.Number)
                        {
                            VariableStorage.SetValue(destinationVariableName, topValue.ConvertTo<float>());
                        }
                        else if (topValue.Type == Types.String)
                        {
                            VariableStorage.SetValue(destinationVariableName, topValue.ConvertTo<string>());
                        }
                        else if (topValue.Type == Types.Boolean)
                        {
                            VariableStorage.SetValue(destinationVariableName, topValue.ConvertTo<bool>());
                        }
                        else
                        {
                            throw new ArgumentOutOfRangeException($"Invalid Yarn value type {topValue.Type}");
                        }

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.Stop:
                    {
                        // Immediately stop execution, and report that fact.
                        ReturnFromNode(currentNode);

                        // Unwind the call stack.
                        while (state.CanReturn) {
                            var node = Program?.Nodes[state.PopCallStack().nodeName];
                            ReturnFromNode(node);
                        }
                        
                        DialogueCompleteHandler?.Invoke();
                        CurrentExecutionState = ExecutionState.Stopped;

                        break;
                    }
                case Instruction.InstructionTypeOneofCase.RunNode:
                    ExecuteJumpToNode(i.RunNode.NodeName, false);
                    break;
                case Instruction.InstructionTypeOneofCase.PeekAndRunNode:
                    ExecuteJumpToNode(null, false);
                    break;
                case Instruction.InstructionTypeOneofCase.DetourToNode:
                    ExecuteJumpToNode(i.DetourToNode.NodeName, true);
                    break;
                case Instruction.InstructionTypeOneofCase.PeekAndDetourToNode:
                    ExecuteJumpToNode(null, true);
                    break;
                case Instruction.InstructionTypeOneofCase.Return:
                    {
                        ReturnFromNode(currentNode);
                        
                        State.CallSite returnSite = default;
                        if (state.CanReturn) {
                            returnSite = state.PopCallStack();
                        }
                        if (returnSite.nodeName == null) {
                            // We've reached the top of the call stack, so
                            // there's nowhere to return to. Stop the program.
                            DialogueCompleteHandler?.Invoke();
                            CurrentExecutionState = ExecutionState.Stopped;
                            break;
                        }
                        SetNode(returnSite.nodeName, clearState: false);
                        state.programCounter = returnSite.instruction;
                    }
                    break;
                default:
                    // Whoa, no idea what OpCode this is. Stop the program
                    // and throw an exception.
                    CurrentExecutionState = ExecutionState.Stopped;

                    throw new ArgumentOutOfRangeException($"{i.InstructionTypeCase} is not a supported instruction.");
            }
        }

        private void ExecuteJumpToNode(string? nodeName, bool isDetour)
        {
            if (isDetour)
            {
                // Preserve our current state.
                state.PushCallStack();
            } else {
                // We are jumping straight to another node. Unwind the current
                // call stack and issue a 'node complete' event for every node.
                ReturnFromNode(this.Program?.Nodes[CurrentNodeName]);

                while (state.CanReturn) {
                    var poppedNodeName = state.PopCallStack().nodeName;
                    if (poppedNodeName != null) {
                        ReturnFromNode(this.Program?.Nodes[poppedNodeName]);
                    }
                }

            }

            if (nodeName == null)
            {
                // The node name wasn't supplied - get it from the top of the stack.
                nodeName = state.PeekValue().ConvertTo<string>();
            }

            SetNode(nodeName, clearState: !isDetour);

            // Decrement program counter here, because it will
            // be incremented when this function returns, and
            // would mean skipping the first instruction
            state.programCounter -= 1;
        }

        internal struct LineGroupCandidate : IContentSaliencyOption {
            public const string NoneContentID = "Yarn.Internal.None";
            public string label;
            public int conditionValueCount;
            public string? lineID;

            public int ConditionValueCount => conditionValueCount;
            public string? ContentID => lineID;
        }

        private List<LineGroupCandidate> lineGroupCandidates = new List<LineGroupCandidate>();

        private void HandleSelectLineGroupCandidate()
        {
        
            // Pop the parameter count, which is 0
            var actualParamCount = state.PopValue().ConvertTo<int>();
            const int expectedParamCount = 0;
            if (actualParamCount != expectedParamCount) {
                throw new InvalidOperationException($"Function {SelectLineGroupCandidateFunctionName} expected {expectedParamCount} parameters, but received {actualParamCount}");
            }

            // There is always at least one candidate (even if it's only the
            // 'none' option that the compiler generates)
            if (lineGroupCandidates.Count == 0) {
                throw new InvalidOperationException($"Internal Yarn Spinner error: line group had zero candidates");
            }

            if (ContentSaliencyStrategy == null) {
                // We don't have a saliency strategy, so create and store a
                // basic one.
                ContentSaliencyStrategy = new Saliency.FirstSaliencyStrategy();
            }

            // Choose the content to present.
            var selectedContent = ContentSaliencyStrategy.ChooseBestContent(lineGroupCandidates);

            lineGroupCandidates.Clear();

            // Push the label onto the stack
            state.PushValue(selectedContent.label);
        }

        private void HandleAddLineGroupCandidate()
        {
            // 'Add Line Group Candidate' expects 3 parameters pushed in reverse order:
            // -label (str)
            // - condition count (num)
            // - line id (str)
            var actualParamCount = (int)state.PopValue().ConvertTo<int>();
            const int expectedParamCount = 3;

            if (expectedParamCount != actualParamCount)
            {
                throw new InvalidOperationException($"Function {AddLineGroupCandidateFunctionName} expected {expectedParamCount} parameters, but received {actualParamCount}");
            }

            var candidate = new LineGroupCandidate();

            candidate.label = state.PopValue().ConvertTo<string>();
            candidate.conditionValueCount = state.PopValue().ConvertTo<int>();

            string lineID = state.PopValue().ConvertTo<string>();

            if (string.Equals(lineID, LineGroupCandidate.NoneContentID, StringComparison.Ordinal))
            {
                // This content represents the 'none' option. Do not store a
                // line ID for it.
                candidate.lineID = null;
            }
            else
            {
                candidate.lineID = lineID;
            }

            lineGroupCandidates.Add(candidate);
        }

        private static void DummyCommandHandler(Command command)
        {
            throw new System.InvalidOperationException($"Smart node execution nodes must not run commands");
        }

        private static void DummyOptionsHandler(OptionSet options)
        {
            throw new System.InvalidOperationException($"Smart node execution nodes must not run options");
        }

        private static void DummyPrepareForLinesHandler(IEnumerable<string> lineIDs)
        {
            throw new System.InvalidOperationException($"Smart node execution nodes must not run lines");
        }

        private static void DummyLineHandler(Yarn.Line line) {
            throw new System.InvalidOperationException($"Smart node execution nodes must not run lines");
        }
    }

    internal struct PendingOption
    {
        public Line line;
        public int destination;
        public bool enabled;
    }
}

