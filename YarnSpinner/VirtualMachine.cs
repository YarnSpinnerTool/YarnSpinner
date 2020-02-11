using System;
using System.Collections.Generic;
using Google.Protobuf;
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

    public abstract class SerializedVMState {

        public string Text { get; protected set; }

        public abstract VMState Deserialize();
    }

    public class JsonVMState : SerializedVMState
    {
        public JsonVMState(string text) {
            Text = text;
        }

        private VMState _deserialized;

        public override VMState Deserialize() {
            if (this._deserialized != null) {
                return this._deserialized;
            }

            JsonParser parser = JsonParser.Default;
            VMState newState;

            // not sure what Parse() returns when it fails to parse.
            try {
                newState = parser.Parse<VMState>(Text);
            }
            catch (InvalidJsonException exception) {
                throw exception;
            }

            if (newState != null) {
                this._deserialized = newState;
            }

            return this._deserialized;
        }
    }

    public partial class VMStateOption
    {
        // Works the same as KeyValuePair, but in protobuf.

        public VMStateOption(string key, string value) : base()
        {
            this.Key = key;
            this.Value = value;
        }
    }

    public partial class VMStateValue
    {
        // This entire class exists only because serializing entire Value object
        // seemed unnecessary so instead we are rebuilding Values from less complex context.

        public VMStateValue(Value yarnValue) : base() {
            this.YarnValue = yarnValue;
            this.Type = (int)yarnValue.type;
            this.Value = yarnValue.AsString;
        }

        private Value _yarnValue;
        public Value YarnValue {
            get {
                if (this._yarnValue == null) {
                    Yarn.Value.Type type = (Yarn.Value.Type)this.Type;

                    switch (type) {
                        case Yarn.Value.Type.Null:
                            this._yarnValue = Yarn.Value.NULL;
                            break;
                        case Yarn.Value.Type.Number:
                            this._yarnValue = new Yarn.Value(float.Parse(this.Value));
                            break;
                        case Yarn.Value.Type.String:
                            this._yarnValue = new Yarn.Value(this.Value);
                            break;
                        case Yarn.Value.Type.Bool:
                            this._yarnValue = new Yarn.Value(bool.Parse(this.Value));
                            break;
                    }
                }

                return this._yarnValue;
            }
            private set {
                this._yarnValue = value;
            }
        }
    }

    public partial class VMState
    {
        // The methods in here used to handle a Stack object, but it is easier to serialize
        // and handle a List these methods are now providing Stack functionality on top of a List.

        /// Methods for working with the stack
        public void PushValue(object o) {
            if (o is Value) {
                Stack.Add(new VMStateValue(o as Value));
            } else {
                Stack.Add(new VMStateValue(new Value(o)));
            }
        }

        /// Pop a value from the stack
        public Value PopValue() {
            if (Stack.Count != 0) {
                int lastElement = Stack.Count - 1;
                Value item = Stack[lastElement].YarnValue;
                Stack.RemoveAt(lastElement);

                return item;
            }

            return null;
        }

        /// Peek at a value from the stack
        public Value PeekValue() {
            if (Stack.Count != 0) {
                return Stack[Stack.Count - 1].YarnValue;
            }

            return null;
        }

        /// Clear the stack
        public void ClearStack() {
            Stack.Clear();
        }
    }

    internal class VirtualMachine
    {
        internal VirtualMachine (Dialogue d)
        {
            dialogue = d;
            state = new VMState();
        }

        /// Reset the state of the VM
        internal void ResetState() {
            state = new VMState();
        }

        // Using this flag to return expected VM State programCounter.
        // depends on when the State is requested, mostly it would be after
        // an instruction has been processed which increments the counter.
        private bool instructionProcessed = false;

        /// <summary>
        /// Get current State of Virtual Machine as a copy of the VMState object.
        /// </summary>
        public VMState GetStateClone() {
            if (string.IsNullOrEmpty(this.state.CurrentNodeName)) {
                dialogue.LogErrorMessage($"Cannot get State of the Virtual Machine because it is not running any node.");
                return null;
            }

            VMState clonedState = this.state.Clone();

            // Here we decrement saved States program counter, otherwise it would
            // indicate next instruction instead of the current one.
            if (this.instructionProcessed) {
                clonedState.ProgramCounter--;
            }

            return clonedState;
        }

        /// <summary>
        /// Get current State of Virtual Machine as serialized object.
        /// </summary>
        public JsonVMState GetStateSerialized() {
            JsonFormatter formatter = JsonFormatter.Default;
            string json = formatter.Format(GetStateClone());

            return new JsonVMState(json);
        }

        /// <summary>
        /// Set Virtual Machine state from a VMState object.
        /// </summary>
        public void SetState(VMState newState) {
            if (string.IsNullOrEmpty(newState.CurrentNodeName)) {
                // If loaded state NodeName is empty, throw error.
                throw new ArgumentException("Tried to set VM State that does not have any Node set, this is invalid because VM has no idea ");
            } else {
                // Otherwise set VMs node to what the State is expecting.
                SetNode(newState.CurrentNodeName);
            }

            this.state = newState;
        }

        /// <summary>
        /// Set Virtual Machine state from a serialized object.
        /// </summary>
        public void SetState(SerializedVMState serialized) {
            SetState(serialized.Deserialize());
        }

        public Dialogue.LineHandler lineHandler;
        public Dialogue.OptionsHandler optionsHandler;
        public Dialogue.CommandHandler commandHandler;
        public Dialogue.NodeCompleteHandler nodeCompleteHandler;
        public Dialogue.DialogueCompleteHandler dialogueCompleteHandler;

        private Dialogue dialogue;

        internal Program Program { get; set; }

        private VMState state = new VMState();

        public string currentNodeName {
            get {
                return state.CurrentNodeName;
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
            state.CurrentNodeName = nodeName;

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
            var destinationNode = state.CurrentOptions[selectedOptionID].Value;
            state.PushValue(destinationNode);

            // We no longer need the accumulated list of options; clear it
            // so that it's ready for the next one
            state.CurrentOptions.Clear();

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
                this.instructionProcessed = false;

                Instruction currentInstruction = currentNode.Instructions [state.ProgramCounter];
                RunInstruction (currentInstruction);

                state.ProgramCounter++;
                this.instructionProcessed = true;

                if (state.ProgramCounter >= currentNode.Instructions.Count) {
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
                    $"Unknown label {labelName} in node {state.CurrentNodeName}"
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
                        state.ProgramCounter = FindInstructionPointForLabel(i.Operands[0].StringValue) - 1;

                        break;
                    }

                case OpCode.RunLine:
                    {
                        /// - RunLine
                        /** Looks up a string from the string table and
                         *  passes it to the client as a line
                         */
                        string stringKey = i.Operands[0].StringValue;

                        var pause = lineHandler(new Line(stringKey));

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
                        var pause = commandHandler(
                            new Command(i.Operands[0].StringValue)
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
                            state.ProgramCounter = FindInstructionPointForLabel(i.Operands[0].StringValue) - 1;
                        }
                        break;
                    }

                case OpCode.Jump:
                    {/// - Jump
                        /** Jumps to a label whose name is on the stack.
                         */
                        var jumpDestination = state.PeekValue().AsString;
                        state.ProgramCounter = FindInstructionPointForLabel(jumpDestination) - 1;

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
                        state.ProgramCounter -= 1; 

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
                        state.CurrentOptions.Add(
                            new VMStateOption(
                                i.Operands[0].StringValue, // display string key
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
                        if (state.CurrentOptions.Count == 0)
                        {
                            executionState = ExecutionState.Stopped;
                            dialogueCompleteHandler();
                            break;
                        }

                        // Present the list of options to the user and let them pick
                        var optionChoices = new List<OptionSet.Option>();

                        for (int optionIndex = 0; optionIndex < state.CurrentOptions.Count; optionIndex++)
                        {
                            var option = state.CurrentOptions[optionIndex];
                            var line = new Line(option.Key);
                            optionChoices.Add(new OptionSet.Option(line, optionIndex));
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

