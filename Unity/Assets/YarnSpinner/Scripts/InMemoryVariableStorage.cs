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
using Yarn.Unity;

namespace Yarn.Unity {

    /// <summary>
    /// An simple implementation of DialogueUnityVariableStorage, which
    /// stores everything in memory.
    /// </summary>
    /// <remarks>
    /// This class does not perform any saving or loading on its own, but
    /// you can enumerate over the variables by using a `foreach` loop:
    /// 
    /// <![CDATA[
    /// ```csharp    
    /// // 'storage' is an InMemoryVariableStorage    
    /// foreach (var variable in storage) {
    ///         string name = variable.Key;
    ///         Yarn.Value value = variable.Value;
    /// }   
    /// ```
    /// ]]>
    /// 
    /// </remarks>    
    public class InMemoryVariableStorage : VariableStorageBehaviour, IEnumerable<KeyValuePair<string, Yarn.Value>>
    {

        /// Where we actually keeping our variables
        private Dictionary<string, Yarn.Value> variables = new Dictionary<string, Yarn.Value> ();

        /// <summary>
        /// A default value to apply when the object wakes up, or when
        /// ResetToDefaults is called.
        /// </summary>
        [System.Serializable]
        public class DefaultVariable
        {
            /// <summary>
            /// The name of the variable.
            /// </summary>
            /// <remarks>
            /// Do not include the `$` prefix in front of the variable
            /// name. It will be added for you.
            /// </remarks>
            public string name;

            /// <summary>
            /// The value of the variable, as a string.
            /// </summary>
            /// <remarks>
            /// This string will be converted to the appropriate type,
            /// depending on the value of <see cref="type"/>.
            /// </remarks>
            public string value;

            /// <summary>
            /// The type of the variable.
            /// </summary>
            public Yarn.Value.Type type;
        }

        /// <summary>
        /// The list of default variables that should be present in the
        /// InMemoryVariableStorage when the scene loads.
        /// </summary>
        public DefaultVariable[] defaultVariables;

        [Header("Optional debugging tools")]
        
        /// A UI.Text that can show the current list of all variables. Optional.
        [SerializeField] 
        internal UnityEngine.UI.Text debugTextView = null;

        /// Reset to our default values when the game starts
        internal void Awake ()
        {
            ResetToDefaults ();
        }

        /// <summary>
        /// Removes all variables, and replaces them with the variables
        /// defined in <see cref="defaultVariables"/>.
        /// </summary>
        public override void ResetToDefaults ()
        {
            Clear ();

            // For each default variable that's been defined, parse the
            // string that the user typed in in Unity and store the
            // variable
            foreach (var variable in defaultVariables) {
                
                object value;

                switch (variable.type) {
                case Yarn.Value.Type.Number:
                    float f = 0.0f;
                    float.TryParse(variable.value, out f);
                    value = f;
                    break;

                case Yarn.Value.Type.String:
                    value = variable.value;
                    break;

                case Yarn.Value.Type.Bool:
                    bool b = false;
                    bool.TryParse(variable.value, out b);
                    value = b;
                    break;

                case Yarn.Value.Type.Variable:
                    // We don't support assigning default variables from
                    // other variables yet
                    Debug.LogErrorFormat("Can't set variable {0} to {1}: You can't " +
                        "set a default variable to be another variable, because it " +
                        "may not have been initialised yet.", variable.name, variable.value);
                    continue;

                case Yarn.Value.Type.Null:
                    value = null;
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException ();

                }

                var v = new Yarn.Value(value);

                SetValue ("$" + variable.name, v);
            }
        }

        /// <summary>
        /// Stores a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="value">The value to store.</param>
        public override void SetValue (string variableName, Value value)
        {
            // Copy this value into our list
            variables[variableName] = new Yarn.Value(value);
        }

        /// <summary>
        /// Retrieves a <see cref="Value"/> by name.
        /// </summary>
        /// <param name="variableName">The name of the variable to retrieve
        /// the value of.</param>
        /// <returns>The <see cref="Value"/>. If a variable by the name of
        /// <paramref name="variableName"/> is not present, returns a value
        /// representing `null`.</returns>
        public override Value GetValue (string variableName)
        {
            // If we don't have a variable with this name, return the null
            // value
            if (variables.ContainsKey(variableName) == false)
                return Yarn.Value.NULL;
            
            return variables [variableName];
        }

        /// <summary>
        /// Removes all variables from storage.
        /// </summary>
        public override void Clear ()
        {
            variables.Clear ();
        }

        /// If we have a debug view, show the list of all variables in it
        internal void Update ()
        {
            if (debugTextView != null) {
                var stringBuilder = new System.Text.StringBuilder ();
                foreach (KeyValuePair<string,Yarn.Value> item in variables) {
                    string debugDescription;
                    switch (item.Value.type) {
                        case Value.Type.Bool:
                            debugDescription = item.Value.AsBool.ToString();
                            break;
                        case Value.Type.Null:
                            debugDescription = "null";
                            break;
                        case Value.Type.Number:
                            debugDescription = item.Value.AsNumber.ToString();
                            break;
                        case Value.Type.String:
                            debugDescription = $@"""{item.Value.AsString}""";
                            break;
                        default:
                            debugDescription = "<unknown>";
                            break;

                    }
                    stringBuilder.AppendLine (string.Format ("{0} = {1}",
                                                            item.Key,
                                                            debugDescription));
                }
                debugTextView.text = stringBuilder.ToString ();
                debugTextView.SetAllDirty();
            }
        }

        /// <summary>
        /// Returns an <see cref="IEnumerator{T}"/> that iterates over all
        /// variables in this object.
        /// </summary>
        /// <returns>An iterator over the variables.</returns>
        IEnumerator<KeyValuePair<string, Value>> IEnumerable<KeyValuePair<string, Yarn.Value>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, Value>>)variables).GetEnumerator();
        }

        /// <summary>
        /// Returns an <see cref="IEnumerator"/> that iterates over all
        /// variables in this object.
        /// </summary>
        /// <returns>An iterator over the variables.</returns>        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, Value>>)variables).GetEnumerator();
        }
    }
}