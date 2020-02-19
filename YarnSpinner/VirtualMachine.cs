using System;
using System.Collections.Generic;
using System.Globalization;

using static Yarn.Instruction.Types;

namespace Yarn
{
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

            /// The name of the node that we're currently in
            public string currentNodeName;

            /// The instruction number in the current node
            public int programCounter = 0;

            /// List of options, where each option = <Line, destination node>
            public List<KeyValuePair<Line,string>> currentOptions = new List<KeyValuePair<Line, string>>();

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
        
        internal VirtualMachine (Dialogue d)
        {
            dialogue = d;
            state = new State ();
        }

        /// Reset the state of the VM
        internal void ResetState() {
            state = new State();
        }

        
        public Dialogue.LineHandler lineHandler;
        public Dialogue.OptionsHandler optionsHandler;
        public Dialogue.CommandHandler commandHandler;
        public Dialogue.NodeCompleteHandler nodeCompleteHandler;
        public Dialogue.DialogueCompleteHandler dialogueCompleteHandler;

        private Dialogue dialogue;

        internal Program Program { get; set; }

        private State state = new State();

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
            /** Suspended in the middle of execution */
            Suspended,
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
            if (Program.Nodes.ContainsKey(nodeName) == false) {
                executionState = ExecutionState.Stopped;
                throw new DialogueException($"No node named {nodeName}");                
            }

            dialogue.LogDebugMessage ("Running node " + nodeName);

            currentNode = Program.Nodes [nodeName];
            ResetState ();
            state.currentNodeName = nodeName;

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

            // We now know what number option was selected; push the
            // corresponding node name to the stack
            var destinationNode = state.currentOptions[selectedOptionID].Value;
            state.PushValue(destinationNode);

            // We no longer need the accumulated list of options; clear it
            // so that it's ready for the next one
            state.currentOptions.Clear();

            // We're no longer in the WaitingForOptions state; we are now
            // instead Suspended
            executionState = ExecutionState.Suspended;
        }
                    

        /// Resumes execution.inheritdoc
        internal void Continue() {

            if (currentNode == null) {
                throw new DialogueException("Cannot continue running dialogue. No node has been selected.");                
            }

            if (executionState == ExecutionState.WaitingOnOptionSelection) {
                throw new DialogueException ("Cannot continue running dialogue. Still waiting on option selection.");                
            }

            if (lineHandler == null) {
                throw new DialogueException ($"Cannot continue running dialogue. {nameof(lineHandler)} has not been set.");                
            }

            if (optionsHandler == null) {
                throw new DialogueException ($"Cannot continue running dialogue. {nameof(optionsHandler)} has not been set.");                
            }

            if (commandHandler == null) {
                throw new DialogueException ($"Cannot continue running dialogue. {nameof(commandHandler)} has not been set.");                
            }

            if (nodeCompleteHandler == null) {
                throw new DialogueException ($"Cannot continue running dialogue. {nameof(nodeCompleteHandler)} has not been set.");                
            }

            if (nodeCompleteHandler == null) {
                throw new DialogueException ($"Cannot continue running dialogue. {nameof(nodeCompleteHandler)} has not been set.");                
            }

            executionState = ExecutionState.Running;

            // Execute instructions until something forces us to stop
            while (executionState == ExecutionState.Running) {
                Instruction currentInstruction = currentNode.Instructions [state.programCounter];

                RunInstruction (currentInstruction);

                state.programCounter++;

                if (state.programCounter >= currentNode.Instructions.Count) {
                    nodeCompleteHandler(currentNode.Name);
                    executionState = ExecutionState.Stopped;
                    dialogueCompleteHandler();
                    dialogue.LogDebugMessage ("Run complete.");
                }
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
                                strings[expressionIndex] = state.PopValue().AsString;
                            }
                            
                            line.Substitutions = strings;
                        }

                        var pause = lineHandler(line);

                        if (pause == Dialogue.HandlerExecutionType.PauseExecution)
                        {
                            executionState = ExecutionState.Suspended;
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
                                var substitution = state.PopValue().AsString;

                                commandText = commandText.Replace("{" + expressionIndex + "}", substitution);
                            }
                            
                        }

                        var command = new Command(commandText);
                         
                        var pause = commandHandler(
                            command
                        );

                        if (pause == Dialogue.HandlerExecutionType.PauseExecution)
                        {
                            executionState = ExecutionState.Suspended;
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
                        /// - PushNull
                        /** Pushes a null value onto the stack.
                         */
                        state.PushValue(Value.NULL);

                        break;
                    }

                case OpCode.JumpIfFalse:
                    {
                        /// - JumpIfFalse
                        /** Jumps to a named label if the value on the top of the stack
                         *  evaluates to the boolean value 'false'.
                         */
                        if (state.PeekValue().AsBool == false)
                        {
                            state.programCounter = FindInstructionPointForLabel(i.Operands[0].StringValue) - 1;
                        }
                        break;
                    }

                case OpCode.Jump:
                    {/// - Jump
                        /** Jumps to a label whose name is on the stack.
                         */
                        var jumpDestination = state.PeekValue().AsString;
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

                        var function = dialogue.library.GetFunction(functionName);
                        {

                            var expectedParamCount = function.paramCount;

                            // Expect the compiler to have placed the number of parameters
                            // actually passed at the top of the stack.
                            var actualParamCount = (int)state.PopValue().AsNumber;

                            // If a function indicates -1 parameters, it takes as
                            // many parameters as it was given (i.e. it's a
                            // variadic function)
                            if (expectedParamCount == -1)
                            {
                                expectedParamCount = actualParamCount;
                            }

                            if (expectedParamCount != actualParamCount)
                            {
                                throw new InvalidOperationException($"Function {function.name} expected {expectedParamCount}, but received {actualParamCount}");
                            }

                            Value result;
                            if (actualParamCount == 0)
                            {
                                result = function.Invoke();
                            }
                            else
                            {
                                // Get the parameters, which were pushed in reverse
                                Value[] parameters = new Value[actualParamCount];
                                for (int param = actualParamCount - 1; param >= 0; param--)
                                {
                                    parameters[param] = state.PopValue();
                                }

                                // Invoke the function
                                result = function.InvokeWithArray(parameters);
                            }

                            // If the function returns a value, push it
                            if (function.returnsValue)
                            {
                                state.PushValue(result);
                            }
                        }

                        break;
                    }

                case OpCode.PushVariable:
                    {
                        /// - PushVariable
                        /** Get the contents of a variable, push that onto the stack.
                         */
                        var variableName = i.Operands[0].StringValue;
                        var loadedValue = dialogue.continuity.GetValue(variableName);
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
                        dialogue.continuity.SetValue(destinationVariableName, topValue);

                        break;
                    }

                case OpCode.Stop:
                    {
                        /// - Stop
                        /** Immediately stop execution, and report that fact.
                         */
                        nodeCompleteHandler(currentNode.Name);
                        dialogueCompleteHandler();
                        executionState = ExecutionState.Stopped;

                        break;
                    }

                case OpCode.RunNode:
                    {
                        /// - RunNode
                        /** Run a node
                         */
                        string nodeName;

                        if (i.Operands.Count == 0 || string.IsNullOrEmpty(i.Operands[0].StringValue))
                        {
                            // Get a string from the stack, and jump to a node with that name.
                            nodeName = state.PeekValue().AsString;
                        }
                        else
                        {
                            // jump straight to the node
                            nodeName = i.Operands[0].StringValue;
                        }

                        var pause = nodeCompleteHandler(currentNode.Name);
                        
                        SetNode(nodeName);

                        // Decrement program counter here, because it will
                        // be incremented when this function returns, and
                        // would mean skipping the first instruction
                        state.programCounter -= 1; 

                        if (pause == Dialogue.HandlerExecutionType.PauseExecution) {
                            executionState = ExecutionState.Suspended;
                        }                        

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
                                string substitution = state.PopValue().AsString;
                                strings[expressionIndex] = substitution;
                            }
                            
                            line.Substitutions = strings;
                        }

                        state.currentOptions.Add(
                            new KeyValuePair<Line, string>(
                                line,  // line to show
                                i.Operands[1].StringValue  // node name
                            )
                        );

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
                            dialogueCompleteHandler();
                            break;
                        }

                        // Present the list of options to the user and let them pick
                        var optionChoices = new List<OptionSet.Option>();

                        for (int optionIndex = 0; optionIndex < state.currentOptions.Count; optionIndex++)
                        {
                            var option = state.currentOptions[optionIndex];
                            optionChoices.Add(new OptionSet.Option(option.Key, optionIndex));
                        }

                        // We can't continue until our client tell us which option to pick
                        executionState = ExecutionState.WaitingOnOptionSelection;

                        // Pass the options set to the client, as well as a delegate for them to call when the
                        // user has made a selection

                        optionsHandler(new OptionSet(optionChoices.ToArray()));

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

