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

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Yarn.Unity
{

    /// <summary>
    /// The [DialogueRunner]({{|ref
    /// "/docs/unity/components/dialogue-runner.md"|}}) component acts as
    /// the interface between your game and Yarn Spinner.
    /// </summary>
    [AddComponentMenu("Scripts/Yarn Spinner/Dialogue Runner")]
    public class DialogueRunner : MonoBehaviour
    {
        /// <summary>
        /// The <see cref="YarnProgram"/> assets that should be loaded on
        /// scene start.
        /// </summary>
        public YarnProgram[] yarnScripts;

        /// <summary>
        /// The variable storage object.
        /// </summary>
        public VariableStorageBehaviour variableStorage;

        /// <summary>
        /// The object that will handle the actual display and user input.
        /// </summary>
        public DialogueViewBase[] dialogueViews;

        /// <summary>The name of the node to start from.</summary>
        /// <remarks>
        /// This value is used to select a node to start from when
        /// <see cref="StartDialogue"/> is called.
        /// </remarks>
        public string startNode = Yarn.Dialogue.DEFAULT_START;

        /// <summary>
        /// Whether the DialogueRunner should automatically start running
        /// dialogue after the scene loads.
        /// </summary>
        /// <remarks>
        /// The node specified by <see cref="startNode"/> will be used.
        /// </remarks>
        public bool startAutomatically = true;

        /// <summary>
        /// Whether the DialogueRunner should automatically proceed to the
        /// next line once a line has been finished.
        /// </summary>
        public bool continueNextLineOnLineFinished;

#if ADDRESSABLES
        /// <summary>
        /// Whether the DialogueRunner should wait for the linked 
        /// Addressable voice over AudioClips to finish loading (true)
        /// or not (false).
        /// </summary>
        public bool waitForAddressablesLoaded = true;
#endif

        /// <summary>
        /// Gets a value that indicates if the dialogue is actively running.
        /// </summary>
        public bool IsDialogueRunning { get; set; }

        /// <summary>
        /// A type of <see cref="UnityEvent"/> that takes a single string
        /// parameter. 
        /// </summary>
        /// <remarks>
        /// A concrete subclass of <see cref="UnityEvent"/> is needed in
        /// order for Unity to serialise the type correctly.
        /// </remarks>
        [Serializable]
        public class StringUnityEvent : UnityEvent<string> { }

        /// <summary>
        /// A Unity event that is called when a node starts running.
        /// </summary>
        /// <remarks>
        /// This event receives as a parameter the name of the node that is
        /// about to start running.
        /// </remarks>
        /// <seealso cref="Dialogue.NodeStartHandler"/>
        public StringUnityEvent onNodeStart;
        
        /// <summary>
        /// A Unity event that is called when a node is complete.
        /// </summary>
        /// <remarks>
        /// This event receives as a parameter the name of the node that
        /// just finished running.
        /// </remarks>
        /// <seealso cref="Dialogue.NodeCompleteHandler"/>
        public StringUnityEvent onNodeComplete;

        /// <summary>
        /// A Unity event that is called once the dialogue has completed.
        /// </summary>
        /// <seealso cref="Dialogue.DialogueCompleteHandler"/>
        public UnityEvent onDialogueComplete;

        /// <summary>
        /// Gets the name of the current node that is being run.
        /// </summary>
        /// <seealso cref="Dialogue.currentNode"/>
        public string CurrentNodeName => Dialogue.currentNode;

        /// <summary>
        /// Gets the underlying <see cref="Dialogue"/> object that runs the
        /// Yarn code.
        /// </summary>
        public Dialogue Dialogue => dialogue ?? (dialogue = CreateDialogueInstance());

        /// <summary>
        /// Adds a program, and parses and adds the contents of the
        /// program's string table to the DialogueRunner's combined string table.
        /// </summary>
        /// <remarks>This method calls <see
        /// cref="AddDialogueLines(YarnProgram)"/> to load the string table
        /// for the current localisation. It selects the appropriate string
        /// table based on the value of set in the Preferences dialogue.
        public void Add(YarnProgram scriptToLoad)
        {
            Dialogue.AddProgram(scriptToLoad.GetProgram());
            AddDialogueLines(scriptToLoad);
            
            // Keep reference to loaded script so we can reload upon language changes
            var additionalYarnScripts = yarnScripts.ToList();
            additionalYarnScripts.Add(scriptToLoad);
            yarnScripts = additionalYarnScripts.ToArray();
        }

        /// <summary>
        /// Starts running dialogue. The node specified by <see
        /// cref="startNode"/> will start running.
        /// </summary>
        public void StartDialogue() => StartDialogue(startNode);

        /// <summary>
        /// Start the dialogue from a specific node.
        /// </summary>
        /// <param name="startNode">The name of the node to start running
        /// from.</param>
        public void StartDialogue(string startNode)
        {
            // Stop any processes that might be running already
            foreach (var dialogueView in dialogueViews) {
                dialogueView.StopAllCoroutines();
            }

            // Get it going
            RunDialogue();
            void RunDialogue()
            {
                // Mark that we're in conversation.
                IsDialogueRunning = true;

                // Signal that we're starting up.
                foreach (var dialogueView in dialogueViews) {
                    dialogueView.DialogueStarted();
                }

                Dialogue.SetNode(startNode);
#if ADDRESSABLES
                if (waitForAddressablesLoaded) {
                    if (WaitForAddressablesLoadedCoroutine == null) {
                        WaitForAddressablesLoadedCoroutine = StartCoroutine(CheckAddressablesLoaded(ContinueDialogue));
                    }
                } else {
#endif
                    ContinueDialogue();
#if ADDRESSABLES
                }
#endif
            }
        }

        /// <summary>
        /// Resets the <see cref="variableStorage"/>, and starts running the dialogue again from the node named <see cref="startNode"/>.
        /// </summary>        
        public void ResetDialogue()
        {
            variableStorage.ResetToDefaults();
            StartDialogue();
        }

        /// <summary>
        /// Unloads all nodes from the <see cref="dialogue"/>.
        /// </summary>
        public void Clear()
        {
            Assert.IsFalse(IsDialogueRunning, "You cannot clear the dialogue system while a dialogue is running.");
            Dialogue.UnloadAll();
        }

        /// <summary>
        /// Stops the <see cref="dialogue"/>.
        /// </summary>
        public void Stop()
        {
            IsDialogueRunning = false;
            Dialogue.Stop();
        }

        /// <summary>
        /// Returns `true` when a node named `nodeName` has been loaded.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>`true` if the node is loaded, `false` otherwise/</returns>
        public bool NodeExists(string nodeName) => Dialogue.NodeExists(nodeName);

        /// <summary>
        /// Returns the collection of tags that the node associated with
        /// the node named `nodeName`.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The collection of tags associated with the node, or
        /// `null` if no node with that name exists.</returns>
        public IEnumerable<string> GetTagsForNode(String nodeName) => Dialogue.GetTagsForNode(nodeName);

        /// <summary>
        /// Adds a command handler. Dialogue will continue running after the command is called.
        /// </summary>
        /// <remarks>
        /// When this command handler has been added, it can be called from
        /// your Yarn scripts like so:
        ///
        /// <![CDATA[
        /// ```yarn
        /// <<commandName param1 param2>>
        /// ```
        /// ]]>
        ///
        /// When this command handler is called, the DialogueRunner will
        /// not stop executing code.
        /// </remarks>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="handler">The <see cref="CommandHandler"/> that will be invoked when the command is called.</param>
        public void AddCommandHandler(string commandName, CommandHandler handler)
        {
            if (commandHandlers.ContainsKey(commandName) || blockingCommandHandlers.ContainsKey(commandName)) {
                Debug.LogError($"Cannot add a command handler for {commandName}: one already exists");
                return;
            }
            commandHandlers.Add(commandName, handler);
        }

        /// <summary>
        /// Adds a command handler. Dialogue will pause execution after the
        /// command is called.
        /// </summary>
        /// <remarks>
        /// When this command handler has been added, it can be called from
        /// your Yarn scripts like so:
        ///
        /// <![CDATA[
        /// ```yarn
        /// <<commandName param1 param2>>
        /// ```
        /// ]]>
        ///
        /// When this command handler is called, the DialogueRunner will
        /// stop executing code. The <see cref="BlockingCommandHandler"/>
        /// will receive an <see cref="Action"/> to call when it is ready
        /// for the Dialogue Runner to resume executing code.
        /// </remarks>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="handler">The <see cref="CommandHandler"/> that
        /// will be invoked when the command is called.</param>
        public void AddCommandHandler(string commandName, BlockingCommandHandler handler)
        {
            if (commandHandlers.ContainsKey(commandName) || blockingCommandHandlers.ContainsKey(commandName)) {
                Debug.LogError($"Cannot add a command handler for {commandName}: one already exists");
                return;
            }
            blockingCommandHandlers.Add(commandName, handler);
        }

        /// <summary>
        /// Removes a command handler.
        /// </summary>
        /// <param name="commandName">The name of the command to remove.</param>
        public void RemoveCommandHandler(string commandName)
        {
            commandHandlers.Remove(commandName);
            blockingCommandHandlers.Remove(commandName);
        }

        /// <summary>
        /// Add a new function that returns a value, so that it can be
        /// called from Yarn scripts.
        /// </summary>        
        /// <inheritdoc cref="AddFunction(string, int, Function)"/>
        /// <remarks>
        /// If `parameterCount` is -1, the function expects any number of
        /// parameters.
        ///
        /// When this function has been registered, it can be called from
        /// your Yarn scripts like so:
        /// 
        /// <![CDATA[
        /// ```yarn
        /// <<if myFunction(1, 2) == true>>
        ///     myFunction returned true!
        /// <<endif>>
        /// ```
        /// ]]>
        /// 
        /// The `call` command can also be used to invoke the function:
        /// 
        /// <![CDATA[
        /// ```yarn
        /// <<call myFunction(1, 2)>>
        /// ```
        /// ]]>    
        /// </remarks>
        /// <param name="implementation">The <see cref="ReturningFunction"/>
        /// that should be invoked when this function is called.</param>
        /// <seealso cref="AddFunction(string, int, Function)"/>
        /// <seealso cref="AddFunction(string, int, ReturningFunction)"/>
        /// <seealso cref="Library"/> 
        /// <inheritdoc cref="AddFunction(string, int, Function)"/>       
        public void AddFunction(string name, int parameterCount, ReturningFunction implementation)
        {
            if (Dialogue.library.FunctionExists(name)) {
                Debug.LogError($"Cannot add function {name} one already exists");
                return;
            }

            Dialogue.library.RegisterFunction(name, parameterCount, implementation);
        }

        /// <summary>
        /// Add a new function, so that it can be called from Yarn scripts.
        /// </summary>
        /// <remarks>
        /// If `parameterCount` is -1, the function expects any number of
        /// parameters.
        ///
        /// When this function has been registered, it can be invoked using
        /// the `call` command:
        ///
        /// <![CDATA[
        /// ```yarn
        /// <<call myFunction(1, 2)>>
        /// ```
        /// ]]>    
        /// </remarks>
        /// <param name="name">The name of the function to add.</param>
        /// <param name="parameterCount">The number of parameters that this
        /// function expects.</param>
        /// <param name="implementation">The <see cref="Function"/>
        /// that should be invoked when this function is called.</param>
        /// <seealso cref="AddFunction(string, int, Function)"/>
        /// <seealso cref="AddFunction(string, int, ReturningFunction)"/>
        /// <seealso cref="Library"/>        
        public void AddFunction(string name, int parameterCount, Function implementation)
        {
            if (Dialogue.library.FunctionExists(name)) {
                Debug.LogError($"Cannot add function {name} one already exists");
                return;
            }

            Dialogue.library.RegisterFunction(name, parameterCount, implementation);
        }

        /// <summary>
        /// Remove a registered function.
        /// </summary>
        /// <remarks>
        /// After a function has been removed, it cannot be called from Yarn scripts.
        /// </remarks>
        /// <param name="name">The name of the function to remove.</param>
        /// <seealso cref="AddFunction(string, int, Function)"/>
        /// <seealso cref="AddFunction(string, int, ReturningFunction)"/>
        public void RemoveFunction(string name) => Dialogue.library.DeregisterFunction(name);

        #region Private Properties/Variables/Procedures

        List<DialogueViewBase> lineCurrentlyRunOnDialogueViews = new List<DialogueViewBase>();
        Action<int> selectAction;

        /// <summary>
        /// Represents a method that can be called when the DialogueRunner
        /// encounters a command. 
        /// </summary>
        /// <remarks>
        /// After this method returns, the DialogueRunner will continue
        /// executing code.
        /// </remarks>
        /// <param name="parameters">The list of parameters that this
        /// command was invoked with.</param>
        /// <seealso cref="AddCommandHandler(string, CommandHandler)"/>
        /// <seealso cref="AddCommandHandler(string,
        /// BlockingCommandHandler)"/>
        public delegate void CommandHandler(string[] parameters);

        /// <summary>
        /// Represents a method that can be called when the DialogueRunner
        /// encounters a command. 
        /// </summary>
        /// <remarks>
        /// After this method returns, the DialogueRunner will pause
        /// executing code. The `onComplete` delegate will cause the
        /// DialogueRunner to resume executing code.
        /// </remarks>
        /// <param name="parameters">The list of parameters that this
        /// command was invoked with.</param>
        /// <param name="onComplete">The method to call when the DialogueRunner should continue executing code.</param>
        /// <seealso cref="AddCommandHandler(string, CommandHandler)"/>
        /// <seealso cref="AddCommandHandler(string, BlockingCommandHandler)"/>
        public delegate void BlockingCommandHandler(string[] parameters, Action onComplete);

        /// Maps the names of commands to action delegates.
        Dictionary<string, CommandHandler> commandHandlers = new Dictionary<string, CommandHandler>();
        Dictionary<string, BlockingCommandHandler> blockingCommandHandlers = new Dictionary<string, BlockingCommandHandler>();

        /// <summary>
        /// Collection of dialogue lines linked with their corresponding Yarn linetags.
        /// </summary>
        Dictionary<string, string> localizedText = new Dictionary<string, string>();
        /// <summary>
        /// Collection of voice over AudioClips linked with their corresponding Yarn linetags.
        /// </summary>
        Dictionary<string, AudioClip> localizedAudio = new Dictionary<string, AudioClip>();

        // A flag used to note when we call into a blocking command
        // handler, but it calls its complete handler immediately -
        // _before_ the Dialogue is told to pause. This out-of-order
        // problem can lead to the Dialogue being stuck in a paused state.
        // To solve this, this variable is set to false before any blocking
        // command handler is called, and set to true when ContinueDialogue
        // is called. If it's true after calling a blocking command
        // handler, then the Dialogue is not told to pause.
        bool wasCompleteCalled = false;

        /// <summary>
        /// A flag caching if a View has communicated the user's intent to proceed to the next line
        /// </summary>
        bool userIntendedNextLine = false;

        /// <summary>
        /// List of Addressable voice over AudioClips being currently loaded.
        /// </summary>
        private List<Task> addressableVoiceOverLoadingTasks = new List<Task>();

        /// <summary>
        /// Instance of coroutine checking whether all Addressable loading tasks finished.
        /// </summary>
        private Coroutine WaitForAddressablesLoadedCoroutine;


        /// Our conversation engine
        /** Automatically created on first access
         */
        Dialogue dialogue;

        /// Start the dialogue
        void Start()
        {
            Assert.IsNotNull(dialogueViews, "No View class (like DialogueUI) was given! Can't run the dialogue without a View class!");
            Assert.IsNotNull(variableStorage, "Variable storage was not set! Can't run the dialogue!");

            // Ensure that the variable storage has the right stuff in it
            variableStorage.ResetToDefaults();

            // Combine all scripts together and load them
            if (yarnScripts != null && yarnScripts.Length > 0) {

                var compiledPrograms = new List<Program>();

                foreach (var program in yarnScripts) {
                    compiledPrograms.Add(program.GetProgram());
                }

                var combinedProgram = Program.Combine(compiledPrograms.ToArray());

                Dialogue.SetProgram(combinedProgram);
            }

            if (startAutomatically) {
                StartDialogue();
            }
        }

        private void OnEnable() {
            Preferences.LanguagePreferencesChanged += OnLanguagePreferencesChanged;
        }

        private void OnDisable() {
            Preferences.LanguagePreferencesChanged -= OnLanguagePreferencesChanged;
        }

        /// <summary>
        /// Parses and adds the contents of a string table from a yarn
        /// asset to the DialogueRunner's combined string table.
        /// </summary>
        /// <remarks>
        /// <see cref="YarnProgram"/>s contain at least one string table,
        /// stored in <see
        /// cref="YarnProgram.baseLocalisationStringTable"/>. String tables
        /// are <see cref="TextAsset"/>s that contain comma-separated value
        /// formatted text.
        ///
        /// A <see cref="YarnProgram"/> may have more string tables beyond
        /// the base localisation in its <see
        /// cref="YarnProgram.localizations"/>. 
        ///
        /// </remarks>
        /// <param name="yarnScript">The <see cref="YarnProgram"/> to get
        /// the string table from.</param>
        private void AddDialogueLines(YarnProgram yarnScript) {
            foreach (var yarnLine in yarnScript.GetStringTable()) {
                localizedText.Add(yarnLine.Key, yarnLine.Value);
            }

#if ADDRESSABLES
            if (ProjectSettings.AddressableVoiceOverAudioClips) {
                if (waitForAddressablesLoaded) {
                    addressableVoiceOverLoadingTasks.Add(yarnScript.GetVoiceOversOfLanguageAsync(GetVoiceOversCallback));
                } else {
#pragma warning disable CS4014 // Don't await or save the returned Tasks because the user set the option waitForAddressablesLoaded to false
                    yarnScript.GetVoiceOversOfLanguageAsync(GetVoiceOversCallback);
#pragma warning restore CS4014
                }
            } else {
#endif
                foreach (var voiceOver in yarnScript.GetVoiceOversOfLanguage()) {
                    localizedAudio.Add(voiceOver.Key, voiceOver.Value);
                }
#if ADDRESSABLES
            }
#endif
        }

#if ADDRESSABLES
        /// <summary>
        /// Link a voice over AudioClip with a Yarn linetag.
        /// </summary>
        /// <param name="linetag">The linetag of the line.</param>
        /// <param name="voiceOver">The voice over AudioClip of the line.</param>
        private void GetVoiceOversCallback(string linetag, AudioClip voiceOver) {
            localizedAudio[linetag] = voiceOver;
        }

        /// <summary>
        /// Check if all Addressable load tasks are finished and invoke the given
        /// action.
        /// </summary>
        /// <param name="onAddressablesLoaded">Action invoked after all Addressables are loaded.</param>
        /// <returns></returns>
        private IEnumerator CheckAddressablesLoaded(Action onAddressablesLoaded) {
            while (waitForAddressablesLoaded && addressableVoiceOverLoadingTasks.Count > 0) {
                for (int i = addressableVoiceOverLoadingTasks.Count - 1; i >= 0; i--) {
                    // Remove task if not active anymore
                    if ((int)addressableVoiceOverLoadingTasks[i].Status > 3) {
                        addressableVoiceOverLoadingTasks.RemoveAt(i);
                    }
                }
                yield return 0;
            }
            WaitForAddressablesLoadedCoroutine = null;
            onAddressablesLoaded?.Invoke();
        }
#endif

        Dialogue CreateDialogueInstance()
        {
            // Create the main Dialogue runner, and pass our
            // variableStorage to it
            var dialogue = new Yarn.Dialogue(variableStorage) {

                // Set up the logging system.
                LogDebugMessage = delegate (string message) {
                    Debug.Log(message);
                },
                LogErrorMessage = delegate (string message) {
                    Debug.LogError(message);
                },

                lineHandler = HandleLine,
                commandHandler = HandleCommand,
                optionsHandler = HandleOptions,
                nodeStartHandler = (node) => {
                    onNodeStart?.Invoke(node);
                    return Dialogue.HandlerExecutionType.ContinueExecution;
                },
                nodeCompleteHandler = (node) => {
                    onNodeComplete?.Invoke(node);
                    return Dialogue.HandlerExecutionType.ContinueExecution;
                },
                dialogueCompleteHandler = HandleDialogueComplete
            };

            // Yarn Spinner defines two built-in commands: "wait",
            // and "stop". Stop is defined inside the Virtual
            // Machine (the compiler traps it and makes it a
            // special case.) Wait is defined here in Unity.
            AddCommandHandler("wait", HandleWaitCommand);

            foreach (var yarnScript in yarnScripts) {
                AddDialogueLines(yarnScript);
            }

            selectAction = SelectedOption;

            return dialogue;

            void HandleWaitCommand(string[] parameters, Action onComplete)
            {
                if (parameters?.Length != 1) {
                    Debug.LogErrorFormat("<<wait>> command expects one parameter.");
                    onComplete();
                    return;
                }

                string durationString = parameters[0];

                if (float.TryParse(durationString,
                                   System.Globalization.NumberStyles.AllowDecimalPoint,
                                   System.Globalization.CultureInfo.InvariantCulture,
                                   out var duration) == false) {

                    Debug.LogErrorFormat($"<<wait>> failed to parse duration {durationString}");
                    onComplete();
                }

                StartCoroutine(DoHandleWait());
                IEnumerator DoHandleWait()
                {
                    yield return new WaitForSeconds(duration);
                    onComplete();
                }
            }

            void HandleOptions(OptionSet options) {
                DialogueOption[] optionSet = new DialogueOption[options.Options.Length];
                for (int i = 0; i < options.Options.Length; i++) {
                    optionSet[i] = new DialogueOption {
                        TextID = options.Options[i].Line.ID,
                        DialogueOptionID = options.Options[i].ID,
                        TextLocalized = localizedText[options.Options[i].Line.ID]
                    };
                }
                foreach (var dialogueView in dialogueViews) {
                    dialogueView.RunOptions(optionSet, selectAction);
                }
            }

            void HandleDialogueComplete()
            {
                IsDialogueRunning = false;
                foreach (var dialogueView in dialogueViews) {
                    dialogueView.DialogueComplete();
                }
                onDialogueComplete.Invoke();
            }

            Dialogue.HandlerExecutionType HandleCommand(Command command)
            {
                bool wasValidCommand;
                Dialogue.HandlerExecutionType executionType;

                // Try looking in the command handlers first, which is a lot
                // cheaper than crawling the game object hierarchy.

                // Set a flag that we can use to tell if the dispatched command
                // immediately called _continue
                wasCompleteCalled = false;

                (wasValidCommand, executionType) = DispatchCommandToRegisteredHandlers(command, () => ContinueDialogue());

                if (wasValidCommand) {

                    // This was a valid command. It returned either continue,
                    // or pause; if it returned pause, there's a chance that
                    // the command handler immediately called _continue, in
                    // which case we should not pause.
                    if (wasCompleteCalled) {
                        return Dialogue.HandlerExecutionType.ContinueExecution;
                    }
                    else {
                        // Either continue execution, or pause (in which case
                        // _continue will be called)
                        return executionType;
                    }
                }

                // We didn't find it in the comand handlers. Try looking in the game objects.
                (wasValidCommand, executionType) = DispatchCommandToGameObject(command);

                if (wasValidCommand) {
                    // We found an object and method to invoke as a Yarn
                    // command. It may or may not have been a coroutine; if it
                    // was a coroutine, executionType will be
                    // HandlerExecutionType.Pause, and we'll wait for it to
                    // complete before resuming execution.
                    return executionType;
                }

                // TODO for Code View
                // The following passage needs to be adapted to the new design.
                // My knowledge about these commands and why it should be handled
                // on the DialogueUI is limited so it'd be best if you take a lookt at this.
                // My intend would be to handle the situation right here in this class but I
                // could be missing context ...

                // We didn't find a method in our C# code to invoke. Pass it to
                // the UI to handle; it will determine whether we pause or
                // continue.
                //return dialogueViews.RunCommand(command, continueAction);

                // Ugly hack to keep the previous behaviour intact following in 3... 2... 1...
                return GetDialogueUI().RunCommand(command, () => ContinueDialogue());
            }

            /// Forward the line to the dialogue UI.
            Dialogue.HandlerExecutionType HandleLine(Line line)
            {
                // Update lines to current state before sending them to the view classes
                var substitutions = GetInlineExpressions(line);
                var text = localizedText.ContainsKey(line.ID) ? localizedText[line.ID] : string.Empty;
                var audio = localizedAudio.ContainsKey(line.ID) ? localizedAudio[line.ID] : null;
                var localizedLine = new LocalizedLine() {
                    TextID = line.ID,
                    TextLocalized = text,
                    VoiceOverLocalized = audio,
                    Substitutions = substitutions
                };

                // First register current dialogue views for line complete calls
                foreach (var dialogueView in dialogueViews) {
                    lineCurrentlyRunOnDialogueViews.Add(dialogueView);
                }
                // Send line to available dialogue views
                foreach (var dialogueView in dialogueViews) {
                    dialogueView.RunLine(localizedLine, () => OnDialogueLineFinished());
                }
                return Dialogue.HandlerExecutionType.PauseExecution;
            }

            /// Indicates to the DialogueRunner that the user has selected an option
            void SelectedOption(int obj)
            {
                Dialogue.SetSelectedOption(obj);
                ContinueDialogue();
            }

            (bool commandWasFound, Dialogue.HandlerExecutionType executionType) DispatchCommandToRegisteredHandlers(Command command, Action onComplete)
            {
                var commandTokens = command.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                //Debug.Log($"Command: <<{command.Text}>>");

                if (commandTokens.Length == 0) {
                    // Nothing to do
                    return (false, Dialogue.HandlerExecutionType.ContinueExecution);
                }

                var firstWord = commandTokens[0];

                if (commandHandlers.ContainsKey(firstWord) == false &&
                    blockingCommandHandlers.ContainsKey(firstWord) == false) {

                    // We don't have a registered handler for this command, but
                    // some other part of the game might.
                    return (false, Dialogue.HandlerExecutionType.ContinueExecution);
                }

                // Single-word command, eg <<jump>>
                if (commandTokens.Length == 1) {
                    if (commandHandlers.ContainsKey(firstWord)) {
                        commandHandlers[firstWord](null);
                        return (true, Dialogue.HandlerExecutionType.ContinueExecution);
                    }
                    else {
                        blockingCommandHandlers[firstWord](new string[] { }, onComplete);
                        return (true, Dialogue.HandlerExecutionType.PauseExecution);
                    }
                }

                // Multi-word command, eg <<walk Mae left>>
                var remainingWords = new string[commandTokens.Length - 1];

                // Copy everything except the first word from the array
                System.Array.Copy(commandTokens, 1, remainingWords, 0, remainingWords.Length);

                if (commandHandlers.ContainsKey(firstWord)) {
                    commandHandlers[firstWord](remainingWords);
                    return (true, Dialogue.HandlerExecutionType.ContinueExecution);
                }
                else {
                    blockingCommandHandlers[firstWord](remainingWords, onComplete);
                    return (true, Dialogue.HandlerExecutionType.PauseExecution);
                }
            }

            /// commands that can be automatically dispatched look like this:
            /// COMMANDNAME OBJECTNAME <param> <param> <param> ...
            /** We can dispatch this command if:
             * 1. it has at least 2 words
             * 2. the second word is the name of an object
             * 3. that object has components that have methods with the
             *    YarnCommand attribute that have the correct commandString set
             */
            (bool methodFound, Dialogue.HandlerExecutionType executionType) DispatchCommandToGameObject(Command command)
            {
                var words = command.Text.Split(' ');

                // need 2 parameters in order to have both a command name
                // and the name of an object to find
                if (words.Length < 2) {
                    return (false, Dialogue.HandlerExecutionType.ContinueExecution);
                }

                var commandName = words[0];

                var objectName = words[1];

                var sceneObject = GameObject.Find(objectName);

                // If we can't find an object, we can't dispatch a command
                if (sceneObject == null) {
                    return (false, Dialogue.HandlerExecutionType.ContinueExecution);
                }

                int numberOfMethodsFound = 0;
                List<string[]> errorValues = new List<string[]>();

                List<string> parameters;

                if (words.Length > 2) {
                    parameters = new List<string>(words);
                    parameters.RemoveRange(0, 2);
                }
                else {
                    parameters = new List<string>();
                }

                var startedCoroutine = false;

                // Find every MonoBehaviour (or subclass) on the object
                foreach (var component in sceneObject.GetComponents<MonoBehaviour>()) {
                    var type = component.GetType();

                    // Find every method in this component
                    foreach (var method in type.GetMethods()) {

                        // Find the YarnCommand attributes on this method
                        var attributes = (YarnCommandAttribute[])method.GetCustomAttributes(typeof(YarnCommandAttribute), true);

                        // Find the YarnCommand whose commandString is equal to
                        // the command name
                        foreach (var attribute in attributes) {
                            if (attribute.CommandString == commandName) {

                                var methodParameters = method.GetParameters();
                                bool paramsMatch = false;
                                // Check if this is a params array
                                if (methodParameters.Length == 1 &&
                                    methodParameters[0].ParameterType.IsAssignableFrom(typeof(string[]))) {
                                    // Cool, we can send the command!

                                    // If this is a coroutine, start it,
                                    // and set a flag so that we know to
                                    // wait for it to finish
                                    string[][] paramWrapper = new string[1][];
                                    paramWrapper[0] = parameters.ToArray();
                                    if (method.ReturnType == typeof(IEnumerator)) {
                                        StartCoroutine(DoYarnCommandParams(component, method, paramWrapper));
                                        startedCoroutine = true;
                                    }
                                    else {
                                        method.Invoke(component, paramWrapper);

                                    }
                                    numberOfMethodsFound++;
                                    paramsMatch = true;

                                }
                                // Otherwise, verify that this method has the right number of parameters
                                else if (methodParameters.Length == parameters.Count) {
                                    paramsMatch = true;
                                    foreach (var paramInfo in methodParameters) {
                                        if (!paramInfo.ParameterType.IsAssignableFrom(typeof(string))) {
                                            Debug.LogErrorFormat(sceneObject, "Method \"{0}\" wants to respond to Yarn command \"{1}\", but not all of its parameters are strings!", method.Name, commandName);
                                            paramsMatch = false;
                                            break;
                                        }
                                    }
                                    if (paramsMatch) {
                                        // Cool, we can send the command!

                                        // If this is a coroutine, start it,
                                        // and set a flag so that we know to
                                        // wait for it to finish
                                        if (method.ReturnType == typeof(IEnumerator)) {
                                            StartCoroutine(DoYarnCommand(component, method, parameters.ToArray()));
                                            startedCoroutine = true;
                                        }
                                        else {
                                            method.Invoke(component, parameters.ToArray());
                                        }
                                        numberOfMethodsFound++;
                                    }
                                }
                                //parameters are invalid, but name matches.
                                if (!paramsMatch) {
                                    //save this error in case a matching
                                    //command is never found.
                                    errorValues.Add(new string[] { method.Name, commandName, methodParameters.Length.ToString(), parameters.Count.ToString() });
                                }
                            }
                        }
                    }
                }

                // Warn if we found multiple things that could respond to this
                // command.
                if (numberOfMethodsFound > 1) {
                    Debug.LogWarningFormat(sceneObject, "The command \"{0}\" found {1} targets. " +
                        "You should only have one - check your scripts.", command, numberOfMethodsFound);
                }
                else if (numberOfMethodsFound == 0) {
                    //list all of the near-miss methods only if a proper match
                    //is not found, but correctly-named methods are.
                    foreach (string[] errorVal in errorValues) {
                        Debug.LogErrorFormat(sceneObject, "Method \"{0}\" wants to respond to Yarn command \"{1}\", but it has a different number of parameters ({2}) to those provided ({3}), or is not a string array!", errorVal[0], errorVal[1], errorVal[2], errorVal[3]);
                    }
                }

                var wasValidCommand = numberOfMethodsFound > 0;

                if (wasValidCommand == false) {
                    return (false, Dialogue.HandlerExecutionType.ContinueExecution);
                }

                if (startedCoroutine) {
                    // Signal to the Dialogue that execution should wait. 
                    return (true, Dialogue.HandlerExecutionType.PauseExecution);
                }
                else {
                    // This wasn't a coroutine, so no need to wait for it.
                    return (true, Dialogue.HandlerExecutionType.ContinueExecution);
                }

                IEnumerator DoYarnCommandParams(MonoBehaviour component,
                                                MethodInfo method,
                                                string[][] localParameters)
                {
                    // Wait for this command coroutine to complete
                    yield return StartCoroutine((IEnumerator)method.Invoke(component, localParameters));

                    // And then continue running dialogue
                    ContinueDialogue();
                }

                IEnumerator DoYarnCommand(MonoBehaviour component,
                                          MethodInfo method,
                                          string[] localParameters)
                {
                    // Wait for this command coroutine to complete
                    yield return StartCoroutine((IEnumerator)method.Invoke(component, localParameters));

                    // And then continue running dialogue
                    ContinueDialogue();
                }
            }
        }

        void OnLanguagePreferencesChanged(object sender, EventArgs e) {
            localizedAudio.Clear();
            localizedText.Clear();
            foreach (var yarnScript in yarnScripts) {
                AddDialogueLines(yarnScript);
            }
        }

        void ContinueDialogue()
        {
            lineCurrentlyRunOnDialogueViews.Clear();
            wasCompleteCalled = true;
            userIntendedNextLine = false;
            Dialogue.Continue();
        }

        internal void OnViewUserIntentNextLine() {
            userIntendedNextLine = true;

            switch (GetLowestLineStatus()) {
                case DialogueViewBase.DialogueLineStatus.Running:
                    FinishLineCurrentlyRunningOnViews();
                    break;
                case DialogueViewBase.DialogueLineStatus.Finished:
                    EndLineCurrentlyFinishedOnViews();
                    break;
                default:
                    break;
            }

            if (CheckDialogueViewsForCommonLineStatus(DialogueViewBase.DialogueLineStatus.Ended) && (continueNextLineOnLineFinished || userIntendedNextLine)) {
                ContinueDialogue();
            }
        }

        internal void OnViewUserIntentFinishLine() {
            FinishLineCurrentlyRunningOnViews();
        }

        void OnDialogueLineFinished() {
            if (CheckDialogueViewsForCommonLineStatus(DialogueViewBase.DialogueLineStatus.Finished)) {
                LineFinishedOnAllViews();

                if (continueNextLineOnLineFinished || userIntendedNextLine) {
                    EndLineCurrentlyFinishedOnViews();
                }
            }
        }

        void OnDialogueLineCompleted() {
            if (CheckDialogueViewsForCommonLineStatus(DialogueViewBase.DialogueLineStatus.Ended)) {
                ContinueDialogue();
            }
        }

        private void LineFinishedOnAllViews() {
            foreach (var dialogueView in dialogueViews) {
                dialogueView.OnFinishedLineOnAllViews();
            }
        }

        private void EndLineCurrentlyFinishedOnViews() {
            foreach (var dialogueView in dialogueViews) {
                if (dialogueView.dialogueLineStatus == DialogueViewBase.DialogueLineStatus.Finished) {
                    StartCoroutine(dialogueView.EndCurrentLine(OnDialogueLineCompleted));
                }
            }
        }

        private void FinishLineCurrentlyRunningOnViews() {
            foreach (var dialogueView in dialogueViews) {
                if (dialogueView.dialogueLineStatus == DialogueViewBase.DialogueLineStatus.Running) {
                    dialogueView.FinishRunningCurrentLine();
                }
            }
        }

        DialogueViewBase.DialogueLineStatus GetLowestLineStatus() {
            var lowestStatus = DialogueViewBase.DialogueLineStatus.Ended;
            foreach (var dialogueView in dialogueViews) {
                if (dialogueView.dialogueLineStatus < lowestStatus) {
                    lowestStatus = dialogueView.dialogueLineStatus;
                }
            }
            return lowestStatus;
        }

        private bool CheckDialogueViewsForCommonLineStatus (DialogueViewBase.DialogueLineStatus commonStatusToCheck) {
            foreach (var dialogueView in dialogueViews) {
                if (dialogueView.dialogueLineStatus != commonStatusToCheck) {
                    return false;
                }
            }
            return true;
        }

        string[] GetInlineExpressions (Line line) {
            if (!localizedText.ContainsKey(line.ID)) return Array.Empty<string>();

            List<string> substitutions = new List<string>();

            foreach (string substitution in line.Substitutions) {
                substitutions.Add(substitution);
            }

            return substitutions.ToArray();
        }

        [Obsolete("This is a temporary workaround. Do not keep this.")]
        private DialogueUI GetDialogueUI() 
        {
            DialogueUI dialogueUI = null;
            // Search mode: confident
            foreach (var dialogueView in dialogueViews) {
                if (dialogueView.GetType() == typeof(DialogueUI)) {
                    dialogueUI = dialogueView as DialogueUI;
                }
            }
            // Search mode: panicking
            if (!dialogueUI) {
                dialogueUI = FindObjectOfType<DialogueUI>();
            }
            // Search mode: giving up
            if (!dialogueUI) {
                Debug.LogError("No DialogueUI for command handling found.", gameObject);
            }

            return dialogueUI;
        }

        #endregion
    }

    #region Class/Interface

    /// <summary>
    /// An attribute that marks a method on a <see cref="MonoBehaviour"/>
    /// as a [command](<![CDATA[ {{<ref "/docs/unity/working-with-commands">}}]]>).
    /// </summary>
    /// <remarks>
    /// When a <see cref="DialogueRunner"/> receives a <see
    /// cref="Command"/>, and no command handler has been installed for the
    /// command, it splits it by spaces, and then checks to see if the
    /// second word, if any, is the name of any <see cref="GameObject"/>s
    /// in the scene. 
    ///
    /// If one is found, it is checked to see if any of the
    /// <see cref="MonoBehaviour"/>s attached to the class has a <see
    /// cref="YarnCommandAttribute"/> whose <see
    /// cref="YarnCommandAttribute.CommandString"/> matching the first word
    /// of the command.
    ///
    /// If a method is found, its parameters are checked:
    ///
    /// * If the method takes a single <see cref="string"/>[] parameter,
    /// the method is called, and will be passed an array containing all
    /// words in the command after the first two.
    /// 
    /// * If the method takes a number of <see cref="string"/> parameters
    /// equal to the number of words in the command after the first two, it
    /// will be called with those words as parameters.
    ///
    /// * Otherwise, it will not be called, and a warning will be issued.
    ///
    /// ### `YarnCommand`s and Coroutines
    /// 
    /// This attribute may be attached to a coroutine. 
    /// 
    /// {{|note|}}
    /// The <see
    /// cref="DialogueRunner"/> determines if the method is a coroutine if
    /// the method returns <see cref="IEnumerator"/>.
    /// {{|/note|}}
    /// 
    /// If the method is a coroutine, the DialogueRunner will pause
    /// execution until the coroutine ends.
    /// </remarks>
    /// <example>
    ///
    /// The following C# code uses the `YarnCommand` attribute to register
    /// commands.
    ///
    /// <![CDATA[
    /// ```csharp 
    /// class ExampleBehaviour : MonoBehaviour {
    ///         [YarnCommand("jump")] 
    ///         void Jump()
    ///         {
    ///             Debug.Log($"{this.gameObject.name} is jumping!");
    ///         }
    ///    
    ///         [YarnCommand("walk")] 
    ///         void WalkToDestination(string destination) {
    ///             Debug.Log($"{this.gameObject.name} is walking to {destination}!");
    ///         }
    ///     
    ///         [YarnCommand("shine_flashlight")] 
    ///         IEnumerator ShineFlashlight(string durationString) {
    ///             float.TryParse(durationString, out var duration);
    ///             Debug.Log($"{this.gameObject.name} is turning on the flashlight for {duration} seconds!");
    ///             yield new WaitForSeconds(duration);
    ///             Debug.Log($"{this.gameObject.name} is turning off the flashlight!");
    ///         }
    /// }
    /// ```
    /// ]]>
    ///
    /// Next, assume that this `ExampleBehaviour` script has been attached
    /// to a <see cref="GameObject"/> present in the scene named "Mae". The
    /// `Jump` and `WalkToDestination` methods may then be called from a
    /// Yarn script like so:
    ///
    /// <![CDATA[
    /// ```yarn 
    /// // Call the Jump() method in the ExampleBehaviour on Mae
    /// <<jump Mae>>
    ///
    /// // Call the WalkToDestination() method in the ExampleBehaviour 
    /// // on Mae, passing "targetPoint" as a parameter
    /// <<walk Mae targetPoint>>
    /// 
    /// // Call the ShineFlashlight method, passing "0.5" as a parameter;
    /// // dialogue will wait until the coroutine ends.
    /// <<shine_flashlight Mae 0.5>>
    /// ```
    /// ]]>
    ///
    /// Running this Yarn code will result in the following text being
    /// logged to the Console:
    ///
    /// ``` 
    /// Mae is jumping! 
    /// Mae is walking to targetPoint! 
    /// Mae is turning on the flashlight for 0.5 seconds!
    /// (... 0.5 seconds elapse ...)
    /// Mae is turning off the flashlight!
    /// ```
    /// </example>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class YarnCommandAttribute : System.Attribute
    {
        /// <summary>
        /// The name of the command, as it exists in Yarn.
        /// </summary>
        /// <remarks>
        /// This value does not have to be the same as the name of the
        /// method. For example, you could have a method named
        /// "`WalkToPoint`", and expose it to Yarn as a command named
        /// "`walk_to_point`".
        /// </remarks>        
        public string CommandString { get; set; }

        public YarnCommandAttribute(string commandString) => CommandString = commandString;
    }
    
    /// <summary>
    /// A <see cref="MonoBehaviour"/> that a <see cref="DialogueRunner"/>
    /// uses to store and retrieve variables.
    /// </summary>
    /// <remarks>
    /// This abstract class inherits from <see cref="MonoBehaviour"/>,
    /// which means that subclasses of this class can be attached to <see
    /// cref="GameObject"/>s.
    /// </remarks>
    public abstract class VariableStorageBehaviour : MonoBehaviour, Yarn.VariableStorage
    {
        /// <inheritdoc/>
        public abstract Value GetValue(string variableName);

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, float floatValue) => SetValue(variableName, new Yarn.Value(floatValue));

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, bool boolValue) => SetValue(variableName, new Yarn.Value(boolValue));

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, string stringValue) => SetValue(variableName, new Yarn.Value(stringValue));

        /// <inheritdoc/>
        public abstract void SetValue(string variableName, Value value);

        /// <inheritdoc/>
        /// <remarks>
        /// The implementation in this abstract class throws a <see
        /// cref="NotImplementedException"/> when called. Subclasses of
        /// this class must provide their own implementation.
        /// </remarks>
        public virtual void Clear() => throw new NotImplementedException();

        /// <summary>
        /// Resets the VariableStorageBehaviour to its initial state.
        /// </summary>
        /// <remarks>
        /// This is similar to <see cref="Clear"/>, but additionally allows
        /// subclasses to restore any default values that should be
        /// present.
        /// </remarks>
        public abstract void ResetToDefaults();
    }

    public class LocalizedLine {
        /// <summary>
        /// DialogueLine's ID
        /// </summary>
        public string TextID;
        /// <summary>
        /// DialogueLine's inline expression's substitution
        /// </summary>
        public string[] Substitutions;
        /// <summary>
        /// DialogueLine's text
        /// </summary>
        public string TextLocalized;
        /// <summary>
        /// DialogueLine's voice over clip
        /// </summary>
        public AudioClip VoiceOverLocalized;
    }

    public class DialogueOption {
        /// <summary>
        /// The ID of this dialogue option
        /// </summary>
        public int DialogueOptionID;
        /// <summary>
        /// The ID of the dialogue option's text
        /// </summary>
        public string TextID;
        /// <summary>
        /// The text of this dialogue option
        /// </summary>
        public string TextLocalized;
    }

    #endregion
}
