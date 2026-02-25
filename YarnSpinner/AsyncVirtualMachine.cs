// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.
#pragma warning disable CA2007
namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Yarn.Saliency;

    /// <summary>
    /// Contains methods for evaluating the value of smart variables
    /// </summary>
    public interface ISmartVariableEvaluator
    {
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

    public interface IDialogueResponder: ILibrary
    {
        ValueTask HandleLine(Line line, CancellationToken token);
        ValueTask<int> HandleOptions(OptionSet options, CancellationToken token);
        ValueTask HandleCommand(Command command, CancellationToken token);
        ValueTask HandleNodeStart(string node, CancellationToken token);
        ValueTask HandleNodeComplete(string node, CancellationToken token);
        ValueTask HandleDialogueComplete();
        ValueTask PrepareForLines(List<string> lineIDs, CancellationToken token);
        ValueTask<IConvertible> thunk(string functionName, IConvertible[] parameters, CancellationToken token);
        bool TryGetFunctionDefinition(string functionName, out FunctionDefinition functionDefinition);
        Dictionary<string, FunctionDefinition> allDefinitions { get; }
    }

    internal struct PendingOption
    {
        public Line line;
        public int destination;
        public bool enabled;
    }

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

    internal class AsyncVirtualMachine
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
            public Stack<Value> stack = new Stack<Value>();

            internal struct CallSite
            {
                public string nodeName;
                public int instruction;
            }

            private readonly Stack<CallSite> callStack = new Stack<CallSite>();

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
                if (currentNodeName == null)
                {
                    throw new InvalidOperationException("Internal error: Can't push current call stack, because not running a node");
                }

                callStack.Push(new CallSite
                {
                    nodeName = currentNodeName,
                    instruction = programCounter
                });
            }

            internal bool CanReturn => this.callStack.Count > 0;

            internal CallSite PopCallStack()
            {
                return callStack.Pop();
            }
        }

        internal AsyncVirtualMachine(IVariableStorage storage)
        {
            this.VariableStorage = storage;
            this.ContentSaliencyStrategy = new RandomBestLeastRecentlyViewedSaliencyStrategy(storage);
            state = new State();
        }

        /// Reset the state of the VM
        internal void ResetState()
        {
            state = new State();
        }

        private IDialogueResponder? dialogueResponder;
        public IDialogueResponder Responder
        {
            get
            {
                if (dialogueResponder == null)
                {
                    throw new ArgumentNullException($"Attempted to access {nameof(Responder)} without having set one.");
                }
                return dialogueResponder;
            }
            set
            {
                dialogueResponder = value;
            }
        }

        public bool IsDialogueRunning
        {
            get
            {
                if (dialogueCancellationSource == null || dialogueCancellationSource.IsCancellationRequested)
                {
                    return false;
                }
                return true;
            }
        }

        public IVariableStorage VariableStorage { get; set; }
        public Logger? LogDebugMessage { get; set; }
        public Logger? LogErrorMessage { get; set; }

        /// <summary>
        /// The <see cref="Program"/> that this virtual machine is running.
        /// </summary>
        internal Program? Program { get; set; }

        internal State state = new State();

        public string? CurrentNodeName => state.currentNodeName;

        public IContentSaliencyStrategy ContentSaliencyStrategy { get; internal set; }

        internal Node? currentNode;

        CancellationTokenSource? dialogueCancellationSource;

        // the global token exists so that the cancellation source can be linked into this token
        // this way in the case of Unity it can be the Application.exitCancellationToken
        // in other tools they can use their equivalent but mostly exists for testing porpoises
        public CancellationToken GlobalToken;

        public ValueTask SetNode(string nodeName)
        {
            return SetNode(nodeName, clearState: true);
        }

        internal async ValueTask SetNode(string nodeName, bool clearState)
        {
            if (Program == null || Program.Nodes.Count == 0)
            {
                throw new DialogueException($"Cannot load node {nodeName}: No nodes have been loaded.");
            }

            if (Program.Nodes.ContainsKey(nodeName) == false)
            {
                throw new DialogueException($"No node named {nodeName} has been loaded.");
            }

            LogDebugMessage?.Invoke("Setting node " + nodeName);

            currentNode = Program.Nodes[nodeName];

            if (clearState)
            {
                ResetState();
            }

            state.currentNodeName = nodeName;
            state.programCounter = 0;

            // figure out what lines we anticipate running
            var stringIDs = Program.LineIDsForNode(nodeName);

            // if we are already running dialogue we have a specific token ready for this
            // but this can be called before starting dialogue
            // and in those cases we won't yet have a token so we fall back to using the global one
            var token = dialogueCancellationSource?.Token ?? GlobalToken;

            // Deliver the string ID
            await Responder.PrepareForLines(stringIDs ?? new List<string>(), token);
        }

        public async ValueTask Stop()
        {
            currentNode = null;
            ResetState();

            // we have already cancelled dialogue, no reason to do it again
            if (dialogueCancellationSource == null || dialogueCancellationSource.IsCancellationRequested)
            {
                LogDebugMessage?.Invoke("Dialogue has already been cancelled");
                return;
            }
            
            dialogueCancellationSource.Cancel();
            dialogueCancellationSource = null;

            await Responder.HandleDialogueComplete();
        }

        public async ValueTask Start()
        {
            if (this.currentNode == null)
            {
                LogErrorMessage?.Invoke("Current node has not been set, unable to start dialogue");
                return;
            }

            LogDebugMessage?.Invoke($"Starting dialogue with {this.currentNode}");

            // need to see if we are already running
            // if we are then we 
            if (dialogueCancellationSource != null && !dialogueCancellationSource.IsCancellationRequested)
            {
                LogErrorMessage?.Invoke("Dialogue is already running, cannot start during this");
                throw new DialogueException("Dialogue is already running, cannot start new dialogue while existing dialogue is in progress. Stop the current dialogue before starting anew");
            }

            // refreshing our dialogue cancellation token
            // the old one will have been cancelled either by dialogue finishing or being stopped
            dialogueCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(GlobalToken);
            
            // kick off executing instructions
            var canContinue = true;

            // Execute instructions until something forces us to stop
            while (currentNode != null && canContinue)
            {
                if (dialogueCancellationSource.IsCancellationRequested)
                {
                    return;
                }

                Instruction currentInstruction = currentNode.Instructions[state.programCounter];

                canContinue = await RunInstruction(currentInstruction, dialogueCancellationSource.Token);

                state.programCounter++;

                if (currentNode != null && state.programCounter >= currentNode.Instructions.Count)
                {
                    await ReturnFromNode(currentNode);
                    await Stop();
                    LogDebugMessage?.Invoke("Run complete.");
                }
            }
        }


        private async ValueTask ReturnFromNode(Node? node)
        {
            if (node == null)
            {
                // Nothing to do.
                return;
            }
            if (dialogueCancellationSource == null || dialogueCancellationSource.IsCancellationRequested)
            {
                return;
            }
            await Responder.HandleNodeComplete(node.Name, dialogueCancellationSource.Token);

            string? nodeTrackingVariable = node.TrackingVariableName;
            if (nodeTrackingVariable != null)
            {
                if (this.VariableStorage.TryGetValue(nodeTrackingVariable, out float result))
                {
                    result += 1;
                    this.VariableStorage.SetValue(nodeTrackingVariable, result);
                }
                else
                {
                    this.LogErrorMessage?.Invoke($"Failed to get the tracking variable for node {node.Name}");
                }
            }
        }

        internal async ValueTask<bool> RunInstruction(Instruction i, CancellationToken cancellationToken)
        {
            switch (i.InstructionTypeCase)
            {
                case Instruction.InstructionTypeOneofCase.JumpTo:
                    state.programCounter = i.JumpTo.Destination - 1;
                    return true;
                case Instruction.InstructionTypeOneofCase.PeekAndJump:
                    {
                        state.programCounter = state.PeekValue().ConvertTo<int>() - 1;
                    }
                    return true;
                case Instruction.InstructionTypeOneofCase.RunLine:
                    {
                        // Looks up a string from the string table and
                        // passes it to the client as a line
                        string stringKey = i.RunLine.LineID;

                        var expressionCount = i.RunLine.SubstitutionCount;

                        var strings = new string[expressionCount];

                        for (int expressionIndex = expressionCount - 1; expressionIndex >= 0; expressionIndex--)
                        {
                            strings[expressionIndex] = state.PopValue().ConvertTo<string>();
                        }

                        Line line = new Line(stringKey, strings);

                        await Responder.HandleLine(line, cancellationToken);

                        return true;
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

                        var command = new Command(commandText);

                        await Responder.HandleCommand(command, cancellationToken);

                        return true;
                    }
                case Instruction.InstructionTypeOneofCase.AddOption:
                    {
                        // Add an option to the current state.

                        var lineID = i.AddOption.LineID;

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

                        var line = new Line(lineID, strings);

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

                        return true;
                    }
                case Instruction.InstructionTypeOneofCase.ShowOptions:
                    {
                        // If we have no options to show, immediately stop.
                        if (state.currentOptions.Count == 0)
                        {
                            await Stop();
                            return false;
                            // should this throw?
                        }

                        // Present the list of options to the user and let them pick
                        var optionChoices = new List<OptionSet.Option>();

                        for (int optionIndex = 0; optionIndex < state.currentOptions.Count; optionIndex++)
                        {
                            var option = state.currentOptions[optionIndex];
                            optionChoices.Add(new OptionSet.Option(option.line, optionIndex, option.destination, option.enabled));
                        }

                        var chosenOption = await Responder.HandleOptions(new OptionSet(optionChoices.ToArray()), cancellationToken);
                        
                        if (chosenOption == AsyncDialogue.NoOptionSelected)
                        {
                            // Push a flag indicating that no option was selected.
                            // this means the jump if false will pass taking us to the end of the option statement
                            this.state.PushValue(false);
                        }
                        else
                        {
                            if (chosenOption < 0 || chosenOption >= state.currentOptions.Count)
                            {
                                throw new ArgumentOutOfRangeException($"{chosenOption} is not a valid option ID (expected a number between 0 and {state.currentOptions.Count - 1}.");
                            }

                            // We now know what number option was selected; push the
                            // corresponding node name to the stack
                            var destinationInstruction = state.currentOptions[chosenOption].destination;
                            state.PushValue(destinationInstruction);
                            // pushing a true to indicate that an option was selected
                            state.PushValue(true);
                        }

                        // We no longer need the accumulated list of options; clear it
                        // so that it's ready for the next one
                        state.currentOptions.Clear();

                        return true;
                    }
                case Instruction.InstructionTypeOneofCase.PushString:
                    state.PushValue(i.PushString.Value);
                    return true;
                case Instruction.InstructionTypeOneofCase.PushFloat:
                    state.PushValue(i.PushFloat.Value);
                    return true;
                case Instruction.InstructionTypeOneofCase.PushBool:
                    state.PushValue(i.PushBool.Value);
                    return true;
                case Instruction.InstructionTypeOneofCase.JumpIfFalse:
                    {
                        if (state.PeekValue().ConvertTo<bool>() == false)
                        {
                            state.programCounter = i.JumpIfFalse.Destination - 1;
                        }
                    }
                    return true;
                case Instruction.InstructionTypeOneofCase.Pop:
                    state.PopValue();
                    return true;
                case Instruction.InstructionTypeOneofCase.CallFunc:
                    {
                        var value = await CallFunction(i.CallFunc.FunctionName, state.stack, this.Responder, cancellationToken);
                        state.stack.Push(value);
                        return true;
                    }

                case Instruction.InstructionTypeOneofCase.PushVariable:
                    {
                        // Get the contents of a variable, push that onto the stack.
                        var variableName = i.PushVariable.VariableName;

                        Value loadedValue;

                        var didLoadValue = VariableStorage.TryGetValue<IConvertible>(variableName, out var loadedObject);

                        if (didLoadValue && loadedObject != null)
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
                            // We don't have a value for this. The initial
                            // value may be found in the program. (If it's
                            // not, then the variable's value is undefined,
                            // which isn't allowed.)

                            if (Program == null)
                            {
                                throw new InvalidOperationException("Program is null");
                            }

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

                        return true;

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

                        return true;
                    }
                case Instruction.InstructionTypeOneofCase.Stop:
                    {
                        // Immediately stop execution, and report that fact.
                        await ReturnFromNode(currentNode);

                        // Unwind the call stack.
                        while (state.CanReturn)
                        {
                            var node = Program?.Nodes[state.PopCallStack().nodeName];
                            await ReturnFromNode(node);
                        }

                        await Stop();

                        return false;
                    }
                case Instruction.InstructionTypeOneofCase.RunNode:
                    await ExecuteJumpToNode(i.RunNode.NodeName, false);
                    return true;
                case Instruction.InstructionTypeOneofCase.PeekAndRunNode:
                    await ExecuteJumpToNode(null, false);
                    return true;
                case Instruction.InstructionTypeOneofCase.DetourToNode:
                    await ExecuteJumpToNode(i.DetourToNode.NodeName, true);
                    return true;
                case Instruction.InstructionTypeOneofCase.PeekAndDetourToNode:
                    await ExecuteJumpToNode(null, true);
                    return true;
                case Instruction.InstructionTypeOneofCase.Return:
                    {
                        await ReturnFromNode(currentNode);

                        State.CallSite returnSite = default;
                        if (state.CanReturn)
                        {
                            returnSite = state.PopCallStack();
                        }
                        if (returnSite.nodeName == null)
                        {
                            // We've reached the top of the call stack, so
                            // there's nowhere to return to. Stop the program.
                            await Stop();
                            return false;
                        }
                        await SetNode(returnSite.nodeName, clearState: false);
                        state.programCounter = returnSite.instruction;
                    }
                    return true;
                case Instruction.InstructionTypeOneofCase.AddSaliencyCandidate:
                    {
                        var condition = this.state.PopValue().ConvertTo<bool>();

                        var candidate = new ContentSaliencyOption(i.AddSaliencyCandidate.ContentID)
                        {
                            ComplexityScore = i.AddSaliencyCandidate.ComplexityScore,
                            FailingConditionValueCount = condition ? 0 : 1,
                            PassingConditionValueCount = condition ? 1 : 0,
                            Destination = i.AddSaliencyCandidate.Destination,
                            ContentType = ContentSaliencyContentType.Line,
                        };

                        saliencyCandidateList.Add(candidate);
                    }
                    return true;
                case Instruction.InstructionTypeOneofCase.AddSaliencyCandidateFromNode:
                    {
                        var nodeName = i.AddSaliencyCandidateFromNode.NodeName;

                        if (this.Program == null)
                        {
                            throw new InvalidOperationException($"Failed to add saliency candidate from node {nodeName}: {nameof(Program)} is null");
                        }

                        if (this.Program.Nodes.TryGetValue(nodeName, out var node) == false)
                        {
                            throw new InvalidOperationException($"Failed to add saliency candidate from node {nodeName}: no node with this name is loaded");
                        }

                        if (this.VariableStorage.SmartVariableEvaluator == null)
                        {
                            throw new InvalidOperationException($"Failed to add saliency candidate from node {nodeName}: {nameof(this.VariableStorage.SmartVariableEvaluator)} is not set");
                        }

                        int passed = 0;
                        int failed = 0;

                        foreach (var variableName in node.ContentSaliencyConditionVariables)
                        {
                            // For each condition variable in this node,
                            // evaluate it, and record whether it evaluated to
                            // true or false.
                            this.VariableStorage.SmartVariableEvaluator.TryGetSmartVariable(variableName, out bool result);

                            if (result) { passed += 1; }
                            else { failed += 1; }
                        }

                        int conditionComplexityScore = node.ContentSaliencyConditionComplexityScore;

                        var candidate = new ContentSaliencyOption(nodeName)
                        {
                            ComplexityScore = conditionComplexityScore,
                            FailingConditionValueCount = failed,
                            PassingConditionValueCount = passed,
                            Destination = i.AddSaliencyCandidateFromNode.Destination,
                            ContentType = ContentSaliencyContentType.Node,
                        };

                        this.saliencyCandidateList.Add(candidate);

                    }
                    return true;
                case Instruction.InstructionTypeOneofCase.SelectSaliencyCandidate:
                    {
                        // Pass the collection of salient content candidates to
                        // the strategy and get back either a single thing to
                        // run, or null
                        ContentSaliencyOption? result = this.ContentSaliencyStrategy.QueryBestContent(this.saliencyCandidateList);

                        if (result != null)
                        {
                            // The content that was selected must be one of the candidates.
                            bool selectedContentWasValid = saliencyCandidateList.Contains(result);

                            if (selectedContentWasValid == false)
                            {
                                throw new DialogueException($"Content saliency strategy {ContentSaliencyStrategy} did not " +
                                    $"return a valid selection (available content IDs to choose from were " +
                                    $"{string.Join(", ", saliencyCandidateList)}, but strategy returned {result}");
                            }

                            // Indicate to the strategy that we are committing
                            // to this item.
                            this.ContentSaliencyStrategy.ContentWasSelected(result);

                            // Push the destination that we're jumping to.
                            this.state.PushValue(result.Destination);

                            // Push a flag indicating that content was selected.
                            this.state.PushValue(true);
                        }
                        else
                        {
                            // No content was selected.

                            // Push a flag indicating that content was not selected.
                            this.state.PushValue(false);
                        }

                        // Clear the saliency candidate list
                        saliencyCandidateList.Clear();
                    }
                    return true;
                default:
                    throw new ArgumentOutOfRangeException($"{i.InstructionTypeCase} is not a supported instruction.");
            }
        }

        private readonly List<ContentSaliencyOption> saliencyCandidateList = new List<ContentSaliencyOption>();

        private async ValueTask ExecuteJumpToNode(string? nodeName, bool isDetour)
        {
            if (isDetour)
            {
                // Preserve our current state.
                state.PushCallStack();
            }
            else
            {
                // We are jumping straight to another node. Unwind the current
                // call stack and issue a 'node complete' event for every node.
                await ReturnFromNode(this.Program?.Nodes[CurrentNodeName]);

                while (state.CanReturn)
                {
                    var poppedNodeName = state.PopCallStack().nodeName;
                    if (poppedNodeName != null)
                    {
                        await ReturnFromNode(this.Program?.Nodes[poppedNodeName]);
                    }
                }

            }

            if (nodeName == null)
            {
                // The node name wasn't supplied - get it from the top of the stack.
                nodeName = state.PeekValue().ConvertTo<string>();
            }

            await SetNode(nodeName, clearState: !isDetour);

            // Decrement program counter here, because it will
            // be incremented when this function returns, and
            // would mean skipping the first instruction
            state.programCounter -= 1;
        }

        public static Value[] ParametersForFunction(FunctionDefinition function, Stack<Value> stack)
        {
            // we are gonna do a basic check, we assume our responder will make sure it's all perfect
            // we could do more here but then we'd have to deal with the issues of unknown types again
            // much easier to let the responder who has them deal with it
            // we all need here to have the minimum number of parameters we'd expect
            var actualParamCount = stack.Pop().ConvertTo<int>();
            var expectedCount = function.functionType.Parameters.Count + (function.functionType.VariadicParameterType == null ? 0 : 1);

            var minimumCount = expectedCount - (function.functionType.VariadicParameterType == null ? 0 : 1);
            if (actualParamCount < minimumCount)
            {
                throw new System.InvalidOperationException($"Internal error: {function.Name}'s expects {expectedCount} parameters but has {actualParamCount}");
            }

            // make a single array of yarnvalues of the appropriate size
            var parameters = new Yarn.Value[actualParamCount];
            for (int i = actualParamCount - 1; i > -1; i--)
            {
                parameters[i] = stack.Pop();
            }
            return parameters;
        }

        public static async ValueTask<Yarn.Value> CallFunction(string functionName, Stack<Value> state, IDialogueResponder? responder, CancellationToken token)
        {
            if (responder != null && responder.TryGetFunctionDefinition(functionName, out var function))
            {
                var parameters = ParametersForFunction(function, state);
                
                var result = await responder.thunk(function.Name, parameters, token);
                if (result == null)
                {
                    throw new System.InvalidOperationException($"Internal error: Return value of the function is not convertible to a Yarn type.");
                }

                if (Types.TypeMappings.TryGetValue(result.GetType(), out var yarnType))
                {
                    Value yarnValue = new Value(yarnType, result);
                    return yarnValue;
                }
                else
                {
                    throw new System.InvalidOperationException($"Internal error: Return value of the function is not convertible to a Yarn type.");
                }
            }
            else if (StandardLibrary.TryGetFunction(functionName, out function))
            {
                var parameters = ParametersForFunction(function, state);
                var value = await StandardLibrary.CallFunc(function, parameters, token);

                return value;
            }
            else
            {
                throw new System.ArgumentException($"Encountered a function call to \"{functionName}\", this does not match a function we know about");
            }
        }
    }
}
