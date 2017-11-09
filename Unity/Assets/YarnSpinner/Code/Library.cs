using System;
using System.Collections.Generic;

namespace Yarn
{
    public delegate Object ReturningFunction (params Value[] parameters);
    public delegate void Function (params Value[] parameters);

    public class FunctionInfo {

        // The name of the function, as it exists in the script.
        public string name { get; private set;}

        // The number of parameters this function requires.
        // -1 = the function will accept any number of params
        public int paramCount {get; private set;}

        // The actual implementation of the function.
        // Comes in two flavours: a returning one, and a non-returning one.
        // Doing this means that you don't have to add "return null"
        // to the end of a function if it doesn't return values.
        public Function function { get; private set; }
        public ReturningFunction returningFunction { get; private set; }

        // Does this function return a value?
        public bool returnsValue {
            get {
                return returningFunction != null;
            }
        }

        public Value Invoke(params Value[] parameters) {
            return InvokeWithArray(parameters);
        }

        public Value InvokeWithArray(Value[] parameters) {

            int length;

            if (parameters != null)
                length = parameters.Length;
            else
                length = 0;

            if (IsParameterCountCorrect (length)) {
                if (returnsValue) {
                    return new Value(returningFunction (parameters));
                } else {
                    function (parameters);
                    return Value.NULL; // a null Value
                }
            } else {
                string error = string.Format (
                    "Incorrect number of parameters for function {0} (expected {1}, got {2}",
                    this.name,
                    this.paramCount,
                    parameters.Length);

                throw new InvalidOperationException (error);
            }
        }

        // TODO: support for typed parameters
        // TODO: support for return type
        internal FunctionInfo(string name, int paramCount, Function implementation) {
            this.name = name;
            this.paramCount = paramCount;
            this.function = implementation;
            this.returningFunction = null;
        }

        internal FunctionInfo(string name, int paramCount, ReturningFunction implementation) {
            this.name = name;
            this.paramCount = paramCount;
            this.returningFunction = implementation;
            this.function = null;
        }

        internal bool IsParameterCountCorrect (int parameterCount)
        {
            return paramCount == parameterCount || paramCount == -1;
        }

    }

    // A Library is a collection of callable functions.
    public class Library
    {

        private Dictionary<string, FunctionInfo> functions = new Dictionary<string, FunctionInfo>();

        // Returns a function; throws an exception if it doesn't exist.
        // Use FunctionExists to check for a function's existence.
        public FunctionInfo GetFunction(string name) {
            try {
                return functions [name];
            } catch (KeyNotFoundException) {
                throw new InvalidOperationException (name + " is not a valid function");
            }
        }

        // Loads functions from another library. If the other library
        // contains a function with the same name as ours, ours will be
        // replaced.
        public void ImportLibrary(Library otherLibrary) {
            foreach (var entry in otherLibrary.functions) {
                functions [entry.Key] = entry.Value;
            }
        }

        public void RegisterFunction(FunctionInfo function) {
            functions [function.name] = function;
        }

        public void RegisterFunction(string name, int parameterCount, ReturningFunction implementation) {
            var info = new FunctionInfo (name, parameterCount, implementation);
            RegisterFunction (info);
        }

        public void RegisterFunction(string name, int parameterCount, Function implementation) {
            var info = new FunctionInfo (name, parameterCount, implementation);
            RegisterFunction (info);
        }

        public bool FunctionExists(string name) {
            return functions.ContainsKey (name);
        }

        public void DeregisterFunction(string name) {
            if (functions.ContainsKey(name))
                functions.Remove (name);
        }

    }
}

