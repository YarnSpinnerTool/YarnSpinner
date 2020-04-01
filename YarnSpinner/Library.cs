using System;
using System.Collections.Generic;
using System.Globalization;

namespace Yarn
{
    /// <summary>
    /// Represents a function that can be called from Yarn programs that
    /// returns a function.
    /// </summary>
    /// <param name="parameters">The parameters that this function has
    /// received.</param>
    /// <returns>The returned value from this function.</returns>
    /// <seealso cref="FunctionInfo"/>
    /// <seealso cref="Function"/>
    /// <seealso cref="Library"/>
    public delegate object ReturningFunction (params Value[] parameters);

#pragma warning disable CA1716 // Identifiers should not match keywords
    /// <summary>
    /// Represents a function that can be called from Yarn programs.
    /// </summary>
    /// <param name="parameters">The parameters that this function has
    /// received.</param>
    /// <seealso cref="FunctionInfo"/>
    /// <seealso cref="ReturningFunction"/>
    /// <seealso cref="Library"/>
    public delegate void Function (params Value[] parameters);
#pragma warning restore CA1716 // Identifiers should not match keywords

    /// <summary>
    /// Represents a function in a <see cref="Library"/>.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this class yourself. Instead, use
    /// the <see cref="Library"/> class's <see
    /// cref="Library.RegisterFunction(string, int, Function)"/> and
    /// <see cref="Library.RegisterFunction(string, int,
    /// ReturningFunction)"/> methods to create new functions.
    /// </remarks>
    /// <seealso cref="Function"/>
    /// <seealso cref="ReturningFunction"/>
    public class FunctionInfo {

        /// <summary>
        /// The name of the function, as it exists in the script.
        /// </summary>
        public string name { get; private set;}

        /// <summary>
        /// Gets and sets the number of parameters this function requires.
        /// </summary>
        /// <remarks>
        /// If this is set to -1, the function will accept any number of parameters.
        /// </remarks>
        public int paramCount {get; private set;}

        // The actual implementation of the function.
        // Comes in two flavours: a returning one, and a non-returning one.
        // Doing this means that you don't have to add "return null"
        // to the end of a function if it doesn't return values.
        private Function function { get; set; }
        private ReturningFunction returningFunction { get; set; }

        // Does this function return a value?

        /// <summary>Gets a value indicating whether this function returns a value or not.</summary>
        public bool returnsValue {
            get {
                return returningFunction != null;
            }
        }

        /// <summary>
        /// Invokes this function.
        /// </summary>
        /// <param name="parameters">The parameters to pass to the
        /// function.</param>
        /// <returns>The <see cref="Value"/> returned by this function. If
        /// <see cref="returnsValue"/> is `false`, the value will be a
        /// `null` value.</returns>
        public Value Invoke(params Value[] parameters) {
            return InvokeWithArray(parameters);
        }

        /// <summary>
        /// Invokes this function.
        /// </summary>
        /// <param name="parameters">The parameters to pass to the
        /// function.</param>
        /// <returns>The <see cref="Value"/> returned by this function. If
        /// <see cref="returnsValue"/> is `false`, the value will be a
        /// `null` value.</returns>
        internal Value InvokeWithArray(Value[] parameters)
        {

            int numberOfParameters = parameters?.Length ?? 0;

            if (paramCount == numberOfParameters || paramCount == -1)
            {
                if (returnsValue)
                {
                    return new Value(returningFunction(parameters));
                }
                else
                {
                    function(parameters);
                    return Value.NULL; // a null Value
                }
            }
            else
            {
                string error = string.Format(CultureInfo.CurrentCulture,
                    "Incorrect number of parameters for function {0} (expected {1}, got {2}",
                    this.name,
                    this.paramCount,
                    parameters.Length);

                throw new InvalidOperationException(error);
            }
        }

        // TODO: support for typed parameters
        // TODO: support for return type

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionInfo"/> class.
        /// </summary>
        /// <param name="name">The name of this function.</param>
        /// <param name="paramCount">The number of parameters that this function expects to receive.</param>
        /// <param name="implementation">The <see cref="Function"/> that should be invoked when the function is called.</param>
        internal FunctionInfo(string name, int paramCount, Function implementation) {
            this.name = name;
            this.paramCount = paramCount;
            this.function = implementation;
            this.returningFunction = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionInfo"/> class.
        /// </summary>
        /// <param name="name">The name of this function.</param>
        /// <param name="paramCount">The number of parameters that this function expects to receive.</param>
        /// <param name="implementation">The <see cref="ReturningFunction"/> that should be invoked when the function is called.</param>
        internal FunctionInfo(string name, int paramCount, ReturningFunction implementation) {
            this.name = name;
            this.paramCount = paramCount;
            this.returningFunction = implementation;
            this.function = null;
        }

    }

    /// <summary>
    /// A collection of functions that can be called from Yarn programs.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this class yourself. The <see
    /// cref="Dialogue"/> class creates one of its own, which you can
    /// access via the <see cref="Dialogue.library"/> property.
    /// </remarks>
    /// <seealso cref="FunctionInfo"/>
    /// <seealso cref="Dialogue"/>
    public class Library
    {

        private readonly Dictionary<string, FunctionInfo> functions = new Dictionary<string, FunctionInfo>();

        // Returns a function; throws an exception if it doesn't exist.
        // Use FunctionExists to check for a function's existence.

        /// <summary>
        /// Returns a <see cref="FunctionInfo"/> with a given name.
        /// </summary>
        /// <param name="name">The name of the function to
        /// retrieve.</param>
        /// <returns>The <see cref="FunctionInfo"/>.</returns>
        /// <throws cref="InvalidOperationException">Thrown when a function
        /// named `name` is not present in the library.</throws>
        public FunctionInfo GetFunction(string name)
        {
            try
            {
                return functions[name];
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException(name + " is not a valid function");
            }
        }

        /// <summary>
        /// Loads functions from another <see cref="Library"/>.
        /// </summary>
        /// <param name="otherLibrary">The library to import functions from.</param>
        /// <remarks>
        /// If the other library contains a function with the same name as
        /// one in this library, the function in the other library takes
        /// precedence.
        /// </remarks>
        public void ImportLibrary(Library otherLibrary)
        {
            foreach (var entry in otherLibrary.functions)
            {
                functions[entry.Key] = entry.Value;
            }
        }

        internal void RegisterFunction(FunctionInfo function)
        {
            functions[function.name] = function;
        }

        /// <summary>
        /// Registers a new function that returns a value, which can be
        /// called from a Yarn program.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="parameterCount">The number of parameters that this
        /// function expects to receive.</param>
        /// <param name="implementation">The <see
        /// cref="ReturningFunction"/> to be invoked when the function is
        /// called.</param>
        /// <remarks>If `parameterCount` is -1, the function expects to
        /// receive any number of parameters.
        ///
        /// If a function named `name` already exists in this Library, it
        /// will be replaced.
        /// </remarks>
        public void RegisterFunction(string name, int parameterCount, ReturningFunction implementation)
        {
            var info = new FunctionInfo(name, parameterCount, implementation);
            RegisterFunction(info);
        }

        /// <summary>
        /// Registers a new function that returns a value, which can be
        /// called from a Yarn program.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="parameterCount">The number of parameters that this
        /// function expects to receive.</param>
        /// <param name="implementation">The <see
        /// cref="Function"/> to be invoked when the function is
        /// called.</param>
        /// <remarks>If `parameterCount` is -1, the function expects to
        /// receive any number of parameters.
        ///
        /// If a function named `name` already exists in this Library, it
        /// will be replaced.
        /// </remarks>        
        public void RegisterFunction(string name, int parameterCount, Function implementation)
        {
            var info = new FunctionInfo(name, parameterCount, implementation);
            RegisterFunction(info);
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Library"/> contains a function named `name`.
        /// </summary>
        /// <param name="name">The name of the function to look for.</param>
        /// <returns>`true` if a function exists in this Library; `false` otherwise.</returns>
        public bool FunctionExists(string name) {
            return functions.ContainsKey (name);
        }

        /// <summary>
        /// Removes a function from the Library.
        /// </summary>
        /// <param name="name">The name of the function to remove.</param>
        /// <remarks>
        /// If no function with the given name is present in the Library,
        /// this method does nothing.
        /// </remarks>
        public void DeregisterFunction(string name)
        {
            if (functions.ContainsKey(name))
            {
                functions.Remove(name);
            }
        }
    }
}
