using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// An extremely simple implementation of DialogueUnityVariableStorage, which
// just stores everything in a Dictionary.
public class ExampleVariableStorage : Yarn.Unity.VariableStorageBehaviour
{

	// Where we actually keeping our variables
	Dictionary<string, float> variables = new Dictionary<string, float> ();

	// A default value to apply when the object wakes up, or 
	// when ResetToDefaults is called
	[System.Serializable]
	public class DefaultVariable
	{
		public string name;
		public float value;
	}

	// Our list of default variables, for debugging.
	public DefaultVariable[] defaultVariables;

	// A UI.Text that will show the current list of all variables
	public UnityEngine.UI.Text debugTextView;

	// Reset to our default values when the game starts
	void Awake ()
	{
		ResetToDefaults ();
	}

	// Erase all variables and reset to default values
	public override void ResetToDefaults ()
	{
		Clear ();
		
		foreach (var variable in defaultVariables) {
			SetNumber ("$" + variable.name, variable.value);
		}
	}

	// Set a variable's value
	public override void SetNumber (string variableName, float number)
	{
		variables [variableName] = number;
	}

	// Get a variable's value, or 0.0 if it doesn't exist
	public override float GetNumber (string variableName)
	{
		float value = 0.0f;
		if (variables.ContainsKey (variableName)) {
			
			value = variables [variableName];
			
		}
		return value;
	}

	// Erase all variables
	public override void Clear ()
	{
		variables.Clear ();
	}

	// If we have a debug view, show the list of all variables in it
	void Update ()
	{
		if (debugTextView != null) {
			var stringBuilder = new System.Text.StringBuilder ();
			foreach (KeyValuePair<string,float> item in variables) {
				stringBuilder.AppendLine (string.Format ("{0} = {1}", 
				                                         item.Key, 
				                                         item.Value));
			}
			debugTextView.text = stringBuilder.ToString ();
		}
	}


}
