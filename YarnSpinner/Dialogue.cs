// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#pragma warning disable CA2007
namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Yarn.Markup;

#nullable enable

    /// <summary>
    /// A line of dialogue, sent from the <see cref="AsyncDialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// <para>When the game receives a <see cref="Line"/>, it should do the
    /// following things to prepare the line for presentation to the user.
    /// </para>
    /// <list type="number">
    /// <item>Use the value in the <see cref="ID"/> field to look up the
    /// appropriate user-facing text in the string table. </item>
    ///
    /// <item>Use <see cref="LineParser.ExpandSubstitutions(string, IList{string})"/> to replace all
    /// substitutions in the user-facing text.</item>
    ///
    /// <item>Use <see cref="LineParser.ParseString(string, string, bool, bool, bool)"/> to parse all markup in the
    /// line.</item>
    /// </list>
    ///
    /// <para>You typically do not create instances of this struct yourself.
    /// They are created by the <see cref="AsyncDialogue"/> during program
    /// execution.</para>
    /// </remarks>
    /// <seealso cref="IDialogueResponder.HandleLine(Line, System.Threading.CancellationToken)"/>
#pragma warning disable CA1815
    public struct Line
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="Line"/> struct.
        /// </summary>
        /// <param name="stringID">The unique line ID for this content.</param>
        /// <param name="substitutions">The list of values that should be
        /// substituted into the final line.
        /// </param>
        public Line(string stringID, string[] substitutions) : this()
        {
            this.ID = stringID;
            this.Substitutions = substitutions;
        }

        /// <summary>
        /// The string ID for this line.
        /// </summary>
        public string ID { get; }

        /// <summary>
        /// The values that should be inserted into the user-facing text before
        /// delivery.
        /// </summary>
        public string[] Substitutions { get; }
    }
#pragma warning restore CA1815

    /// <summary>
    /// A set of <see cref="OptionSet.Option"/>s, sent from the <see
    /// cref="AsyncDialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You typically do not create instances of this struct yourself. They are
    /// created by the <see cref="AsyncDialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="IDialogueResponder.HandleOptions(OptionSet, CancellationToken)"/>
#pragma warning disable CA1815
    public struct OptionSet
    {
        internal OptionSet(Option[] options)
        {
            Options = options;
        }

#pragma warning disable CA1716
        /// <summary>
        /// An option to be presented to the user.
        /// </summary>
        public struct Option
        {
            internal Option(Line line, int id, bool isAvailable)
            {
                Line = line;
                ID = id;
                IsAvailable = isAvailable;
            }

            /// <summary>
            /// Gets the <see cref="Line"/> that should be presented to the
            /// user for this option.
            /// </summary>
            /// <remarks>
            /// See the documentation for the <see cref="Yarn.Line"/> class
            /// for information on how to prepare a line before presenting
            /// it to the user. 
            /// </remarks>
            public Line Line { get; private set; }

            /// <summary>
            /// Gets the identifying number for this option.
            /// </summary>
            /// <remarks>
            /// You should not rely on this value being the same as the index of the option inside the <see cref="OptionSet"/>
            /// </remarks>
            public int ID { get; private set; }

            /// <summary>
            /// Gets a value indicating whether the player should be
            /// permitted to select this option.
            /// </summary>
            /// <remarks>
            /// <para>
            /// If this value is <see langword="false"/>, this option had a
            /// line condition on it that failed. The option will still be
            /// delivered to the game, but, depending on the needs of the
            /// game, the game may decide to not allow the player to select
            /// it, or not offer it to the player at all.
            /// </para>
            /// <para>
            /// This is intended for situations where games wish to show
            /// options that the player _could_ have taken, if some other
            /// condition had been met (e.g. having enough "charisma"
            /// points).
            /// </para>
            /// </remarks>
            public bool IsAvailable { get; private set; }
        }
#pragma warning restore CA1716

        /// <summary>
        /// Gets the <see cref="Option"/>s that should be presented to the
        /// user.
        /// </summary>
        /// <seealso cref="Option"/>
        public Option[] Options { get; private set; }
    }
#pragma warning restore CA1815

    /// <summary>
    /// A command, sent from the <see cref="AsyncDialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="AsyncDialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="IDialogueResponder.HandleCommand(Command, CancellationToken)"/>
#pragma warning disable CA1815
    public struct Command
    {
        internal Command(string text)
        {
            Text = text;
        }

        /// <summary>
        /// Gets the text of the command.
        /// </summary>
        public string Text { get; private set; }
    }
#pragma warning restore CA1815

    /// <summary>
    /// Represents a method that receives diagnostic messages and error
    /// information from a <see cref="AsyncDialogue"/>.
    /// </summary>
    /// <remarks>
    /// The text that this delegate receives may be output to a console, or
    /// sent to a log.
    /// </remarks>
    /// <param name="message">The text that should be logged.</param>
    public delegate void Logger(string message);

    /// <summary>
    /// Contains methods for parsing raw text into a <see
    /// cref="MarkupParseResult"/>.
    /// </summary>
    public interface IMarkupParser
    {
        /// <summary>
        /// Parses a string into markup, given a locale.
        /// </summary>
        /// <param name="rawText">The text to parse.</param>
        /// <param name="localeCode">The locale to use when parsing the text.</param>
        /// <returns></returns>
        public MarkupParseResult ParseMarkup(string rawText, string localeCode);
    }

    /// <summary>
    /// Represents different kinds of variables that can be fetched from a <see
    /// cref="AsyncDialogue"/> using <see cref="IVariableAccess.TryGetValue{T}(string, out T)"/>.
    /// </summary>
    public enum VariableKind
    {
        /// <summary>
        /// The kind of the variable cannot be determined. It may not be known
        /// to the system.
        /// </summary>
        Unknown,
        /// <summary>
        /// The variable's value is stored in memory, and may be persisted to
        /// disk.
        /// </summary>
        Stored,
        /// <summary>
        /// The variable's value is computed at run-time, and is not persisted
        /// to disk. 
        /// </summary>
        Smart
    }

    /// <summary>Provides a mechanism for retrieving values.</summary>
    public interface IVariableAccess
    {
        /// <summary>
        /// Given a variable name, attempts to fetch a value for the variable,
        /// either from storage, initial values found in <see cref="Program"/>,
        /// or by evaluating a smart variable found in <see cref="Program"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to return. The fetched
        /// value will be converted to this type, if possible.</typeparam>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="result">If this method returns <see langword="true"/>,
        /// this parameter will contain the fetched value.</param>
        /// <returns><see langword="true"/> if a value could be fetched; <see
        /// langword="false"/> otherwise.</returns>
        bool TryGetValue<T>(string variableName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? result);

        /// <summary>
        /// Gets the kind of variable named <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <returns>A <see cref="VariableKind"/> enum representing the kind of
        /// the variable named <paramref name="name"/>.</returns>
        VariableKind GetVariableKind(string name);

        /// <summary>
        /// Gets or sets the Yarn <see cref="Program"/> that stores information
        /// about the initial values of variables, and is able to produce values
        /// for smart variables.
        /// </summary>
        Program? Program { get; set; }

        /// <summary>
        /// Gets or sets the object to use when evaluating smart variables.
        /// </summary>
        ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }
    }

    /// <summary>Provides a mechanism for storing values.</summary>
    public interface IVariableStorage : IVariableAccess
    {
        /// <summary>
        /// Stores a <see cref="string"/> in this VariableStorage.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="stringValue">The string to store.</param>
        void SetValue(string variableName, string stringValue);

        /// <summary>
        /// Stores a <see cref="float"/> in this VariableStorage.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="floatValue">The number to store.</param>
        void SetValue(string variableName, float floatValue);

        /// <summary>
        /// Stores a <see cref="bool"/> in this VariableStorage.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="boolValue">The boolean value to store.</param>
        void SetValue(string variableName, bool boolValue);

        /// <summary>
        /// Removes all variables from storage.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// A simple concrete implementation of <see cref="IVariableStorage"/>
    /// that keeps all variables in memory.
    /// </summary>
    public class MemoryVariableStore : IVariableStorage
    {
        private readonly Dictionary<string, object> variables = new Dictionary<string, object>();

        private static bool TryGetAsType<T>(Dictionary<string, object> dictionary, string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? result)
        {
            if (dictionary.TryGetValue(key, out var objectResult) == true
                && typeof(T).IsAssignableFrom(objectResult.GetType()))
            {
                result = (T)objectResult;
                return true;
            }

            result = default!;
            return false;
        }

        /// <inheritdoc/>
        public Program? Program { get; set; }

        /// <inheritdoc/>
        public ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }

        /// <inheritdoc/>
        public virtual bool TryGetValue<T>(string variableName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? result)
        {
            if (Program == null)
            {
                throw new InvalidOperationException($"Can't get variable {variableName}: {nameof(Program)} is null");
            }

            switch (GetVariableKind(variableName))
            {
                case VariableKind.Stored:
                    // This is a stored value. First, attempt to fetch it from the
                    // variable storage.

                    // Try to get the value from the dictionary, and check to see that it's the 
                    if (TryGetAsType(variables, variableName, out result))
                    {
                        // We successfully fetched it from storage.
                        return true;
                    }
                    else
                    {
                        // We didn't fetch it from storage. Fall back to the
                        // program's initial value storage.
                        return Program.TryGetInitialValue(variableName, out result);
                    }
                case VariableKind.Smart:
                    // The variable is a smart variable. Ask our smart variable
                    // evaluator.
                    if (SmartVariableEvaluator == null)
                    {
                        throw new InvalidOperationException($"Can't get variable {variableName}: {nameof(SmartVariableEvaluator)} is null");
                    }
                    return this.SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);
                case VariableKind.Unknown:
                default:
                    // The variable is not known.
                    result = default;
                    return false;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            this.variables.Clear();
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, string stringValue)
        {
            this.variables[variableName] = stringValue;
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, float floatValue)
        {
            this.variables[variableName] = floatValue;
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, bool boolValue)
        {
            this.variables[variableName] = boolValue;
        }

        /// <inheritdoc/>
        public VariableKind GetVariableKind(string name)
        {
            // Does this variable exist in our stored values?
            if (this.variables.ContainsKey(name))
            {
                return VariableKind.Stored;
            }
            if (this.Program == null)
            {
                // We don't have a Program, so we can't ask it for other
                // information.
                return VariableKind.Unknown;
            }
            // Ask our Program about it. It will be able to tell if the variable
            // is stored, smart, or unknown.
            return this.Program.GetVariableKind(name);
        }
    }

    /// <summary>
    /// Co-ordinates the execution of Yarn programs.
    /// </summary>
    /// <remarks>
    /// This type is basically a wrapper around the <see cref="AsyncVirtualMachine"/> and provides a nicer interface into manipulating the vm from the perspective of dialogue instead of a stack machine.
    /// </remarks>
    public class AsyncDialogue: ISmartVariableEvaluator, IDialogueResponder
    {
        /// <summary>
        /// Creates a new AsyncDialogue
        /// </summary>
        /// <param name="variableStorage">The varstore for this dialogue</param>
        /// <exception cref="ArgumentNullException">If the variable storage is null there is no way to create an a dialogue object</exception>
        public AsyncDialogue(IVariableStorage variableStorage)
        {
            this.VariableStorage = variableStorage ?? throw new ArgumentNullException(nameof(variableStorage));
            this.VariableStorage.SmartVariableEvaluator = this;
            this.VariableStorage.Program = this.Program;

            this.vm = new AsyncVirtualMachine(this.VariableStorage);
            this.vm.Responder = this;
            this.smartVariableVM = new AsyncVirtualMachine(this.VariableStorage);
        }

        /// <summary>
        /// The value to indicate to the dialogue runner that no option was selected and dialogue should fall through to the rest of the program.
        /// </summary>
        public const int NoOptionSelected = -1;

        /// <summary>
        /// The logger for debug messages, is forwarded to the VM
        /// </summary>
        public Logger? LogDebugMessage
        {
            get => vm.LogDebugMessage;
            set => vm.LogDebugMessage = value;
        }
        /// <summary>
        /// The logger for error messages, is forwarded to the VM
        /// </summary>
        public Logger? LogErrorMessage
        {
            get => vm.LogErrorMessage;
            set => vm.LogErrorMessage = value;
        }

        /// <summary>
        /// The variable storage used by the dialogue
        /// </summary>
        public IVariableStorage VariableStorage { get; set; }

        /// <summary>
        /// The Responder for this dialogue, this is how the dialogue informs the rest of the system of important events
        /// </summary>
        public IDialogueResponder? Responder { get; set; }

        /// <summary>
        /// The compiled Yarn program used by the dialogue.
        /// </summary>
        public Program? Program
        {
            get => vm?.Program;
            set
            {
                vm.Program = value;
                vm.ResetState();

                smartVariableVM.Program = value;
                smartVariableVM.ResetState();

                VariableStorage.Program = value;
            }
        }
        private readonly AsyncVirtualMachine smartVariableVM;

        /// <summary>
        /// What saliency content strategy does the dialogue use to select salient content?
        /// </summary>
        public Saliency.IContentSaliencyStrategy ContentSaliencyStrategy
        {
            get => this.vm.ContentSaliencyStrategy;
            set => this.vm.ContentSaliencyStrategy = value;
        }

        private AsyncVirtualMachine vm;

        /// <summary>
        /// Is true when dialogue is currently running, false otherwise.
        /// </summary>
        public bool IsActive => vm.IsDialogueRunning;

        /// <summary>
        /// Starts dialogue from a specific entry node and will run until completion of dialogue.
        /// </summary>
        /// <param name="node">The node to use as the entry point for the dialogue</param>
        public async ValueTask StartDialogue(string node)
        {
            await vm.SetNode(node);
            await vm.Start();
        }

        /// <summary>
        /// Starts dialogue at the value of <see cref="CurrentNode"/>.
        /// <seealso cref="StartDialogue(string)"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is no node currently set as the entry point this method throws</exception>
        public async ValueTask Start()
        {
            if (CurrentNode == null)
            {
                throw new InvalidOperationException("Asked to start dialogue but a start node has not been set");
            }
            await vm.Start();
        }

        /// <summary>
        /// Works out if the node is part of a node group or not.
        /// If the node isn't a valid node will always return false.
        /// </summary>
        /// <param name="nodeName">The node whos node groupedipidty you are curious about</param>
        /// <returns>true if the requested node is part of a node group</returns>
        /// <exception cref="InvalidOperationException">Throws if there is no program set</exception>
        public bool IsNodeGroup(string nodeName)
        {
            if (this.Program == null)
            {
                LogErrorMessage?.Invoke($"Can't determine if {nodeName} is a hub node, because no program has been set.");
                throw new InvalidOperationException($"Can't determine if {nodeName} is a hub node, because no program has been set.");
            }

            if (this.Program.Nodes.TryGetValue(nodeName, out var node) == false)
            {
                // Not a valid node, so not a valid node group.
                return false;
            }

            return node.IsNodeGroupHub;
        }
        
        /// <summary>
        /// Works out if the node has been visited.
        /// See also <seealso cref="GetNodeVisitCount(string)"/>
        /// </summary>
        /// <param name="nodeName">The node whos visitation you are curious about</param>
        /// <returns>True if the node has been visited at least once</returns>
        public bool IsNodeVisited(string nodeName)
        {
            return GetNodeVisitCount(nodeName) > 0;
        }
        /// <summary>
        /// Works out how many times the node has been visited.
        /// See also <seealso cref="IsNodeVisited(string)"/>
        /// </summary>
        /// <param name="nodeName">the node whos visitation count you are cirous about</param>
        /// <returns>0 if the node has never been visited, otherwise the number of times the node has been visited</returns>
        public float GetNodeVisitCount(string nodeName)
        {
            VariableStorage.TryGetValue(StandardLibrary.GenerateUniqueVisitedVariableForNode(nodeName), out float count);
            return count;
        }
        
        /// <summary>
        /// Determines if the requested node group has any content to show.
        /// </summary>
        /// <remarks>
        /// This may return false for any number of reasons including:
        /// <list type="bullet">
        /// <item> the requested node is not a node or nodegroup </item>
        /// <item> the node group has no elements </item>
        /// <item> the node group has been fully exhausted according to the <see cref="ContentSaliencyStrategy"/> </item>
        /// <item> there is only failing content according to the <see cref="ContentSaliencyStrategy"/> </item>
        /// </list>
        /// </remarks>
        /// <param name="nodeGroup">The node group to check for content</param>
        /// <returns>True if there is content to show, false otherwise</returns>
        public bool HasAnyContent(string nodeGroup)
        {
            if (this.Program == null)
            {
                // we somehow don't have a program, so we don't have ANY
                // content, let alone this specific content
                return false;
            }

            if (this.Program.Nodes.TryGetValue(nodeGroup, out var node) == false)
            {
                // No node with this name
                return false;
            }

            if (!node.IsNodeGroupHub)
            {
                // Not a node group hub, so it always has content available
                return true;
            }

            var options = this.GetSaliencyOptionsForNodeGroup(nodeGroup);
            var bestOption = this.ContentSaliencyStrategy.QueryBestContent(options);

            // Did the saliency strategy indicate that an option could be selected?
            return bestOption != null;
        }
        
        /// <summary>
        /// Returns the list of salient options inside a node groups
        /// </summary>
        /// <param name="nodeGroup">The node group you want the salient content from</param>
        /// <returns>Collection of salient content inside the node group, may be a single item if the requested nodegroup is just a single node</returns>
        /// <exception cref="ArgumentException">If the requested nodegroup isn't a node</exception>
        public IEnumerable<Saliency.ContentSaliencyOption> GetSaliencyOptionsForNodeGroup(string nodeGroup)
        {
            if (NodeExists(nodeGroup) == false)
            {
                // This node doesn't exist - it can't be a node OR a node group,
                // and we've been asked for an invalid value.
                LogErrorMessage?.Invoke($"{nodeGroup} is not a valid node name");
                throw new ArgumentException($"{nodeGroup} is not a valid node name");
            }

            if (IsNodeGroup(nodeGroup) == false)
            {
                // This is not a node group, it's a plain node. Return a single
                // content saliency "option" that represents this node.
                return new[] {
                    new Saliency.ContentSaliencyOption(nodeGroup) {
                        ComplexityScore = 0,
                        ContentType = Saliency.ContentSaliencyContentType.Node,
                        PassingConditionValueCount = 1,
                        FailingConditionValueCount = 0,
                    }
                };
            }

            // This is a valid node group name. Ask the saliency system to
            // produce the collection of options that could run.
            return SmartVariableEvaluationVirtualMachine.GetSaliencyOptionsForNodeGroup(nodeGroup, this.VariableStorage, this);
        }

        /// <summary>
        /// Determins if the requested node exists in the dialogue
        /// </summary>
        /// <param name="nodeName">the node whos existence you are curious about</param>
        /// <returns>True if the node exists, false otherwise</returns>
        public bool NodeExists(string nodeName)
        {
            if (this.Program == null)
            {
                LogDebugMessage?.Invoke("Tried to call NodeExists, but no program has been loaded!");
                return false;
            }

            if (this.Program.Nodes == null || this.Program.Nodes.Count == 0)
            {
                // No nodes? Then this node doesn't exist.
                return false;
            }

            return this.Program.Nodes.ContainsKey(nodeName);
        }

        /// <summary>
        /// Attempts to retrieve the requested smart variable's current value
        /// </summary>
        /// <typeparam name="T">The type of the smart varible, must match the type of the variable itself or be convertible to that type</typeparam>
        /// <param name="name">The name of the smart variable</param>
        /// <param name="result">the smart variable itself</param>
        /// <returns>True if it was possible to retrieve the smart variable</returns>
        public bool TryGetSmartVariable<T>(string name, out T result)
        {
            return SmartVariableEvaluationVirtualMachine.TryGetSmartVariable(
                name,
                this.VariableStorage,
                this,
                out result);
        }

        /// <inheritdoc />
        public ValueTask HandleLine(Line line, CancellationToken token)
        {
            LogDebugMessage?.Invoke($"Got line: {line.ID}");

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to handle a line but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }

            return Responder.HandleLine(line, token);
        }

        /// <inheritdoc />
        public ValueTask<int> HandleOptions(OptionSet options, CancellationToken token)
        {
            LogDebugMessage?.Invoke("Got Options");
            foreach (var option in options.Options)
            {
                LogDebugMessage?.Invoke($"Got option: {option.ID}");
            }

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to handle options but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }

            return Responder.HandleOptions(options, token);
        }

        /// <inheritdoc />
        public ValueTask HandleCommand(Command command, CancellationToken token)
        {
            LogDebugMessage?.Invoke($"Got a coomand: {command.Text}");

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to handle a command but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }

            return Responder.HandleCommand(command, token);
        }

        /// <inheritdoc />
        public ValueTask HandleNodeStart(string node, CancellationToken token)
        {
            LogDebugMessage?.Invoke($"node started: {node}");

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to handle node start but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }
            
            return Responder.HandleNodeStart(node, token);
        }

        /// <inheritdoc />
        public ValueTask HandleNodeComplete(string node, CancellationToken token)
        {
            LogDebugMessage?.Invoke($"node completed: {node}");

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to handle node complete but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }
            
            return Responder.HandleNodeComplete(node, token);
        }

        /// <inheritdoc />
        public ValueTask HandleDialogueComplete()
        {
            LogDebugMessage?.Invoke($"dialogue completed");

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to handle dialogue complete but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }
            
            return Responder.HandleDialogueComplete();
        }

        /// <inheritdoc />
        public ValueTask PrepareForLines(List<string> lineIDs, CancellationToken token)
        {
            if (lineIDs is null)
            {
                throw new ArgumentNullException(nameof(lineIDs));
            }
            LogDebugMessage?.Invoke($"preparing for {lineIDs.Count} lines");

            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to prepare for lines but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }

            return Responder.PrepareForLines(lineIDs, token);
        }

        /// <inheritdoc />
        public bool CanCallFunction(string functionName)
        {
            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to see if we can handle a function but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }
            return Responder.CanCallFunction(functionName);
        }
        /// <inheritdoc />
        public ValueTask<IConvertible> InvokeFunction(string functionName, IConvertible[] parameters, CancellationToken token)
        {
            if (Responder is null)
            {
                LogErrorMessage?.Invoke("Asked to invoke a function but have no responder set!");
                throw new InvalidOperationException($"{nameof(Responder)} is null!");
            }
            return Responder.InvokeFunction(functionName, parameters, token);
        }
        
        /// <summary>
        /// Wipes the current program
        /// </summary>
        public void UnloadAll()
        {
            Program = null;
        }

        /// <summary>
        /// Returns the current node, or null if one hasn't been set
        /// </summary>
        public string? CurrentNode
        {
            get
            {
                if (this.vm == null)
                {
                    return null;
                }
                else
                {
                    return this.vm.CurrentNodeName;
                }
            }
        }
        
        /// <summary>
        /// Sets the current node
        /// </summary>
        /// <param name="node">the requested node</param>
        public async ValueTask SetNode(string node)
        {
            await this.vm.SetNode(node, clearState: true);
        }
        
        /// <summary>
        /// Requests the current dialogue stop
        /// </summary>
        /// <remarks>
        /// This cancels the various tokens that are provided to all tasks so dialogue cancellation will percolate outwards from this point
        /// </remarks>
        public async ValueTask Stop()
        {
            await this.vm.Stop();
        }
        
        /// <summary>
        /// Returns all headers on the requested node.
        /// </summary>
        /// <remarks>
        /// This exists primarily so that you don't have to dig directly into the compiled protobuf types.
        /// </remarks>
        /// <param name="nodeName">The node whos headers you covet</param>
        /// <returns>The collection of headers as key-value pairs</returns>
        /// <exception cref="InvalidOperationException">Throws if there is no program set</exception>
        public IEnumerable<KeyValuePair<string, string>> GetHeaders(string nodeName)
        {
            if (this.Program == null)
            {
                throw new InvalidOperationException($"Can't get headers for node {nodeName}, because no program is set");
            }

            if (this.Program.Nodes.Count == 0)
            {
                throw new InvalidOperationException($"Can't get headers for node {nodeName}, because the program contains no nodes");
            }

            if (this.Program.Nodes.TryGetValue(nodeName, out var node) == false)
            {
                throw new InvalidOperationException($"Can't get headers for node {nodeName}: no node with this name was found");
            }
            var result = new List<KeyValuePair<string, string>>(node.Headers.Count);

            foreach (var header in node.Headers)
            {
                result.Add(new KeyValuePair<string, string>(header.Key.Trim(), header.Value.Trim()));
            }

            return result;
        }

        /// <summary>
        /// Returns a single value from a single node's header.
        /// </summary>
        /// <remarks>
        /// If you are requesting multiple headers it would be better to use <see cref="GetHeaders(string)"/> and filter that collection.
        /// </remarks>
        /// <param name="nodeName">The node whos header you covet</param>
        /// <param name="headerName">The header upon the requested node</param>
        /// <returns>The value of the requested header on the requested node, or null if either the node or header doesn't exist</returns>
        /// <exception cref="InvalidOperationException">Throws if there is no program set</exception>
        public string? GetHeaderValue(string nodeName, string headerName)
        {
            if (this.Program == null)
            {
                throw new InvalidOperationException($"Can't get headers for node {nodeName}, because no program is set");
            }

            if (this.Program.Nodes.Count == 0)
            {
                throw new InvalidOperationException($"Can't get headers for node {nodeName}, because the program contains no nodes");
            }

            if (this.Program.Nodes.TryGetValue(nodeName, out var node) == false)
            {
                throw new InvalidOperationException($"Can't get headers for node {nodeName}: no node with this name was found");
            }

            foreach (var header in node.Headers)
            {
                if (header.Key == headerName)
                {
                    return header.Value.Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the names of all nodes in the program.
        /// </summary>
        /// <remarks>
        /// Exists so that you don't have to go digging inside the protobuf yourself.
        /// </remarks>
        public IEnumerable<string> NodeNames
        {
            get
            {
                return this.Program?.Nodes.Keys ?? Array.Empty<string>();
            }
        }
    }
}
#pragma warning restore CA2007
