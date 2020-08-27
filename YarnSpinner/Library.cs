using System;
using System.Collections.Generic;
using System.Globalization;

namespace Yarn
{
    /// <summary>
    /// A collection of functions that can be called from Yarn programs.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this class yourself. The <see
    /// cref="Dialogue"/> class creates one of its own, which you can
    /// access via the <see cref="Dialogue.Library"/> property.
    /// </remarks>
    /// <seealso cref="Dialogue"/>
    public class Library
    {

        private Dictionary<string, Delegate> delegates = new Dictionary<string, Delegate>();
        
        /// <summary>
        /// Returns a <see cref="Delegate"/> with a given name.
        /// </summary>
        /// <param name="name">The name of the function to
        /// retrieve.</param>
        /// <returns>The <see cref="Delegate"/>.</returns>
        /// <throws cref="InvalidOperationException">Thrown when a function
        /// named `name` is not present in the library.</throws>
        public Delegate GetFunction(string name)
        {
            try
            {
                return delegates[name];
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException($"Function {name} is not present in the library.");
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
            foreach (var entry in otherLibrary.delegates) {
                delegates[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Registers a new function that returns a value, which can be
        /// called from a Yarn program.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="implementation">The <see cref="Func{TResult}"/> to
        /// be invoked when the function is called.</param>
        /// <typeparam name="TResult">The return type of the
        /// function.</typeparam>
        /// <exception cref="ArgumentException">Thrown when a function
        /// named <paramref name="name"/> already exists in the <see
        /// cref="Library"/>.</exception>
        /// <exception cref="ArgumentNullException">Thrown when name is
        /// null.</exception>
        public void RegisterFunction<TResult>(string name, Func<TResult> implementation)
        {
            RegisterFunction(name, (Delegate)implementation);
        }

        /// <inheritdoc cref="RegisterFunction{TResult}(string, Func{TResult})"/>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <typeparam name="T1">The type of the function's first argument.</typeparam>
        public void RegisterFunction<T1, TResult>(string name, Func<T1, TResult> implementation)
        {
            RegisterFunction(name, (Delegate)implementation);
        }

        /// <inheritdoc cref="RegisterFunction{TResult}(string, Func{TResult})"/>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <typeparam name="T1">The type of the function's first argument.</typeparam>
        /// <typeparam name="T2">The type of the function's second argument.</typeparam>
        public void RegisterFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> implementation)
        {
            RegisterFunction(name, (Delegate)implementation);
        }

        /// <inheritdoc cref="RegisterFunction{TResult}(string, Func{TResult})"/>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <typeparam name="T1">The type of the function's first argument.</typeparam>
        /// <typeparam name="T2">The type of the function's second argument.</typeparam>
        /// <typeparam name="T3">The type of the function's third argument.</typeparam>
        public void RegisterFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> implementation)
        {
            RegisterFunction(name, (Delegate)implementation);
        }

        /// <inheritdoc cref="RegisterFunction{TResult}(string, Func{TResult})"/>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <typeparam name="T1">The type of the function's first argument.</typeparam>
        /// <typeparam name="T2">The type of the function's second argument.</typeparam>
        /// <typeparam name="T3">The type of the function's third argument.</typeparam>
        /// <typeparam name="T4">The type of the function's fourth argument.</typeparam>
        public void RegisterFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> implementation)
        {
            RegisterFunction(name, (Delegate)implementation);
        }

        /// <inheritdoc cref="RegisterFunction{TResult}(string, Func{TResult})"/>
        /// <typeparam name="TResult">The return type of the function.</typeparam>
        /// <typeparam name="T1">The type of the function's first argument.</typeparam>
        /// <typeparam name="T2">The type of the function's second argument.</typeparam>
        /// <typeparam name="T3">The type of the function's third argument.</typeparam>
        /// <typeparam name="T4">The type of the function's fourth argument.</typeparam>
        /// <typeparam name="T5">The type of the function's fifth argument.</typeparam>
        public void RegisterFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> implementation)
        {
            RegisterFunction(name, (Delegate)implementation);
        }

        /// <inheritdoc cref="RegisterFunction{TResult}(string,
        /// Func{TResult})"/>
        /// <param name="name">The name of the function.</param>
        /// <param name="implementation">The <see cref="Delegate"/> that
        /// should be invoked when this function is called from Yarn
        /// scripts.</param>
        public void RegisterFunction(string name, Delegate implementation) {
            delegates.Add(name, implementation);
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Library"/> contains a function named `name`.
        /// </summary>
        /// <param name="name">The name of the function to look for.</param>
        /// <returns>`true` if a function exists in this Library; `false` otherwise.</returns>
        public bool FunctionExists(string name) {
            return delegates.ContainsKey(name);
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
            if (FunctionExists(name))
            {
                delegates.Remove(name);
            }
        }
    }
}
