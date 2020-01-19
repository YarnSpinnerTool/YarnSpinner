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
using UnityEditor;
using System.Collections.Generic;

namespace Yarn.Unity {
    public class YarnSpinnerEditorWindow : EditorWindow {

        class CheckerResult {
            public enum State {
                NotTested,
                Passed,
                Failed
            }

            public State state;
            public TextAsset script;

            public ValidationMessage[] messages = new ValidationMessage[0];

            public override bool Equals (object obj)
            {
                if (obj is CheckerResult && ((CheckerResult)obj).script == this.script)
                    return true;
                else
                    return false;
            }

            public override int GetHashCode ()
            {
                return this.script.GetHashCode();
            }

            public CheckerResult(TextAsset script) {
                this.script = script;
                this.state = State.NotTested;
            }
        }

        // The list of files that we know about, and their status.
        private List<CheckerResult> checkResults = new List<CheckerResult>();

        // The list of analysis results that were made as a result of checking
        // all scripts
        private List<Yarn.Analysis.Diagnosis> diagnoses = new List<Yarn.Analysis.Diagnosis>();

        // Current scrolling position
        Vector2 scrollPos;

        // Root folder to search for json files
        private static string jsonRootPath;
        private static bool isJSONRootPathChosen = false;

        // Seconds before the progress bar appears during checking
        const float timeBeforeProgressBar = 0.1f;

        // Updates the list of all scripts that should be checked.
        void UpdateYarnScriptList() {

            // Clear the list of files
            checkResults.Clear();

            // Clear the list of diagnoses
            diagnoses = new List<Yarn.Analysis.Diagnosis>();

            // Find all TextAssets
            var list = AssetDatabase.FindAssets("t:textasset", new [] {jsonRootPath});

            foreach (var guid in list) {

                // Filter the list to only include .json files
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".yarn.txt")) {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

                    var newResult = new CheckerResult(asset);

                    checkResults.Add(newResult);
                }
            }
        }

        // Shows the window.
        [MenuItem("Window/Yarn Spinner %#y", false, 2000)]
        static void ShowWindow() {
            EditorWindow.GetWindow<YarnSpinnerEditorWindow>();
        }

        // Called when the window first appears.
        void OnEnable() {

            // Set the window title
            this.titleContent.text = "Yarn Spinner";
            this.titleContent.image = Icons.windowIcon;

            // Update the list of scripts known to the window
            if(isJSONRootPathChosen)
                UpdateYarnScriptList();
        }

        void RefreshAllResults() {
            // Start checking all files.

            // First, record when we started - we need
            // to know if it's time to show a progress
            // dialogue
            var startTime = EditorApplication.timeSinceStartup;

            // Have we presented a progress bar?
            var progressBarVisible = false;

            // Start checking all files; the delegate will be called
            // after each file has been checked
            CheckAllFiles(delegate(int complete, int total) {

                // How long have we been at this?
                var timeSinceStart = EditorApplication.timeSinceStartup - startTime;

                // If longer than 'timeBeforeProgressBar', show the progress bar
                if (timeSinceStart > timeBeforeProgressBar) {

                    // Figure out how much of the progress bar should be filled
                    var progress = (float)complete / (float)total;

                    // Describe what we're doing
                    var info = string.Format("Checking file {0} of {1}...", complete, total);

                    // Display or update the bar
                    EditorUtility.DisplayProgressBar("Checking Yarn Files", info, progress);

                    // Record that we need to clear this bar
                    progressBarVisible = true;
                }
            });

            // All done. Get rid of the progress bar if needed.
            if (progressBarVisible) {
                EditorUtility.ClearProgressBar();
            }
        }

        void OnGUI() {

            using (new EditorGUILayout.VerticalScope()) {
                EditorGUILayout.Space();

                // Show list of folders
                GUILayout.Label("Root folder for scripts - Will search recursively", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                string pathLabelTxt = isJSONRootPathChosen ? jsonRootPath : "***Select Path***";
                GUILayout.Label(pathLabelTxt, EditorStyles.helpBox);
                if(GUILayout.Button("Choose Yarn Script Root Path"))
                {
                    string folderPath = EditorUtility.OpenFolderPanel("Yarn Script Root Path", "", "");

                    // Parse folder
                    jsonRootPath = folderPath.Substring(Application.dataPath.ToCharArray().Length-6);                    
                    
                    UpdateYarnScriptList();
                    isJSONRootPathChosen = true;                    
                }
                GUILayout.EndHorizontal();

                // Return if no path selected
                if (!isJSONRootPathChosen)
                    return;

                //EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh")) {
                    RefreshAllResults();
                }
                
                EditorGUILayout.Space();

                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos)) {
                    scrollPos = scroll.scrollPosition;

                    foreach (var result in checkResults) {
                        DrawScriptGUI (result);
                    }

                    // Draw any diagnoses that resulted
                    foreach (var diagnosis in diagnoses) {

                        MessageType messageType;

                        switch (diagnosis.severity) {
                        case Yarn.Analysis.Diagnosis.Severity.Error:
                            messageType = MessageType.Error;
                            break;
                        case Yarn.Analysis.Diagnosis.Severity.Warning:
                            messageType = MessageType.Warning;
                            break;
                        case Yarn.Analysis.Diagnosis.Severity.Note:
                            messageType = MessageType.Info;
                            break;
                        default:
                            throw new System.ArgumentOutOfRangeException ();
                        }

                        EditorGUILayout.HelpBox(diagnosis.ToString(showSeverity:false), messageType);
                    }

                }

            }


        }

        static void DrawScriptGUI (CheckerResult result)
        {
            using (new EditorGUILayout.HorizontalScope()) {
                // What icon should we use for this script?
                Texture image;
                switch (result.state) {
                case CheckerResult.State.NotTested:
                    image = Icons.notTestedIcon;
                    break;
                case CheckerResult.State.Passed:
                    image = Icons.successIcon;
                    break;
                case CheckerResult.State.Failed:
                    image = Icons.failedIcon;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException ();
                }

                // Draw the image and the label name
                EditorGUILayout.LabelField (new GUIContent (image), GUILayout.Width (20));
                EditorGUILayout.LabelField (result.script.name);

            }

            // Draw any messages resulting from this script
            if (result.messages.Length > 0) {

                EditorGUI.indentLevel += 2;

                foreach (var message in result.messages) {

                    EditorGUILayout.HelpBox(message.message, message.type);

                }

                EditorGUI.indentLevel -= 2;
            }

        }

        // Finds all .JSON files, and validates them.
        delegate void GUICallback(int complete, int total);
        void CheckAllFiles (GUICallback callback = null)
        {

            // The shared context for all script analysis.
            var analysisContext = new Yarn.Analysis.Context();

            // We shouldn't try to perform program analysis if
            // any of the files fails to compile, because that
            // analysis would be performed on incomplete data.
            bool shouldPerformAnalysis = true;

            // How many files have we finished checking?
            int complete = 0;

            // Let's get started!

            // First, ensure that we're looking at all of the scripts.
            UpdateYarnScriptList();

            // Next, compile each one.
            foreach (var result in checkResults) {

                // Attempt to compile the file. Record any compiler messages.
                CheckerResult.State state;

                var messages = ValidateFile(result.script, analysisContext, out state);

                result.state = state;
                result.messages = messages;

                // Don't perform whole-program analysis if any file failed to compile
                if (result.state != CheckerResult.State.Passed) {
                    shouldPerformAnalysis = false;
                }

                // We're done with it; if we have a callback to call after
                // each file is validated, do so.
                complete++;

                if (callback != null)
                    callback(complete, checkResults.Count);

            }

            var results = new List<Yarn.Analysis.Diagnosis>();

            if (shouldPerformAnalysis) {
                var scriptAnalyses = analysisContext.FinishAnalysis ();
                results.AddRange (scriptAnalyses);
            }


            var environmentAnalyses = AnalyseEnvironment ();
            results.AddRange (environmentAnalyses);

            diagnoses = results;

        }



        // Validates a single script.
        ValidationMessage[] ValidateFile(TextAsset script, Analysis.Context analysisContext, out CheckerResult.State result) {

            // The list of messages we got from the compiler.
            var messageList = new List<ValidationMessage>();

            // A dummy variable storage; it won't be used, but Dialogue
            // needs it.
            var variableStorage = new Yarn.MemoryVariableStore();

            // The Dialog object is the compiler.
            var dialog = new Dialogue(variableStorage);

            // Whether compilation failed or not; this will be
            // set to true if any error messages are returned.
            bool failed = false;

            // Called when we get an error message. Convert this
            // message into a ValidationMessage and store it;
            // additionally, mark that this file failed compilation
            dialog.LogErrorMessage = delegate (string message) {
                var msg = new ValidationMessage();
                msg.type = MessageType.Error;
                msg.message = message;
                messageList.Add(msg);

                // any errors means this validation failed
                failed = true;
            };

            // Called when we get an error message. Convert this
            // message into a ValidationMessage and store it
            dialog.LogDebugMessage = delegate (string message) {
                var msg = new ValidationMessage();
                msg.type = MessageType.Info;
                msg.message = message;
                messageList.Add(msg);
            };

            // Attempt to compile this script. Any exceptions will result
            // in an error message
            try {
                // TODO: update for Yarn 1.0
                //dialog.LoadString(script.text,script.name);
            } catch (System.Exception e) {
                dialog.LogErrorMessage(e.Message);
            }

            // Once compilation has finished, run the analysis on it
            dialog.Analyse(analysisContext);

            // Did it succeed or not?
            if (failed) {
                result = CheckerResult.State.Failed;
            } else {
                result = CheckerResult.State.Passed;
            }

            // All done.
            return messageList.ToArray();

        }

        // A result from validation.
        struct ValidationMessage {
            public string message;

            public MessageType type;
        }

        struct Deprecation {
            public System.Type type;
            public string methodName;
            public string usageNotes;

            public Deprecation (System.Type type, string methodName, string usageNotes)
            {
                this.type = type;
                this.methodName = methodName;
                this.usageNotes = usageNotes;
            }

        }

        IEnumerable<Yarn.Analysis.Diagnosis> AnalyseEnvironment ()
        {

            var deprecations = new List<Deprecation>();

            deprecations.Add(new Deprecation(
                typeof(Yarn.Unity.VariableStorageBehaviour),
                "SetNumber",
                "This method is obsolete, and will not be called in future " +
                "versions of Yarn Spinner. Use SetValue instead."
            ));

            deprecations.Add(new Deprecation(
                typeof(Yarn.Unity.VariableStorageBehaviour),
                "GetNumber",
                "This method is obsolete, and will not be called in future " +
                "versions of Yarn Spinner. Use GetValue instead."
            ));

            var results = new List<Yarn.Analysis.Diagnosis>();

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();


            foreach (var assembly in assemblies) {
                foreach (var type in assembly.GetTypes()) {

                    foreach (var deprecation in deprecations) {
                        if (!type.IsSubclassOf (deprecation.type))
                            continue;

                        foreach (var method in type.GetMethods ()) {
                            if (method.Name == deprecation.methodName && method.DeclaringType == type) {
                                var message = "{0} implements the {1} method. {2}";
                                message = string.Format (message, type.Name, deprecation.methodName, deprecation.usageNotes);
                                var diagnosis = new Yarn.Analysis.Diagnosis (message, Yarn.Analysis.Diagnosis.Severity.Warning);
                                results.Add (diagnosis);
                            }
                        }

                    }
                }
            }
            return results;
        }

    }

    // Icons used by this editor window.
    internal class Icons {

        private static Texture GetTexture(string textureName) {
            var guids = AssetDatabase.FindAssets (string.Format ("{0} t:texture", textureName));
            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        static Texture _successIcon;
        public static Texture successIcon {
            get {
                if (_successIcon == null) {
                    _successIcon = GetTexture("YarnSpinnerSuccess");
                }
                return _successIcon;
            }
        }

        static Texture _failedIcon;
        public static Texture failedIcon {
            get {
                if (_failedIcon == null) {
                    _failedIcon = GetTexture("YarnSpinnerFailed");
                }
                return _failedIcon;
            }
        }

        static Texture _notTestedIcon;
        public static Texture notTestedIcon {
            get {
                if (_notTestedIcon == null) {
                    _notTestedIcon = GetTexture("YarnSpinnerNotTested");
                }
                return _notTestedIcon;
            }
        }

        static Texture _windowIcon;
        public static Texture windowIcon {
            get {
                if (_windowIcon == null) {
                    _windowIcon = GetTexture("YarnSpinnerEditorWindow");
                }
                return _windowIcon;
            }
        }
    }
}


