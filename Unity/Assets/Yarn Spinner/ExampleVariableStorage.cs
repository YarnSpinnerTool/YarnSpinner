/*

The MIT License (MIT)

Copyright (c) 2015 Secret Lab Pty Ltd and Yarn Spinner contributors.

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
using Yarn.Unity;

// An extremely simple implementation of DialogueUnityVariableStorage, which
// just stores everything in a Dictionary.
public class ExampleVariableStorage : VariableStorageBehaviour
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

	[Header("Optional debugging tools")]
	// A UI.Text that can show the current list of all variables. Optional.
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
