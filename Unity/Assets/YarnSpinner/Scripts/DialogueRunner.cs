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
using System.Collections;
using System.Collections.Generic;
using CsvHelper;
using System;

// Field ... is never assigned to and will always have its default value null
#pragma warning disable 0649

namespace Yarn.Unity
{

    /// DialogueRunners act as the interface between your game and
    /// YarnSpinner.
    /** Make our menu item slightly nicer looking */
    [AddComponentMenu("Scripts/Yarn Spinner/Dialogue Runner")]
    public class DialogueRunner : MonoBehaviour
    {
        /// The source files to load the conversation from
        public YarnProgram[] yarnScripts;

        public string textLanguage;

        /// Our variable storage
        public Yarn.Unity.VariableStorageBehaviour variableStorage;

        /// The object that will handle the actual display and user input
        public Yarn.Unity.DialogueUIBehaviour dialogueUI;

        /// Which node to start from
        public string startNode = Yarn.Dialogue.DEFAULT_START;

        /// Whether we should start dialogue when the scene starts
        public bool startAutomatically = true;

        /// Tests to see if the dialogue is running
        public bool isDialogueRunning { get; private set; }

        public bool automaticCommands = true;

        private System.Action _continue;

        private System.Action<int> _selectAction;

        public delegate void CommandHandler(string[] parameters);
        public delegate void BlockingCommandHandler(string[] parameters, System.Action onComplete);

        /// Maps the names of commands to action delegates.
        private Dictionary<string, CommandHandler> commandHandlers = new Dictionary<string, CommandHandler>();
        private Dictionary<string, BlockingCommandHandler> blockingCommandHandlers = new Dictionary<string, BlockingCommandHandler>();

        // Maps string IDs received from Yarn Spinner to user-facing text
        private Dictionary<string, string> strings = new Dictionary<string, string>();

        /// A type of UnityEvent that takes a single string parameter. 
        ///
        /// We need to create a concrete subclass in order for Unity to
        /// serialise the type correctly.
        [System.Serializable]
        public class StringUnityEvent : UnityEngine.Events.UnityEvent<string> { }

        /// A Unity event that receives the name of the node that just
        /// finished running
        [SerializeField] StringUnityEvent onNodeComplete;

        // A flag used to note when we call into a blocking command
        // handler, but it calls its complete handler immediately -
        // _before_ the Dialogue is told to pause. This out-of-order
        // problem can lead to the Dialogue being stuck in a paused state.
        // To solve this, this variable is set to false before any blocking
        // command handler is called, and set to true when ContinueDialogue
        // is called. If it's true after calling a blocking command
        // handler, then the Dialogue is not told to pause.
        private bool wasCompleteCalled = false;
        
        /// Our conversation engine
        /** Automatically created on first access
         */
        private Dialogue _dialogue;
        public Dialogue dialogue {
            get {
                if (_dialogue == null) {
                    // Create the main Dialogue runner, and pass our
                    // variableStorage to it
                    _dialogue = new Yarn.Dialogue (variableStorage);

                    // Set up the logging system.
                    _dialogue.LogDebugMessage = delegate (string message) {
                        Debug.Log (message);
                    };
                    _dialogue.LogErrorMessage = delegate (string message) {
                        Debug.LogError (message);
                    };

                    _dialogue.lineHandler = HandleLine;
                    _dialogue.commandHandler = HandleCommand;
                    _dialogue.optionsHandler = HandleOptions;
                    _dialogue.nodeCompleteHandler = (node) => { 
                        onNodeComplete?.Invoke(node); 
                        return Dialogue.HandlerExecutionType.ContinueExecution; 
                    };
                    _dialogue.dialogueCompleteHandler = HandleDialogueComplete;

                    // Yarn Spinner defines two built-in commands: "wait",
                    // and "stop". Stop is defined inside the Virtual
                    // Machine (the compiler traps it and makes it a
                    // special case.) Wait is defined here in Unity.
                    AddCommandHandler("wait", HandleWaitCommand);

                    foreach (var yarnScript in yarnScripts) {
                        AddStringTable(yarnScript);
                    }

                    _continue = this.ContinueDialogue;

                    _selectAction = this.SelectedOption;
        
                }
                return _dialogue;
            }
        }

        private void HandleWaitCommand(string[] parameters, System.Action onComplete)
        {
            if (parameters?.Length != 1) {
                Debug.LogErrorFormat("<<wait>> command expects one parameter.");
                onComplete();
                return;
            }

            string durationString = parameters[0];

            if (float.TryParse(
                durationString,
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture,
                out var duration) == false) {
                    
                Debug.LogErrorFormat($"<<wait>> failed to parse duration {durationString}");
                onComplete();
            }
            StartCoroutine(DoHandleWait(duration, onComplete));
        }

        private IEnumerator DoHandleWait(float duration, Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            onComplete();
        }

        private void HandleDialogueComplete()
        {
            isDialogueRunning = false;
            this.dialogueUI.DialogueComplete();
        }

        private void HandleOptions(OptionSet options)
        {
            
            this.dialogueUI.RunOptions(options, strings, _selectAction);
        }

        private Dialogue.HandlerExecutionType HandleCommand(Command command)
        {
            bool wasValidCommand;
            Dialogue.HandlerExecutionType executionType;

            // Try looking in the command handlers first, which is a lot
            // cheaper than crawling the game object hierarchy.

            // Set a flag that we can use to tell if the dispatched command
            // immediately called _continue
            wasCompleteCalled = false;

            (wasValidCommand, executionType) = DispatchCommandToRegisteredHandlers(command, _continue);   

            if (wasValidCommand) {

                // This was a valid command. It returned either continue,
                // or pause; if it returned pause, there's a chance that
                // the command handler immediately called _continue, in
                // which case we should not pause.
                if (wasCompleteCalled) {
                    return Dialogue.HandlerExecutionType.ContinueExecution;
                } else {
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

            // We didn't find a method in our C# code to invoke. Pass it to
            // the UI to handle; it will determine whether we pause or
            // continue.
            return this.dialogueUI.RunCommand(command, _continue);            
            
        }

        public (bool commandWasFound, Dialogue.HandlerExecutionType executionType) DispatchCommandToRegisteredHandlers(Command command, System.Action onComplete) {

            var commandTokens = command.Text.Split(new[] {' '}, System.StringSplitOptions.RemoveEmptyEntries);

            Debug.Log($"Command: <<{command.Text}>>");

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
                } else {
                    blockingCommandHandlers[firstWord](new string[] {}, onComplete);
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
            } else {
                blockingCommandHandlers[firstWord](remainingWords, onComplete);
                return (true, Dialogue.HandlerExecutionType.PauseExecution);
            }
        }

        /// Forward the line to the dialogue UI.
        private Dialogue.HandlerExecutionType HandleLine(Line line)
        {
            return this.dialogueUI.RunLine (line, strings, _continue);            
        }

        /// Adds a command handler. Yarn Spinner will continue execution after this handler is called.
        public void AddCommandHandler(string commandName, CommandHandler handler) {
            if (commandHandlers.ContainsKey(commandName) || blockingCommandHandlers.ContainsKey(commandName)) {
                Debug.LogError($"Cannot add a command handler for {commandName}: one already exists");
                return;
            }
            commandHandlers.Add(commandName, handler);
        }

        /// Adds a command handler. Yarn Spinner will pause execution after this handler is called.
        public void AddCommandHandler(string commandName, BlockingCommandHandler handler) {
            if (commandHandlers.ContainsKey(commandName) || blockingCommandHandlers.ContainsKey(commandName)) {
                Debug.LogError($"Cannot add a command handler for {commandName}: one already exists");
                return;
            }
            blockingCommandHandlers.Add(commandName, handler);
        }

        /// Removes a specific command handler.
        public void RemoveCommandHandler(string commandName) {
            commandHandlers.Remove(commandName);
            blockingCommandHandlers.Remove(commandName);
        }

        /// Start the dialogue
        void Start ()
        {
            // Ensure that we have our Implementation object
            if (dialogueUI == null) {
                Debug.LogError ("Implementation was not set! Can't run the dialogue!");
                return;
            }

            // And that we have our variable storage object
            if (variableStorage == null) {
                Debug.LogError ("Variable storage was not set! Can't run the dialogue!");
                return;
            }

            // Ensure that the variable storage has the right stuff in it
            variableStorage.ResetToDefaults ();

            // Combine all scripts together and load them
            if (yarnScripts != null && yarnScripts.Length > 0) {
                
                var compiledPrograms = new List<Program>();

                foreach (var program in yarnScripts) {
                    compiledPrograms.Add(program.GetProgram());
                }

                var combinedProgram = Program.Combine(compiledPrograms.ToArray());
                
                dialogue.SetProgram(combinedProgram);
            }

            
            if (startAutomatically) {
                StartDialogue();
            }
            
        }

        /// Adds a program and its base localisation string table
        internal void Add(YarnProgram scriptToLoad)
        {
            AddProgram(scriptToLoad);
            AddStringTable(scriptToLoad);
        }

        /// Adds a program, and all of its nodes
        internal void AddProgram(YarnProgram scriptToLoad)
        {
            this.dialogue.AddProgram(scriptToLoad.GetProgram());
        }

        /// Adds a tagged string table from the yarn asset depending on the variable "textLanguage"
        public void AddStringTable(YarnProgram yarnScript) {

            var textToLoad = new TextAsset();
            if (yarnScript.localizations != null || yarnScript.localizations.Length > 0) {
                textToLoad = Array.Find(yarnScript.localizations, element => element.languageName == textLanguage)?.text;
            }
            if (textToLoad == null || string.IsNullOrEmpty(textToLoad.text)) {
                textToLoad = yarnScript.baseLocalisationStringTable;
            }

            // Use the invariant culture when parsing the CSV
            var configuration = new CsvHelper.Configuration.Configuration(
                System.Globalization.CultureInfo.InvariantCulture
            );

            using (var reader = new System.IO.StringReader(textToLoad.text))
            using (var csv = new CsvReader(reader, configuration)) {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read()) {
                    strings.Add(csv.GetField("id"), csv.GetField("text"));
                }
            }
        }

        public void AddStringTable(IDictionary<string, Yarn.StringInfo> stringTable) {
            foreach (var line in stringTable) {
                strings.Add(line.Key, line.Value.text);
            }
        }

        /// Indicates to the DialogueRunner that the user has selected an option
        private void SelectedOption(int obj)
        {
            this.dialogue.SetSelectedOption(obj);
            ContinueDialogue();
        }

        /// Destroy the variable store and start the dialogue again
        public void ResetDialogue ()
        {
            variableStorage.ResetToDefaults ();
            StartDialogue ();
        }

        /// Start the dialogue from the start node
        public void StartDialogue () {
            StartDialogue(startNode);
        }

        /// Start the dialogue from a given node
        public void StartDialogue (string startNode)
        {

            // Stop any processes that might be running already
            dialogueUI.StopAllCoroutines ();

            // Get it going
            RunDialogue (startNode);
        }

        private void ContinueDialogue()
        {
            wasCompleteCalled = true;
            this.dialogue.Continue();           
        }

        void RunDialogue (string startNode = "Start")
        {
            // Mark that we're in conversation.
            isDialogueRunning = true;

            // Signal that we're starting up.
            this.dialogueUI.DialogueStarted();

            this.dialogue.SetNode(startNode);

            ContinueDialogue();
            
            
        }

        /// Clear the dialogue system
        public void Clear() {

            if (isDialogueRunning) {
                throw new System.InvalidOperationException("You cannot clear the dialogue system while a dialogue is running.");
            }

            dialogue.UnloadAll();
        }

        /// Stop the dialogue
        public void Stop() {
            isDialogueRunning = false;
            dialogue.Stop();
        }

        /// Test to see if a node name exists
        public bool NodeExists(string nodeName) {
            return dialogue.NodeExists(nodeName);
        }

        /// Return the current node name
        public string currentNodeName {
            get {
                return dialogue.currentNode;
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
        public (bool methodFound, Dialogue.HandlerExecutionType executionType) DispatchCommandToGameObject(Command command) {

            var words = command.Text.Split(' ');

            // need 2 parameters in order to have both a command name
            // and the name of an object to find
            if (words.Length < 2)
            {
                return (false, Dialogue.HandlerExecutionType.ContinueExecution);                
            }

            var commandName = words[0];

            var objectName = words[1];

            var sceneObject = GameObject.Find(objectName);

            // If we can't find an object, we can't dispatch a command
            if (sceneObject == null)
            {
                return (false, Dialogue.HandlerExecutionType.ContinueExecution);                
            }

            int numberOfMethodsFound = 0;
            List<string[]> errorValues = new List<string[]>();

            List<string> parameters;

            if (words.Length > 2) {
                parameters = new List<string>(words);
                parameters.RemoveRange(0, 2);
            } else {
                parameters = new List<string>();
            }

            var startedCoroutine = false;

            // Find every MonoBehaviour (or subclass) on the object
            foreach (var component in sceneObject.GetComponents<MonoBehaviour>()) {
                var type = component.GetType();

                // Find every method in this component
                foreach (var method in type.GetMethods()) {

                    // Find the YarnCommand attributes on this method
                    var attributes = (YarnCommandAttribute[]) method.GetCustomAttributes(typeof(YarnCommandAttribute), true);

                    // Find the YarnCommand whose commandString is equal to
                    // the command name
                    foreach (var attribute in attributes) {
                        if (attribute.commandString == commandName) {


                            var methodParameters = method.GetParameters();
                            bool paramsMatch = false;
                            // Check if this is a params array
                            if (methodParameters.Length == 1 && 
                                methodParameters[0].ParameterType.IsAssignableFrom(typeof(string[])))
                                {
                                    // Cool, we can send the command!

                                    // If this is a coroutine, start it,
                                    // and set a flag so that we know to
                                    // wait for it to finish
                                    string[][] paramWrapper = new string[1][];
                                    paramWrapper[0] = parameters.ToArray();
                                    if (method.ReturnType == typeof(IEnumerator))
                                    {
                                        StartCoroutine(DoYarnCommand(component, method, paramWrapper)); 
                                        startedCoroutine = true;
                                    }
                                    else
                                    {
                                        method.Invoke(component, paramWrapper);                                        

                                    }
                                    numberOfMethodsFound++;
                                    paramsMatch = true;

                            }
                            // Otherwise, verify that this method has the right number of parameters
                            else if (methodParameters.Length == parameters.Count)
                            {
                                paramsMatch = true;
                                foreach (var paramInfo in methodParameters)
                                {
                                    if (!paramInfo.ParameterType.IsAssignableFrom(typeof(string)))
                                    {
                                        Debug.LogErrorFormat(sceneObject, "Method \"{0}\" wants to respond to Yarn command \"{1}\", but not all of its parameters are strings!", method.Name, commandName);
                                        paramsMatch = false;
                                        break;
                                    }
                                }
                                if (paramsMatch)
                                {
                                    // Cool, we can send the command!
                                    
                                    // If this is a coroutine, start it,
                                    // and set a flag so that we know to
                                    // wait for it to finish
                                    if (method.ReturnType == typeof(IEnumerator))
                                    {
                                        StartCoroutine(DoYarnCommand(component, method, parameters.ToArray()));
                                        startedCoroutine = true;
                                    }
                                    else
                                    {
                                        method.Invoke(component, parameters.ToArray());
                                    }
                                    numberOfMethodsFound++;
                                }
                            }
                            //parameters are invalid, but name matches.
                            if (!paramsMatch)
                            {
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
            } else if (numberOfMethodsFound == 0) {
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
            } else {
                // This wasn't a coroutine, so no need to wait for it.
                return (true, Dialogue.HandlerExecutionType.ContinueExecution);                
            }
            
        }

        IEnumerator DoYarnCommand(MonoBehaviour component, System.Reflection.MethodInfo method, string[][] parameters) {
            // Wait for this command coroutine to complete
            yield return StartCoroutine((IEnumerator)method.Invoke(component, parameters));

            // And then continue running dialogue
            ContinueDialogue();
        }

        IEnumerator DoYarnCommand(MonoBehaviour component, System.Reflection.MethodInfo method, string[] parameters) {
            // Wait for this command coroutine to complete
            yield return StartCoroutine((IEnumerator)method.Invoke(component, parameters));

            // And then continue running dialogue
            ContinueDialogue();
        }

        // Expose the RegisterFunction methods from dialogue.library to Unity

        /// Registers a new function that returns a value, so that it can
        /// be called from Yarn scripts.
        public void RegisterFunction(string name, int parameterCount, ReturningFunction implementation) {
            dialogue.library.RegisterFunction(name, parameterCount, implementation);
        }

        /// Registers a new function that doesn't return a value, so that
        /// it can be called from Yarn scripts.
        public void RegisterFunction(string name, int parameterCount, Function implementation) {
            dialogue.library.RegisterFunction(name, parameterCount, implementation);
        }
        
    }

    /// them to Yarn.

    /** For example:
     *  [YarnCommand("dosomething")]
     *      void Foo() {
     *         do something!
     *      }
     */
    public class YarnCommandAttribute : System.Attribute
    {
        public string commandString { get; private set; }

        public YarnCommandAttribute(string commandString) {
            this.commandString = commandString;
        }
    }

    /// Scripts that can act as the UI for the conversation should subclass
    /// this
    public abstract class DialogueUIBehaviour : MonoBehaviour
    {
        /// A conversation has started.
        public virtual void DialogueStarted() {
            // Default implementation does nothing.            
        }

        /// Display a line.
        public abstract Dialogue.HandlerExecutionType RunLine (Yarn.Line line, IDictionary<string, string> strings, System.Action onLineComplete);

        /// Display the options, and call the optionChooser when done.
        public abstract void RunOptions (Yarn.OptionSet optionSet, 
                                        IDictionary<string, string> strings,
                                                System.Action<int> onOptionSelected);

        /// Perform some game-specific command.
        public abstract Dialogue.HandlerExecutionType RunCommand (Yarn.Command command, System.Action onCommandComplete);

        /// The node has ended.
        public virtual Dialogue.HandlerExecutionType NodeComplete(string nextNode, System.Action onComplete) {
            // Default implementation does nothing.  

            return Dialogue.HandlerExecutionType.ContinueExecution;             
        }

        /// The conversation has ended.
        public virtual void DialogueComplete () {
            // Default implementation does nothing.            
        }
    }

    /// Scripts that can act as a variable storage should subclass this
    public abstract class VariableStorageBehaviour : MonoBehaviour, Yarn.VariableStorage
    {
        /// Get a value
        public abstract Value GetValue(string variableName);

        public virtual void SetValue(string variableName, float floatValue)
        {
            Value value = new Yarn.Value(floatValue);
            this.SetValue(variableName, value);
        }
        public virtual void SetValue(string variableName, bool stringValue)
        {
            Value value = new Yarn.Value(stringValue);
            this.SetValue(variableName, value);
        }
        public virtual void SetValue(string variableName, string boolValue)
        {
            Value value = new Yarn.Value(boolValue);
            this.SetValue(variableName, value);
        }

        /// Set a value
        public abstract void SetValue(string variableName, Value value);

        /// Not implemented here
        public virtual void Clear ()
        {
            throw new System.NotImplementedException ();
        }

        public abstract void ResetToDefaults ();

    }

}
