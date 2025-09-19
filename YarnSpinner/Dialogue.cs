/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Yarn.Markup;

#nullable enable

    /// <summary>
    /// A line of dialogue, sent from the <see cref="Dialogue"/> to the game.
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
    /// They are created by the <see cref="Dialogue"/> during program
    /// execution.</para>
    /// </remarks>
    /// <seealso cref="Dialogue.LineHandler"/>
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
    /// cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You typically do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.OptionsHandler"/>
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
            internal Option(Line line, int id, int destinationInstruction, bool isAvailable)
            {
                Line = line;
                ID = id;
                DestinationInstruction = destinationInstruction;
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
            /// When the user selects this option, this value should be
            /// used as the parameter for <see
            /// cref="Dialogue.SetSelectedOption(int)"/>.
            /// </remarks>
            public int ID { get; private set; }

            /// <summary>
            /// Gets the name of the node that will be run if this option
            /// is selected.
            /// </summary>
            /// <remarks>
            /// The value of this property not be valid if this is a
            /// shortcut option.
            /// </remarks>
            internal int DestinationInstruction { get; set; }

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
    /// A command, sent from the <see cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.CommandHandler"/>    
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
    /// information from a <see cref="Dialogue"/>.
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
    /// cref="Dialogue"/> using <see cref="IVariableAccess.TryGetValue{T}(string, out T)"/>.
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
    /// Represents the method that is called when the Dialogue delivers a <see
    /// cref="Line"/>.
    /// </summary>
    /// <param name="line">The <see cref="Line"/> that has been
    /// delivered.</param>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void LineHandler(Line line);

    /// <summary>
    /// Represents the method that is called when the Dialogue delivers an <see
    /// cref="OptionSet"/>.
    /// </summary>
    /// <param name="options">The <see cref="OptionSet"/> that has been
    /// delivered.</param>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void OptionsHandler(OptionSet options);

    /// <summary>
    /// Represents the method that is called when the Dialogue delivers a <see
    /// cref="Command"/>.
    /// </summary>
    /// <param name="command">The <see cref="Command"/> that has been
    /// delivered.</param>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void CommandHandler(Command command);

    /// <summary>
    /// Represents the method that is called when the Dialogue reaches the end
    /// of a node.
    /// </summary>
    /// <param name="completedNodeName">The name of the node.</param>
    /// <remarks>
    /// This method may be called multiple times over the course of code
    /// execution. A node being complete does not necessarily represent the end
    /// of the conversation.
    /// </remarks>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void NodeCompleteHandler(string completedNodeName);

    /// <summary>
    /// Represents the method that is called when the Dialogue begins executing
    /// a node.
    /// </summary>
    /// <param name="startedNodeName">The name of the node.</param>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void NodeStartHandler(string startedNodeName);

    /// <summary>
    /// Represents the method that is called when the dialogue has reached its
    /// end, and no more code remains to be run.
    /// </summary>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    public delegate void DialogueCompleteHandler();

    /// <summary>
    /// Represents the method that is called when the dialogue anticipates that
    /// it will deliver lines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method should begin preparing to run the lines. For example, if a
    /// game delivers dialogue via voice-over, the appropriate audio files
    /// should be loaded.
    /// </para>
    /// <para>
    /// This method serves to provide a hint to the game that a line _may_ be
    /// run. Not every line indicated in <paramref name="lineIDs"/> may end up
    /// actually running.
    /// </para>
    /// <para>
    /// This method may be called any number of times during a dialogue session.
    /// </para>
    /// </remarks>
    /// <param name="lineIDs">The collection of line IDs that may be delivered
    /// at some point soon.</param>
    public delegate void PrepareForLinesHandler(IEnumerable<string> lineIDs);

    /// <summary>
    /// Co-ordinates the execution of Yarn programs.
    /// </summary>
    public class Dialogue : ISmartVariableEvaluator
    {

        /// <summary>
        /// Gets or sets the object that provides access to storing and
        /// retrieving the values of variables.
        /// </summary>
        public IVariableStorage VariableStorage { get; set; }

        /// <summary>
        /// Invoked when the Dialogue needs to report debugging
        /// information.
        /// </summary>
        public Logger? LogDebugMessage { get; set; }

        /// <summary>
        /// Invoked when the Dialogue needs to report an error.
        /// </summary>
        public Logger? LogErrorMessage { get; set; }

        /// <summary>The node that execution will start from.</summary>
        public const string DefaultStartNodeName = "Start";

        /// <summary>Gets or sets the compiled Yarn program.</summary>
        internal Program? Program
        {
            get => vm?.Program;
            set
            {
                vm.Program = value;
                vm.ResetState();

                smartVariableVM.Program = value;
                smartVariableVM.ResetState();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Dialogue is currently executing
        /// Yarn instructions.
        /// </summary>
        public bool IsActive => vm.CurrentExecutionState != VirtualMachine.ExecutionState.Stopped;

        /// <summary>
        /// Gets or sets the <see cref="Yarn.LineHandler"/> that is called when
        /// a line is ready to be shown to the user.
        /// </summary>
        public LineHandler? LineHandler
        {
            get => vm.LineHandler;
            set => vm.LineHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.OptionsHandler"/> that is called
        /// when a set of options are ready to be shown to the user.
        /// </summary>
        /// <remarks>
        /// The Options Handler delivers an <see cref="OptionSet"/> to the game.
        /// Before <see cref="Continue"/> can be called to resume execution,
        /// <see cref="SetSelectedOption"/> must be called to indicate which
        /// <see cref="OptionSet.Option"/> was selected by the user. If <see
        /// cref="SetSelectedOption"/> is not called, an exception is thrown.
        /// </remarks>
        public OptionsHandler? OptionsHandler
        {
            get => vm.OptionsHandler;
            set => vm.OptionsHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.CommandHandler"/> that is called
        /// when a command is to be delivered to the game.
        /// </summary>
        public CommandHandler? CommandHandler
        {
            get => vm.CommandHandler;
            set => vm.CommandHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.NodeStartHandler"/> that is called
        /// when a node is started.
        /// </summary>
        public NodeStartHandler? NodeStartHandler
        {
            get => vm.NodeStartHandler;
            set => vm.NodeStartHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.NodeCompleteHandler"/> that is
        /// called when a node is complete.
        /// </summary>
        public NodeCompleteHandler? NodeCompleteHandler
        {
            get => vm.NodeCompleteHandler;
            set => vm.NodeCompleteHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.DialogueCompleteHandler"/> that is
        /// called when the dialogue reaches its end.
        /// </summary>
        public DialogueCompleteHandler? DialogueCompleteHandler
        {
            get => vm.DialogueCompleteHandler;
            set => vm.DialogueCompleteHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="PrepareForLinesHandler"/> that is called
        /// when the dialogue anticipates delivering some lines.
        /// </summary>
        /// <value></value>
        public PrepareForLinesHandler? PrepareForLinesHandler
        {
            get => vm.PrepareForLinesHandler;
            set => vm.PrepareForLinesHandler = value;
        }

        /// <summary>
        /// The virtual machine to use when running dialogue.
        /// </summary>
        private readonly VirtualMachine vm;

        /// <summary>
        /// The virtual machine to use when evaluating smart variables.
        /// </summary>
        /// <remarks>
        /// This is kept separate from the main VM in order to prevent
        /// evaluating smart variables from modifying the evaluation state of
        /// dialogue.
        /// </remarks>
        private readonly VirtualMachine smartVariableVM;

        /// <summary>
        /// Gets the <see cref="Yarn.Library"/> that this Dialogue uses to
        /// locate functions.
        /// </summary>
        /// <remarks>
        /// When the Dialogue is constructed, the Library is initialized with
        /// the built-in operators like <c>+</c>, <c>-</c>, and so on.
        /// </remarks>
        public Library Library { get; internal set; }

        /// <summary>
        /// Gets or sets the content saliency strategy used by this <see cref="Dialogue"/>.
        /// </summary>
        /// <remarks>
        /// A content saliency strategy is a class that implements <see
        /// cref="Yarn.Saliency.IContentSaliencyStrategy"/> and selects the most
        /// appropriate content in a line group, or any other situation where
        /// content saliency is relevant.
        /// </remarks>
        public Saliency.IContentSaliencyStrategy ContentSaliencyStrategy
        {
            get => this.vm.ContentSaliencyStrategy;
            set => this.vm.ContentSaliencyStrategy = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialogue"/> class.
        /// </summary>
        /// <param name="variableStorage">The <see
        /// cref="Yarn.IVariableStorage"/> that this Dialogue should
        /// use.</param>
        public Dialogue(Yarn.IVariableStorage variableStorage)
        {
            Library = new Library();

            this.VariableStorage = variableStorage ?? throw new ArgumentNullException(nameof(variableStorage));
            this.VariableStorage.SmartVariableEvaluator = this;
            this.VariableStorage.Program = this.Program;

            this.vm = new VirtualMachine(this.Library, this.VariableStorage);
            this.smartVariableVM = new VirtualMachine(this.Library, this.VariableStorage);

            Library.ImportLibrary(new StandardLibrary());

            Library.RegisterFunction("visited", delegate (string node)
            {
                return IsNodeVisited(node);
            });
            Library.RegisterFunction("visited_count", delegate (string node)
            {
                return GetNodeVisitCount(node);
            });
            Library.RegisterFunction("has_any_content", delegate (string nodeGroup)
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
            });
        }

        /// <summary>
        /// Loads all nodes from the provided <see cref="Yarn.Program"/>.
        /// </summary>
        /// <remarks>
        /// This method replaces any existing nodes have been loaded.
        /// </remarks>
        /// <param name="program">The <see cref="Yarn.Program"/> to use.</param>
        public void SetProgram(Program program)
        {
            this.Program = program;
            this.VariableStorage.Program = program;
        }

        /// <summary>
        /// Loads a compiled <see cref="Yarn.Program"/> from a file.
        /// </summary>
        /// <param name="fileName">The path of the file to load.</param>
        /// <remarks>
        /// <para>
        /// This method replaces the current value of <see cref="Program"/> with
        /// the result of loading the file.
        /// </para>
        /// <para>
        /// This method does not compile Yarn source. To compile Yarn source
        /// code into a <see cref="Yarn.Program"/>, Refer to the Yarn compiler.
        /// </para>
        /// </remarks>
        internal void LoadProgram(string fileName)
        {
            var bytes = File.ReadAllBytes(fileName);

            this.Program = Program.Parser.ParseFrom(bytes);
        }

        /// <summary>
        /// Prepares the <see cref="Dialogue"/> that the user intends to start
        /// running a node.
        /// </summary>
        /// <param name="startNode">The name of the node that will be run. The
        /// node have been loaded by calling <see
        /// cref="SetProgram(Program)"/>.</param>
        /// <remarks>
        /// <para>
        /// After this method is called, you call <see cref="Continue"/> to
        /// start executing it.
        /// </para>
        /// <para>
        /// If <see cref="PrepareForLinesHandler"/> has been set, it may be
        /// called when this method is invoked, as the Dialogue determines which
        /// lines may be delivered during the <paramref name="startNode"/>
        /// node's execution.
        /// </para>
        /// </remarks>
        /// <throws cref="DialogueException">Thrown when no node named
        /// <c>startNode</c> has been loaded.</throws>
        public void SetNode(string startNode = DefaultStartNodeName)
        {
            this.vm.SetNode(startNode, clearState: true);
        }

        /// <summary>
        /// Signals to the <see cref="Dialogue"/> that the user has selected a
        /// specified <see cref="OptionSet.Option"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// After the Dialogue delivers an <see cref="OptionSet"/>, this method
        /// must be called before <see cref="Continue"/> is called.
        /// </para>
        /// <para>
        /// The ID number that should be passed as the parameter to this method
        /// should be the <see cref="OptionSet.Option.ID"/> field in the <see
        /// cref="OptionSet.Option"/> that represents the user's selection.
        /// </para>
        /// </remarks>
        /// <param name="selectedOptionID">The ID number of the Option that the
        /// user selected.</param>
        /// <throws cref="DialogueException">Thrown when the Dialogue is not
        /// expecting an option to be selected.</throws> <throws
        /// cref="ArgumentOutOfRangeException">Thrown when <c>selectedOptionID</c> is
        /// not a valid option ID.</throws>
        /// <seealso cref="Yarn.OptionsHandler"/>
        /// <seealso cref="OptionSet"/>
        /// <seealso cref="Continue"/>
        public void SetSelectedOption(int selectedOptionID)
        {
            this.vm.SetSelectedOption(selectedOptionID);
        }

        /// <summary>
        /// Starts, or continues, execution of the current Program.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method repeatedly executes instructions until one of the
        /// following conditions is encountered:
        /// </para>
        /// <list type="bullet">
        /// <item>The <see cref="LineHandler"/> or <see cref="CommandHandler"/>
        /// is called. After calling either of these handlers, the Dialogue will
        /// wait until <see cref="Continue"/> is called. Continue may be called
        /// from inside the <see cref="LineHandler"/> or <see
        /// cref="CommandHandler"/>, or may be called at any future time.</item>
        ///
        /// <item>The <see cref="OptionsHandler"/> is called. When this occurs,
        /// the Dialogue is waiting for the user to specify which of the options
        /// has been selected, and <see cref="SetSelectedOption(int)"/> must be
        /// called before <see cref="Continue"/> is called again.)</item>
        ///
        /// <item>The Program reaches its end. When this occurs, <see
        /// cref="SetNode(string)"/> must be called before <see
        /// cref="Continue"/> is called again.</item>
        ///
        /// <item>An error occurs while executing the Program.</item>
        /// </list>
        ///
        /// <para>This method has no effect if it is called while the <see
        /// cref="Dialogue"/> is currently in the process of executing
        /// instructions.</para>
        /// </remarks>
        /// <seealso cref="Yarn.LineHandler"/>
        /// <seealso cref="Yarn.OptionsHandler"/>
        /// <seealso cref="Yarn.CommandHandler"/>
        /// <seealso cref="Yarn.NodeCompleteHandler"/>
        /// <seealso cref="Yarn.DialogueCompleteHandler"/>
        public void Continue()
        {
            if (this.vm.CurrentExecutionState == VirtualMachine.ExecutionState.Running)
            {
                // Cannot 'continue' an already running VM.
                return;
            }

            this.vm.Continue();
        }

        /// <summary>
        /// Immediately stops the <see cref="Dialogue"/>.
        /// </summary>
        public void Stop()
        {
            if (this.vm != null)
            {
                this.vm.Stop();
            }
        }

        /// <summary>
        /// Gets the names of the nodes in the currently loaded Program.
        /// </summary>
        /// <remarks>If no program is currently loaded, an empty collection is
        /// returned.</remarks>
        public IEnumerable<string> NodeNames
        {
            get
            {
                return this.Program?.Nodes.Keys ?? Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets the name of the node that this Dialogue is currently executing.
        /// </summary>
        /// <remarks>If <see cref="Continue"/> has never been called, this value
        /// will be <see langword="null"/>.</remarks>
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
        /// Returns the string ID that contains the original, uncompiled source
        /// text for a node.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The string ID.</returns>
        /// <remarks>
        /// <para>
        /// A node's source text will only be present in the string table if its
        /// <c>tags</c> header contains <c>rawText</c>.
        /// </para>
        /// <para>
        /// Because the <see cref="Dialogue"/> class is designed to be unaware
        /// of the contents of the string table, this method does not test to
        /// see if the string table contains an entry with the line ID. You will
        /// need to test for that yourself.
        /// </para>
        /// </remarks>
        public string? GetStringIDForNode(string nodeName)
        {
            if (this.Program?.Nodes.Count == 0)
            {
                this.LogErrorMessage?.Invoke("No nodes are loaded!");
                return null;
            }
            else if (this.Program?.Nodes.ContainsKey(nodeName) ?? false)
            {
                return "line:" + nodeName;
            }
            else
            {
                this.LogErrorMessage?.Invoke("No node named " + nodeName);
                return null;
            }
        }

        /// <summary>
        /// Returns the tags for the node <paramref name="nodeName"/>.
        /// </summary>
        /// <remarks>
        /// The tags for a node are defined by setting the <c>tags</c> header in
        /// the node's source code. This header must be a space-separated list.
        /// </remarks>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The node's tags, or <see langword="null"/> if the node is
        /// not present in the Program.</returns>
        [Obsolete("Use GetHeaderValue(nodeName, \"tags\"), and split the result by spaces", true)]
        public IEnumerable<string> GetTagsForNode(string nodeName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the value of the header named <paramref name="headerName"/> on
        /// the node named <paramref name="nodeName"/>, or <see
        /// langword="null"/> if the header can't be found.
        /// </summary>
        /// <remarks>If the node has more than one header named <paramref
        /// name="headerName"/>, the first one is used.</remarks>
        /// <param name="nodeName">The name of the node.</param>
        /// <param name="headerName">The name of the header.</param>
        /// <returns>The value of the first header on the node with the
        /// specified header value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the program
        /// is not loaded, the program contains no nodes, or the program does
        /// not contain a node named <paramref name="nodeName"/>.</exception>
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
        /// Gets the collection of headers present on the node named <paramref
        /// name="nodeName"/>.
        /// </summary>
        /// <param name="nodeName">The name of the node to get headers
        /// for.</param>
        /// <returns>A collection of key-values pairs, each one representing a
        /// header on the node.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the program
        /// is not loaded, the program contains no nodes, or the program does
        /// not contain a node named <paramref name="nodeName"/>.</exception>
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
        /// Unloads all nodes from the Dialogue.
        /// </summary>
        public void UnloadAll()
        {
            Program = null;
        }

        /// <summary>
        /// Gets a value indicating whether a specified node exists in the
        /// Program.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns><see langword="true"/> if a node named <c>nodeName</c>
        /// exists in the Program, <see langword="false"/>
        /// otherwise.</returns>
        public bool NodeExists(string nodeName)
        {
            if (this.Program == null)
            {
                this.LogErrorMessage?.Invoke("Tried to call NodeExists, but no program has been loaded!");
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
        /// Begins analysis of the <see cref="Program"/> by the <paramref name="context"/>
        /// </summary>
        /// <param name="context">The Context that performs the analysis</param>
        internal void Analyse(Analysis.Context context)
        {
            if (context == null || this.Program == null)
            {
                // can't perform analysis on nothing
                return;
            }
            context.AddProgramToAnalysis(this.Program);
        }

        private bool IsNodeVisited(string nodeName)
        {
            return GetNodeVisitCount(nodeName) > 0;
        }
        private float GetNodeVisitCount(string nodeName)
        {
            VariableStorage.TryGetValue(Library.GenerateUniqueVisitedVariableForNode(nodeName), out float count);
            return count;
        }

        /// <inheritdoc />
        public bool TryGetSmartVariable<T>(string name, out T result)
        {
            return SmartVariableEvaluationVirtualMachine.TryGetSmartVariable(
                name,
                this.VariableStorage,
                this.Library,
                out result);
        }

        /// <summary>
        /// Queries the <see cref="Dialogue"/> for what content could possibly
        /// run if the node group nodeGroup was run.
        /// </summary>
        /// <remarks>
        /// <para>This method evaluates all nodes in the given node group, and
        /// returns a <see cref="Saliency.ContentSaliencyOption"/> object for
        /// each node. This object contains the current number of passing and
        /// failing 'when' clauses on the node, as well as the complexity score
        /// for that node. This is the same information that's passed to a <see
        /// cref="Saliency.IContentSaliencyStrategy"/> object's <see
        /// cref="Saliency.IContentSaliencyStrategy.QueryBestContent(IEnumerable{Saliency.ContentSaliencyOption})"/>
        /// method. This method is read-only, and calling it will not modify any
        /// variable state.
        /// </para>
        /// <para>Note that this method does not filter its output, and may
        /// include content options whose <see
        /// cref="Saliency.ContentSaliencyOption.FailingConditionValueCount"/>
        /// is greater than zero. It's up to the caller of this function to
        /// filter out these options if they're not wanted.</para> 
        /// <para>
        /// This method can be used to see if <em>any</em> content will appear
        /// when a given node group is run. If the collection returned by this
        /// method is empty, then running this node group will not result in any
        /// content. This can be used, for example, to decide whether to show a
        /// 'character can be spoken to' indicator. You can also examine the
        /// individal <see cref="Saliency.ContentSaliencyOption"/> objects to
        /// see if any content is available that passes a filter, such as
        /// whether content might appear that has a user-defined 'plot critical'
        /// tag.
        /// </para> 
        /// </remarks>
        /// <param name="nodeGroup">The name of the node group to get available
        /// content for.</param>
        /// <returns>A collection of <see
        /// cref="Saliency.ContentSaliencyOption"/> objects that may appear if
        /// and when the node group <paramref name="nodeGroup"/> is run.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref
        /// name="nodeGroup"/> is not a valid node name.</exception>
        public IEnumerable<Saliency.ContentSaliencyOption> GetSaliencyOptionsForNodeGroup(string nodeGroup)
        {
            if (NodeExists(nodeGroup) == false)
            {
                // This node doesn't exist - it can't be a node OR a node group,
                // and we've been asked for an invalid value.
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
            return SmartVariableEvaluationVirtualMachine.GetSaliencyOptionsForNodeGroup(nodeGroup, this.VariableStorage, this.Library);
        }

        /// <summary>
        /// Returns if the node group has any potential nodes to be run based on the current salient selector.
        /// </summary>
        /// <param name="nodeGroup">The name of the node group.</param>
        /// <returns>True if there is any salient content for the requested node group</returns>
        public bool HasSalientContent(string nodeGroup)
        {
            var options = GetSaliencyOptionsForNodeGroup(nodeGroup);
            var best = ContentSaliencyStrategy.QueryBestContent(options);
            return best != null;
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="nodeName"/> is the
        /// name of a valid node group in the program.
        /// </summary>
        /// <param name="nodeName">The name of the node group to check.</param>
        /// <returns><see langword="true"/> if <paramref name="nodeName"/> is
        /// the name of a node group; <see langword="false"/>
        /// otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see
        /// cref="Program"/> is null.</exception>
        public bool IsNodeGroup(string nodeName)
        {
            if (this.Program == null)
            {
                throw new InvalidOperationException($"Can't determine if {nodeName} is a hub node, because no program has been set.");
            }

            if (this.Program.Nodes.TryGetValue(nodeName, out var node) == false)
            {
                // Not a valid node, so not a valid node group.
                return false;
            }

            return node.IsNodeGroupHub;
        }

        // The standard, built-in library of functions and operators.
        internal class StandardLibrary : Library
        {
            /// <summary>
            /// The internal random number generator used by functions like
            /// 'random' and 'dice'.
            /// </summary>
            private static readonly System.Random Random = new Random();

            public StandardLibrary()
            {
                #region Operators

                // Register the in-built conversion functions
                this.RegisterFunction("string", delegate (object v)
                {
                    return Convert.ToString(v, System.Globalization.CultureInfo.CurrentCulture);
                });

                this.RegisterFunction("number", delegate (object v)
                {
                    return Convert.ToSingle(v, System.Globalization.CultureInfo.CurrentCulture);
                });

                this.RegisterFunction("format_invariant", delegate (float v)
                {
                    return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                });

                this.RegisterFunction("bool", delegate (object v)
                {
                    return Convert.ToBoolean(v, System.Globalization.CultureInfo.CurrentCulture);
                });

                // Register the built-in types.
                this.RegisterMethods((TypeBase)Types.Number);
                this.RegisterMethods((TypeBase)Types.String);
                this.RegisterMethods((TypeBase)Types.Boolean);

                // NOTE: This is part of a workaround for supporting 'EqualTo'
                // in Enums. See note in TypeUtil.GetCanonicalNameForMethod.
                this.RegisterMethods(new EnumType("Enum", "default", new AnyType()));

                // Register the built-in utility functions

#pragma warning disable CA5394 // System.Random is cryptographically insecure
                this.RegisterFunction<float>("random", () =>
                {
                    return (float)Random.NextDouble();
                });

                this.RegisterFunction("random_range", (float min, float max) =>
                {
                    return Random.Next((int)max - (int)min + 1) + min;
                });

                this.RegisterFunction("random_range_float", delegate (float minInclusive, float maxInclusive)
                {
                    return Random.Next((int)maxInclusive - (int)minInclusive + 1) + minInclusive;
                });

                this.RegisterFunction<int, int>("dice", (int sides) =>
                {
                    return Random.Next(sides) + 1;
                });
#pragma warning restore CA5394 // System.Random is cryptographically insecure

                this.RegisterFunction("min", (float a, float b) => Math.Min(a, b));
                this.RegisterFunction("max", (float a, float b) => Math.Max(a, b));

                this.RegisterFunction<float, int>("round", (float num) =>
                {
                    return (int)Math.Round(num);
                });

                this.RegisterFunction<float, int, float>("round_places", (float num, int places) =>
                {
                    return (float)Math.Round(num, places);
                });

                this.RegisterFunction<float, int>("floor", (float num) =>
                {
                    return (int)Math.Floor(num);
                });

                this.RegisterFunction<float, int>("ceil", (float num) =>
                {
                    return (int)Math.Ceiling(num);
                });

                this.RegisterFunction<float, int>("inc", (float value) =>
                {
                    if (Decimal(value) == 0)
                    {
                        return (int)(value + 1);
                    }
                    else
                    {
                        return (int)Math.Ceiling(value);
                    }
                });

                this.RegisterFunction<float, int>("dec", (float value) =>
                {
                    if (Decimal(value) == 0)
                    {
                        return (int)value - 1;
                    }
                    else
                    {
                        return (int)Math.Floor(value);
                    }
                });

                this.RegisterFunction<float, float>("decimal", Decimal);
                this.RegisterFunction<float, int>("int", Integer);

                this.RegisterFunction("format", delegate (string formatString, object argument)
                {
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture, formatString, argument);
                });

                #endregion Operators
            }

            private static float Decimal(float value)
            {
                return value - Integer(value);

            }
            private static int Integer(float value)
            {
                return (int)Math.Truncate(value);
            }
        }
    }
}
