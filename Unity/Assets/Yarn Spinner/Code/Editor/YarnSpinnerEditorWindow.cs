/*

The MIT License (MIT)

Copyright (c) 2015 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

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
using System.Collections;
using System.Collections.Generic;

namespace Yarn.Unity {
	public class YarnSpinnerEditorWindow : EditorWindow {

		class CheckerResult {
			public enum State {
				NotTested,
				Ignored,
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

		private List<CheckerResult> checkResults = new List<CheckerResult>();
		private IEnumerable<Yarn.Analysis.Diagnosis> diagnoses = new List<Yarn.Analysis.Diagnosis>();

		void UpdateJSONList() {
			// Find all TextAssets

			var list = AssetDatabase.FindAssets("t:textasset");

			checkResults.Clear();
			diagnoses = new List<Yarn.Analysis.Diagnosis>();;

			foreach (var guid in list) {

				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith(".json")) {
					var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

					var newResult = new CheckerResult(asset);

					checkResults.Add(newResult);
				}

			}
		}

		[MenuItem("Window/Yarn Spinner %#y", false, 2000)]
		static void ShowWindow() {
			EditorWindow.GetWindow(typeof(YarnSpinnerEditorWindow));
		}

		void OnEnable() {

			// Set the window title
			this.titleContent.text = "Yarn Spinner";
			this.titleContent.image = Icons.windowIcon;

			Validate();

		}
		Vector2 scrollPos;

		void OnGUI() {

			EditorGUILayout.BeginVertical();

			EditorGUILayout.Space();

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

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

			// Bottom box
			EditorGUILayout.EndScrollView();

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Refresh")) {
				Validate();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
		}

		static void DrawScriptGUI (CheckerResult result)
		{
			EditorGUILayout.BeginHorizontal ();

			// What icon should we use for this script?
			Texture image;
			switch (result.state) {
			case CheckerResult.State.NotTested:
				image = null;
				break;
			case CheckerResult.State.Ignored:
				image = Icons.ignoredIcon;
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

			EditorGUILayout.EndHorizontal ();

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
		void Validate ()
		{
			UpdateJSONList();

			var analysisContext = new Yarn.Analysis.Context();

			bool shouldPerformAnalysis = true;

			foreach (var result in checkResults) {

				CheckerResult.State state;

				var messages = ValidateFile(result.script, analysisContext, out state);

				result.state = state;
				result.messages = messages;

				// Don't perform whole-program analysis if any file failed to compile
				if (result.state != CheckerResult.State.Passed) {
					shouldPerformAnalysis = false;
				}

			}

            var results = new List<Yarn.Analysis.Diagnosis>();


			if (shouldPerformAnalysis)
                results.AddRange(analysisContext.FinishAnalysis());

            results.AddRange(AnalyseEnvironment());

            diagnoses = results;

		}



		// Validates a single script.
		ValidationMessage[] ValidateFile(TextAsset script, Analysis.Context analysisContext, out CheckerResult.State result) {

			var messageList = new List<ValidationMessage>();

			var variableStorage = new Yarn.MemoryVariableStore();

			var dialog = new Dialogue(variableStorage);

			bool failed = false;

			dialog.LogErrorMessage = delegate (string message) {
				var msg = new ValidationMessage();
				msg.type = MessageType.Error;
				msg.message = message;
				messageList.Add(msg);

				// any errors means this validation failed
				failed = true;
			};

			dialog.LogDebugMessage = delegate (string message) {
				var msg = new ValidationMessage();
				msg.type = MessageType.Info;
				msg.message = message;
				messageList.Add(msg);
			};

			try {
				dialog.LoadString(script.text,script.name);
			} catch (System.Exception e) {
				dialog.LogErrorMessage(e.Message);
			}

			dialog.Analyse(analysisContext);

			if (failed) {
				result = CheckerResult.State.Failed;
			} else {
				result = CheckerResult.State.Passed;
			}

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
                "This method is obsolete, and will not be called in future versions of Yarn Spinner. Use SetValue instead."
            ));

            deprecations.Add(new Deprecation(
                typeof(Yarn.Unity.VariableStorageBehaviour),
                "GetNumber",
                "This method is obsolete, and will not be called in future versions of Yarn Spinner. Use GetValue instead."
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

		static Texture _ignoredIcon;
		public static Texture ignoredIcon {
			get {
				if (_ignoredIcon == null) {
					_ignoredIcon = GetTexture("YarnSpinnerIgnored");
				}
				return _ignoredIcon;
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


