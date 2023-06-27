// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

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
        internal Dictionary<string, Delegate> Delegates = new Dictionary<string, Delegate>();
        
        /// <summary>
        /// Returns a <see cref="Delegate"/> with a given name.
        /// </summary>
        /// <param name="name">The name of the function to
        /// retrieve.</param>
        /// <returns>The <see cref="Delegate"/>.</returns>
        /// <throws cref="InvalidOperationException">Thrown when a function
        /// named <c>name</c> is not present in the library.</throws>
        public Delegate GetFunction(string name)
        {
            try
            {
                return Delegates[name];
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
            if (otherLibrary == null)
            {
                return;
            }
            foreach (var entry in otherLibrary.Delegates)
            {
                Delegates[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Registers a new function that returns a value, which can be
        /// called from a Yarn program.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="implementation">The method to
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
        public void RegisterFunction(string name, Delegate implementation) {
            Delegates.Add(name, implementation);
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Library"/> contains a function named <c>name</c>.
        /// </summary>
        /// <param name="name">The name of the function to look for.</param>
        /// <returns><c>true</c> if a function exists in this Library; <c>false</c> otherwise.</returns>
        public bool FunctionExists(string name) {
            return Delegates.ContainsKey(name);
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
                Delegates.Remove(name);
            }
        }

        /// <summary>
        /// Registers the methods found inside a type.
        /// </summary>
        /// <param name="type">The type to register methods from.</param>
        protected void RegisterMethods(IType type)
        {
            if (type == null)
            {
                // we weren't given any type to work with
                return;
            }
            
            var methods = type.Methods;

            if (methods == null)
            {
                // this Type declares no methods; nothing to do
                return;
            }

            foreach (var methodDefinition in methods)
            {
                var methodName = methodDefinition.Key;
                var methodImplementation = methodDefinition.Value;

                var canonicalName = TypeUtil.GetCanonicalNameForMethod(type, methodName);

                this.RegisterFunction(canonicalName, methodImplementation);
            }
        }

        /// <summary>
        /// Generates a unique tracking variable name.
        /// This is intended to be used to generate names for visting.
        /// Ideally these will very reproduceable and sensible.
        /// For now it will be something terrible and easy.
        /// </summary>
        /// <param name="nodeName">The name of the node that needs to
        /// have a tracking variable created.</param>
        /// <returns>The new variable name.</returns>
        public static string GenerateUniqueVisitedVariableForNode(string nodeName)
        {
            return $"$Yarn.Internal.Visiting.{nodeName}";
        }
    }
}
