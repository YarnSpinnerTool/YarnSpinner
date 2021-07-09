using System;
using System.Collections.Generic;
using System.Globalization;

using static Yarn.Instruction.Types;

namespace Yarn
{
    /// <summary>
    /// A value used by an Instruction.
    /// </summary>
    public partial class Operand {
        // Define some convenience constructors for the Operand type, so
        // that we don't need to have two separate steps for creating and
        // then preparing the Operand
        public Operand(bool value) : base() {
            this.BoolValue = value;
        }

        public Operand(string value) : base() {
            this.StringValue = value;
        }

        public Operand(float value) : base() {
            this.FloatValue = value;
        }
    }

    internal enum TokenType {


        // Special tokens
        Whitespace,
        Indent,
        Dedent,
        EndOfLine,
        EndOfInput,

        // Numbers. Everybody loves a number
        Number,

        // Strings. Everybody also loves a string
        String,

        // '#'
        TagMarker,

        // Command syntax ("<<foo>>")
        BeginCommand,
        EndCommand,

        // Variables ("$foo")
        Variable,

        // Shortcut syntax ("->")
        ShortcutOption,

        // Option syntax ("[[Let's go here|Destination]]")
        OptionStart, // [[
        OptionDelimit, // |
        OptionEnd, // ]]

        // Command types (specially recognised command word)
        If,
        ElseIf,
        Else,
        EndIf,
        Set,

        // Boolean values
        True,
        False,

        // The null value
        Null,

        // Parentheses
        LeftParen,
        RightParen,

        // Parameter delimiters
        Comma,

        // Operators
        EqualTo, // ==, eq, is
        GreaterThan, // >, gt
        GreaterThanOrEqualTo, // >=, gte
        LessThan, // <, lt
        LessThanOrEqualTo, // <=, lte
        NotEqualTo, // !=, neq

        // Logical operators
        Or, // ||, or
        And, // &&, and
        Xor, // ^, xor
        Not, // !, not

        // this guy's special because '=' can mean either 'equal to'
        // or 'becomes' depending on context
        EqualToOrAssign, // =, to

        UnaryMinus, // -; this is differentiated from Minus
                    // when parsing expressions

        Add, // +
        Minus, // -
        Multiply, // *
        Divide, // /
        Modulo, // %

        AddAssign, // +=
        MinusAssign, // -=
        MultiplyAssign, // *=
        DivideAssign, // /=

        Comment, // a run of text that we ignore

        Identifier, // a single word (used for functions)

        Text // a run of text until we hit other syntax
    }

    internal class VirtualMachine
    {

        internal class State {

            /// <summary>The name of the node that we're currently in.</summary>
            public string currentNodeName;

            /// <summary>The instruction number in the current node.</summary>
            public int programCounter = 0;

            /// <summary>List of options, where each option = (Line, destination node, enabled flag).</summary>
            public List<(Line line, string destination, bool enabled)> currentOptions = new List<(Line line, string destination, bool enabled)>();

            /// <summary>The value stack.</summary>
            private Stack<Value> stack = new Stack<Value>();

            /// <summary>Pushes a value onto the stack.</summary>
            public void PushValue(object o) {
                if( o is Value ) {
                    stack.Push(o as Value);
                } else {
                    stack.Push (new Value(o));
                }
            }

            /// <summary>Pops a value from the stack.</summary>
            public Value PopValue() {
                return stack.Pop ();
            }

            /// <summary>Peeks at a value from the stack.</summary>
            public Value PeekValue() {
                return stack.Peek ();
            }

            /// <summary>Clears the stack.</summary>
            public void ClearStack() {
                stack.Clear ();
            }
        }
        
        internal VirtualMachine (Dialogue d)
        {
            dialogue = d;
            state = new State ();
        }

        /// Reset the state of the VM
        internal void ResetState() {
            state = new State();
        }

        
        public LineHandler LineHandler;
        public OptionsHandler OptionsHandler;
        public CommandHandler CommandHandler;
		public NodeStartHandler NodeStartHandler;
		public NodeCompleteHandler NodeCompleteHandler;
        public DialogueCompleteHandler DialogueCompleteHandler;
        public PrepareForLinesHandler PrepareForLinesHandler;

        private Dialogue dialogue;

        internal Program Program { get; set; }

        private State state = new State();

        public string currentNodeName {
            get {
                return state.currentNodeName;
            }
        }

        public enum ExecutionState {
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

            if (Program == null || Program.Nodes.Count == 0) {
                throw new DialogueException($"Cannot load node {nodeName}: No nodes have been loaded.");
            }

            if (Program.Nodes.ContainsKey(nodeName) == false) {
                executionState = ExecutionState.Stopped;
                throw new DialogueException($"No node named {nodeName} has been loaded.");                
            }

            dialogue.LogDebugMessage?.Invoke ("Running node " + nodeName);

            currentNode = Program.Nodes [nodeName];
            ResetState ();
            state.currentNodeName = nodeName;

			NodeStartHandler?.Invoke(nodeName);

            // Do we have a way to let the client know that certain lines
            // might be run?
            if (this.PrepareForLinesHandler != null)
            {
                // If we have a prepare-for-lines handler, figure out what
                // lines we anticipate running

                // Create a list; we will never have more lines and options
                // than total instructions, so that's a decent capacity for
                // the list (TODO: maybe this list could be reused to save
                // on allocations?)
                var stringIDs = new List<string>(this.currentNode.Instructions.Count);

                // Loop over every instruction and find the ones that run a
                // line or add an option; these are the two instructions
                // that will signal a line can appear to the player
                foreach (var instruction in this.currentNode.Instructions)
                {
                    if (instruction.Opcode == OpCode.RunLine || instruction.Opcode == OpCode.AddOption)
                    {
                        // Both RunLine and AddOption have the string ID
                        // they want to show as their first operand, so
                        // store that
                        stringIDs.Add(instruction.Operands[0].StringValue);
                    }
                }

                // Deliver the string IDs
                this.PrepareForLinesHandler(stringIDs);
            }

            return true;
        }

        public void Stop() {
            executionState = ExecutionState.Stopped;
        }

        public void SetSelectedOption(int selectedOptionID) {

            if (executionState != ExecutionState.WaitingOnOptionSelection) {

                throw new DialogueException(@"SetSelectedOption was called, but Dialogue wasn't waiting for a selection.
                This method should only be called after the Dialogue is waiting for the user to select an option.");
            }

            if (selectedOptionID < 0 || selectedOptionID >= state.currentOptions.Count) {
                throw new ArgumentOutOfRangeException($"{selectedOptionID} is not a valid option ID (expected a number between 0 and {state.currentOptions.Count-1}.");
            }

            // We now know what number option was selected; push the
            // corresponding node name to the stack
            var destinationNode = state.currentOptions[selectedOptionID].destination;
            state.PushValue(destinationNode);

            // We no longer need the accumulated list of options; clear it
            // so that it's ready for the next one
            state.currentOptions.Clear();

            // We're no longer in the WaitingForOptions state; we are now waiting for our game to let us continue
            executionState = ExecutionState.WaitingForContinue;
        }

        /// Resumes execution.
        internal void Continue()
        {
            CheckCanContinue();

            if (executionState == ExecutionState.DeliveringContent) {
                // We were delivering a line, option set, or command, and
                // the client has called Continue() on us. We're still
                // inside the stack frame of the client callback, so to
                // avoid recursion, we'll note that our state has changed
                // back to Running; when we've left the callback, we'll
                // continue executing instructions.
                executionState = ExecutionState.Running;
                return;
            }

            executionState = ExecutionState.Running;

            // Execute instructions until something forces us to stop
            while (executionState == ExecutionState.Running)
            {
                Instruction currentInstruction = currentNode.Instructions[state.programCounter];

                RunInstruction(currentInstruction);

                state.programCounter++;

                if (state.programCounter >= currentNode.Instructions.Count)
                {
                    NodeCompleteHandler(currentNode.Name);
                    executionState = ExecutionState.Stopped;
                    DialogueCompleteHandler();
                    dialogue.LogDebugMessage("Run complete.");
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

            if (executionState == ExecutionState.WaitingOnOptionSelection)
            {
                throw new DialogueException("Cannot continue running dialogue. Still waiting on option selection.");
            }

            if (LineHandler == null)
            {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(LineHandler)} has not been set.");
            }

            if (OptionsHandler == null)
            {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(OptionsHandler)} has not been set.");
            }

            if (CommandHandler == null)
            {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(CommandHandler)} has not been set.");
            }

            if (NodeCompleteHandler == null)
            {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(NodeCompleteHandler)} has not been set.");
            }

            if (NodeCompleteHandler == null)
            {
                throw new DialogueException($"Cannot continue running dialogue. {nameof(NodeCompleteHandler)} has not been set.");
            }
        }

        /// Looks up the instruction number for a named label in the current node.
        internal int FindInstructionPointForLabel(string labelName) {

            if (currentNode.Labels.ContainsKey(labelName) == false) {
                // Couldn't find the node..
                throw new IndexOutOfRangeException (
                    $"Unknown label {labelName} in node {state.currentNodeName}"
                );
            }

            return currentNode.Labels [labelName];
        }

        internal void RunInstruction(Instruction i)
        {
            switch (i.Opcode)
            {
                case OpCode.JumpTo:
                    {
                        /// - JumpTo
                        /** Jumps to a named label
                         */
                        state.programCounter = FindInstructionPointForLabel(i.Operands[0].StringValue) - 1;

                        break;
                    }

                case OpCode.RunLine:
                    {
                        /// - RunLine
                        /** Looks up a string from the string table and
                         *  passes it to the client as a line
                         */
                        string stringKey = i.Operands[0].StringValue;

                        Line line = new Line(stringKey);
                        
                        // The second operand, if provided (compilers prior
                        // to v1.1 don't include it), indicates the number
                        // of expressions in the line. We need to pop these
                        // values off the stack and deliver them to the
                        // line handler.
                        if (i.Operands.Count > 1) {
                            // TODO: we only have float operands, which is
                            // unpleasant. we should make 'int' operands a
                            // valid type, but doing that implies that the
                            // language differentiates between floats and
                            // ints itself. something to think about.
                            var expressionCount = (int)i.Operands[1].FloatValue;

                            var strings = new string[expressionCount];

                            for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--) {
                                strings[expressionIndex] = state.PopValue().ConvertTo<string>();
                            }
                            
                            line.Substitutions = strings;
                        }

                        // Suspend execution, because we're about to deliver content
                        executionState = ExecutionState.DeliveringContent;

                        LineHandler(line);

                        if (executionState == ExecutionState.DeliveringContent) {
                            // The client didn't call Continue, so we'll
                            // wait here.
                            executionState = ExecutionState.WaitingForContinue;
                        }

                        break;
                    }

                case OpCode.RunCommand:
                    {
                        /// - RunCommand
                        /** Passes a string to the client as a custom command
                         */
                        
                        string commandText = i.Operands[0].StringValue;
                        
                        // The second operand, if provided (compilers prior
                        // to v1.1 don't include it), indicates the number
                        // of expressions in the command. We need to pop
                        // these values off the stack and deliver them to
                        // the line handler.
                        if (i.Operands.Count > 1) {
                            // TODO: we only have float operands, which is
                            // unpleasant. we should make 'int' operands a
                            // valid type, but doing that implies that the
                            // language differentiates between floats and
                            // ints itself. something to think about.
                            var expressionCount = (int)i.Operands[1].FloatValue;

                            var strings = new string[expressionCount];

                            // Get the values from the stack, and
                            // substitute them into the command text
                            for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--) {
                                var substitution = state.PopValue().ConvertTo<string>();

                                commandText = commandText.Replace("{" + expressionIndex + "}", substitution);
                            }
                            
                        }

                        executionState = ExecutionState.DeliveringContent;
                         
                        var command = new Command(commandText);

                        CommandHandler(command);

                        if (executionState == ExecutionState.DeliveringContent) {
                            // The client didn't call Continue, so we'll
                            // wait here.
                            executionState = ExecutionState.WaitingForContinue;
                        }

                        break;
                    }

                case OpCode.PushString:
                    {
                        /// - PushString
                        /** Pushes a string value onto the stack. The operand is an index into
                         *  the string table, so that's looked up first.
                         */
                        state.PushValue(i.Operands[0].StringValue);

                        break;
                    }

                case OpCode.PushFloat:
                    {
                        /// - PushFloat
                        /** Pushes a floating point onto the stack.
                         */
                        state.PushValue(i.Operands[0].FloatValue);

                        break;
                    }

                case OpCode.PushBool:
                    {
                        /// - PushBool
                        /** Pushes a boolean value onto the stack.
                         */
                        state.PushValue(i.Operands[0].BoolValue);

                        break;
                    }

                case OpCode.PushNull:
                    {
                        throw new InvalidOperationException("PushNull is no longer valid op code, because null is no longer a valid value from Yarn Spinner 2.0 onwards. To fix this error, re-compile the original source code.");
                    }

                case OpCode.JumpIfFalse:
                    {
                        /// - JumpIfFalse
                        /** Jumps to a named label if the value on the top of the stack
                         *  evaluates to the boolean value 'false'.
                         */
                        if (state.PeekValue().ConvertTo<bool>() == false)
                        {
                            state.programCounter = FindInstructionPointForLabel(i.Operands[0].StringValue) - 1;
                        }
                        break;
                    }

                case OpCode.Jump:
                    {/// - Jump
                        /** Jumps to a label whose name is on the stack.
                         */
                        var jumpDestination = state.PeekValue().ConvertTo<string>();
                        state.programCounter = FindInstructionPointForLabel(jumpDestination) - 1;

                        break;
                    }

                case OpCode.Pop:
                    {
                        /// - Pop
                        /** Pops a value from the stack.
                         */
                        state.PopValue();
                        break;
                    }

                case OpCode.CallFunc:
                    {

                        /// - CallFunc
                        /** Call a function, whose parameters are expected to
                         *  be on the stack. Pushes the function's return value,
                         *  if it returns one.
                         */
                        var functionName = i.Operands[0].StringValue;

                        var function = dialogue.Library.GetFunction(functionName);

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
                        try {
                            object returnValue = function.DynamicInvoke(parametersToUse);
                            // If the function returns a value, push it
                            bool functionReturnsValue = function.Method.ReturnType != typeof(void);

                            if (functionReturnsValue)
                            {
                                state.PushValue(new Value(returnValue));
                            }
                        } catch (System.Reflection.TargetInvocationException ex) {
                            // The function threw an exception. Re-throw the exception it threw.
                            throw ex.InnerException;
                        }

                        break;
                    }

                case OpCode.PushVariable:
                    {
                        /// - PushVariable
                        /** Get the contents of a variable, push that onto the stack.
                         */
                        var variableName = i.Operands[0].StringValue;
                        
                        Value loadedValue;
                        
                        var didLoadValue = dialogue.VariableStorage.TryGetValue<object>(variableName, out var loadedObject);

                        if (didLoadValue)
                        {
                            loadedValue = new Value(loadedObject);
                        }
                        else
                        {
                            // We don't have a value for this. The initial
                            // value may be found in the program. (If it's
                            // not, then the variable's value is undefined,
                            // which isn't allowed.)
                            if (Program.InitialValues.TryGetValue(variableName, out var value))
                            {
                                switch (value.ValueCase)
                                {
                                    case Operand.ValueOneofCase.StringValue:
                                        loadedValue = new Value(value.StringValue);
                                        break;
                                    case Operand.ValueOneofCase.BoolValue:
                                        loadedValue = new Value(value.BoolValue);
                                        break;
                                    case Operand.ValueOneofCase.FloatValue:
                                        loadedValue = new Value(value.FloatValue);
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

                case OpCode.StoreVariable:
                    {
                        /// - StoreVariable
                        /** Store the top value on the stack in a variable.
                         */
                        var topValue = state.PeekValue();
                        var destinationVariableName = i.Operands[0].StringValue;

                        switch (topValue.type)
                        {
                            case Type.Number:
                                dialogue.VariableStorage.SetValue(destinationVariableName, topValue.ConvertTo<float>());
                                break;
                            case Type.String:
                                dialogue.VariableStorage.SetValue(destinationVariableName, topValue.ConvertTo<string>());
                                break;
                            case Type.Bool:
                                dialogue.VariableStorage.SetValue(destinationVariableName, topValue.ConvertTo<bool>());
                                break;
                            default:
                                throw new ArgumentOutOfRangeException($"Invalid Yarn value type {topValue.type}");
                        }
                        

                        break;
                    }

                case OpCode.Stop:
                    {
                        /// - Stop
                        /** Immediately stop execution, and report that fact.
                         */
                        NodeCompleteHandler(currentNode.Name);
                        DialogueCompleteHandler();
                        executionState = ExecutionState.Stopped;

                        break;
                    }

                case OpCode.RunNode:
                    {
                        /// - RunNode
                        /** Run a node
                         */

                        // Pop a string from the stack, and jump to a node
                        // with that name.
                        string nodeName = state.PopValue().ConvertTo<string>();

                        NodeCompleteHandler(currentNode.Name);
                        
                        SetNode(nodeName);

                        // Decrement program counter here, because it will
                        // be incremented when this function returns, and
                        // would mean skipping the first instruction
                        state.programCounter -= 1; 

                        break;
                    }

                case OpCode.AddOption:
                    {
                        /// - AddOption
                        /** Add an option to the current state.
                         */

                        var line = new Line(i.Operands[0].StringValue);

                        if (i.Operands.Count > 2) {
                            // TODO: we only have float operands, which is
                            // unpleasant. we should make 'int' operands a
                            // valid type, but doing that implies that the
                            // language differentiates between floats and
                            // ints itself. something to think about.

                            // get the number of expressions that we're
                            // working with out of the third operand
                            var expressionCount = (int)i.Operands[2].FloatValue;

                            var strings = new string[expressionCount];

                            // pop the expression values off the stack in
                            // reverse order, and store the list of substitutions
                            for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--) {
                                string substitution = state.PopValue().ConvertTo<string>();
                                strings[expressionIndex] = substitution;
                            }
                            
                            line.Substitutions = strings;
                        }

                        // Indicates whether the VM believes that the
                        // option should be shown to the user, based on any
                        // conditions that were attached to the option.
                        var lineConditionPassed = true;

                        if (i.Operands.Count > 3) {
                            // The fourth operand is a bool that indicates
                            // whether this option had a condition or not.
                            // If it does, then a bool value will exist on
                            // the stack indiciating whether the condition
                            // passed or not. We pass that information to
                            // the game.

                            var hasLineCondition = i.Operands[3].BoolValue;

                            if (hasLineCondition)
                            {
                                // This option has a condition. Get it from
                                // the stack.
                                lineConditionPassed = state.PopValue().ConvertTo<bool>();
                            }
                        }

                        state.currentOptions.Add(
                            (line, // line to show
                            destination: i.Operands[1].StringValue, // node name
                            enabled: lineConditionPassed)); // whether the line condition passed

                        break;
                    }

                case OpCode.ShowOptions:
                    {
                        /// - ShowOptions
                        /** If we have no options to show, immediately stop.
                         */
                        if (state.currentOptions.Count == 0)
                        {
                            executionState = ExecutionState.Stopped;
                            DialogueCompleteHandler();
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
                        executionState = ExecutionState.WaitingOnOptionSelection;

                        // Pass the options set to the client, as well as a
                        // delegate for them to call when the user has made
                        // a selection
                        OptionsHandler(new OptionSet(optionChoices.ToArray()));

                        break;
                    }

                default:
                    {
                        /// - default
                        /** Whoa, no idea what OpCode this is. Stop the program
                         * and throw an exception.
                        */
                        executionState = ExecutionState.Stopped;
                        throw new ArgumentOutOfRangeException(
                            $"Unknown opcode {i.Opcode}"
                        );
                    }
            }
        }

    }
}

