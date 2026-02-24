// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface Library
    {
        public Dictionary<string, FunctionDefinition> allDefinitions { get; }
        public bool TryGetFunctionDefinition(string name, out FunctionDefinition function);
    }

    /// <summary>
    /// A collection of functions that can be called from Yarn programs.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this class yourself. The <see
    /// cref="Dialogue"/> class creates one of its own, which you can
    /// access via the <see cref="Dialogue.Library"/> property.
    /// </remarks>
    /// <seealso cref="Dialogue"/>
    public class BasicFunctionLibrary: Library
    {
        // later make this private but for testing it's easier to be public
        private Dictionary<string, FunctionDefinition> functions = new();
        private Dictionary<string, Delegate> delegates = new();

        public Dictionary<string, FunctionDefinition> allDefinitions
        {
            get
            {
                return functions;
            }
        }

        public bool TryGetFunctionDefinition(string name, out FunctionDefinition function)
        {
            return functions.TryGetValue(name, out function);
        }

        private bool TryGetConcreteTypesFromDelegate(Delegate func, out System.Type[] types, out bool isVariadic)
        {
            if (func == null)
            {
                throw new ArgumentNullException($"The delegate cannot be null!");
            }
            var method = func.Method;

            // Does the return type of this delegate map to a value
            // that Yarn Spinner can use?
            if (Types.TypeMappings.TryGetValue(method.ReturnType, out var yarnReturnType) == false)
            {
                // ok we aren't one of the basic types
                // but we might be one of our allowed async types
                if (BaseAsyncTypeMappings.TryGetValue(method.ReturnType, out yarnReturnType) == false)
                {
                    types = Array.Empty<System.Type>();
                    isVariadic = false;
                    return false;
                }
            }

            System.Reflection.ParameterInfo[] methodParameters = method.GetParameters();
            System.Type[]? parameterTypes = null;
            isVariadic = false;
            if (methodParameters.Length > 0)
            {
                parameterTypes = new System.Type[methodParameters.Length];
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    var isLast = i == methodParameters.Length - 1;

                    System.Reflection.ParameterInfo? paramInfo = methodParameters[i];
                    if (paramInfo.ParameterType == typeof(Value))
                    {
                        types = Array.Empty<System.Type>();
                        return false;
                    }

                    if (paramInfo.IsOptional)
                    {
                        types = Array.Empty<System.Type>();
                        return false;
                    }

                    if (paramInfo.IsOut)
                    {
                        types = Array.Empty<System.Type>();
                        return false;
                    }

                    var isLastParamArray = isLast && paramInfo.ParameterType.IsArray;
                    // Normally, we'd check for the presence of a
                    // System.ParamArrayAttribute, but C# doesn't generate
                    // them for local functions. Instead, assume that if the
                    // last parameter is an array of a valid type, it's a
                    // params array.

                    if (isLastParamArray)
                    {
                        // This is a params array. Is the type of the array valid?
                        if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType.GetElementType(), out _))
                        {
                            isVariadic = true;
                            parameterTypes[i] = paramInfo.ParameterType.GetElementType();
                        }
                        else
                        {
                            types = Array.Empty<System.Type>();
                            isVariadic = false;
                            return false;
                        }
                    }
                    else if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType, out _) == false)
                    {
                        types = Array.Empty<System.Type>();
                        isVariadic = false;
                        return false;
                    }
                    else
                    {
                        parameterTypes[i] = paramInfo.ParameterType;
                    }
                }
            }
            
            types = parameterTypes ?? Array.Empty<System.Type>();
            return true;
        }

        private bool TryMakeFunctionFromDelegate(string name, Delegate func, out FunctionDefinition functionDefinition)
        {
            if (func == null)
            {
                throw new ArgumentNullException($"The delegate for {name} cannot be null!");
            }
            var method = func.Method;
            if (method.ReturnType == typeof(Value))
            {
                // Functions that return the internal type Values are
                // operators, and are type checked by
                // ExpressionTypeVisitor. (Future work: define each
                // polymorph of each operator as a separate function
                // that returns a concrete type, rather than the
                // current method of having a 'Value' wrapper type).
                functionDefinition = new();
                return true;
            }

            // Does the return type of this delegate map to a value
            // that Yarn Spinner can use?
            if (Types.TypeMappings.TryGetValue(method.ReturnType, out var yarnReturnType) == false)
            {
                // ok we aren't one of the basic types
                // but we might be one of our allowed async types
                if (BaseAsyncTypeMappings.TryGetValue(method.ReturnType, out yarnReturnType) == false)
                {
                    functionDefinition = new();
                    return false;
                }
            }

            var functionType = new FunctionType(yarnReturnType);
            var includeMethod = true;
            System.Reflection.ParameterInfo[] array = method.GetParameters();

            if (array.Length > 0)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    var isLast = i == array.Length - 1;

                    System.Reflection.ParameterInfo? paramInfo = array[i];
                    if (paramInfo.ParameterType == typeof(Value))
                    {
                        // Don't type-check this method - it's an operator
                        break;
                    }

                    if (paramInfo.IsOptional)
                    {
                        includeMethod = false;
                        continue;
                    }

                    if (paramInfo.IsOut)
                    {
                        includeMethod = false;
                        continue;
                    }

                    var isLastParamArray = isLast && paramInfo.ParameterType.IsArray;
                    // Normally, we'd check for the presence of a
                    // System.ParamArrayAttribute, but C# doesn't generate
                    // them for local functions. Instead, assume that if the
                    // last parameter is an array of a valid type, it's a
                    // params array.

                    if (isLastParamArray)
                    {
                        // This is a params array. Is the type of the array valid?
                        if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType.GetElementType(), out var yarnParamsElementType))
                        {
                            functionType.VariadicParameterType = yarnParamsElementType;
                        }
                        else
                        {
                            includeMethod = false;
                        }
                    }
                    else if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType, out var yarnParameterType) == false)
                    {
                        includeMethod = false;
                    }
                    else
                    {
                        functionType.AddParameter(yarnParameterType);
                    }
                }
            }

            if (includeMethod == false)
            {
                functionDefinition = new();
                return false;
            }

            functionDefinition = new()
            {
                Name = name,
                functionType = functionType,
            };
            return true;
        }

        private static IReadOnlyDictionary<System.Type, Yarn.IType> BaseAsyncTypeMappings { get; } = new Dictionary<System.Type, Yarn.IType>
        {
            {typeof(System.Threading.Tasks.ValueTask<int>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<float>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<double>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<sbyte>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<byte>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<short>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<ushort>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<uint>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<long>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<ulong>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<decimal>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.ValueTask<string>), Yarn.Types.String},
            {typeof(System.Threading.Tasks.ValueTask<bool>), Yarn.Types.Boolean},

            {typeof(System.Threading.Tasks.Task<int>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<float>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<double>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<sbyte>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<byte>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<short>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<ushort>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<uint>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<long>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<ulong>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<decimal>), Yarn.Types.Number},
            {typeof(System.Threading.Tasks.Task<string>), Yarn.Types.String},
            {typeof(System.Threading.Tasks.Task<bool>), Yarn.Types.Boolean},
        };

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
        public void RegisterFunction(string name, Delegate implementation)
        {
            if (TryMakeFunctionFromDelegate(name, implementation, out var functionDefinition))
            {
                functions.Add(name, functionDefinition);
                delegates.Add(name, implementation);
            }
            else
            {
                throw new System.ArgumentException($"Unable to convert the delegate of {name} into a function definition");
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Library"/> contains a function named <c>name</c>.
        /// </summary>
        /// <param name="name">The name of the function to look for.</param>
        /// <returns><c>true</c> if a function exists in this Library; <c>false</c> otherwise.</returns>
        public bool FunctionExists(string name)
        {
            return functions.ContainsKey(name);
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
                functions.Remove(name);
                delegates.Remove(name);
            }
        }

        public async ValueTask<IConvertible> Invoke(string functionName, IConvertible[] parameters, CancellationToken token)
        {
            if (!delegates.TryGetValue(functionName, out var func))
            {
                throw new System.ArgumentException($"Unable to locate the delegate for {functionName}");
            }

            if (!this.TryGetConcreteTypesFromDelegate(func, out var concreteParameterTypes, out var isVariadic))
            {
                throw new System.ArgumentException($"Unable to determine the concrete parameter types for {functionName}");
            }

            int expectedParameterCount = concreteParameterTypes.Length;
            int providedParameterCount = parameters.Length;
            int requiredParameterCount = 0;
            int variadicParameterCount = 0;

            // we will always need a parameter array the length of the parameters the delegate expects
            object[] delegateParameters = new object[expectedParameterCount];
            // we optionally might need a variadic params array also
            Array? variadicParameters = null;
            
            // if we aren't variadic it's a lot simpler
            // you just need the same number of parameters as are expected
            if (!isVariadic)
            {
                requiredParameterCount = expectedParameterCount;

                if (providedParameterCount != requiredParameterCount)
                {
                    throw new System.InvalidOperationException($"Internal error: {functionName} expects {expectedParameterCount} parameters but only {providedParameterCount} were given.");
                }
            }
            else
            {
                // we are variadic which means
                // - we have a required number of one less than the expected
                // - we have 0 or more variadic parameters

                requiredParameterCount = expectedParameterCount - 1;
                variadicParameterCount = providedParameterCount - requiredParameterCount;

                // now if the number of parameters is less than the required it's an error
                if (providedParameterCount < requiredParameterCount)
                {
                    throw new System.InvalidOperationException($"Internal error: variadic {functionName} expects {expectedParameterCount} parameters but only {providedParameterCount} were given.");
                }

                // if we have zero parameters that is only allowed if this function is one that only accepts a single variadic parameter
                if (providedParameterCount == 0 && expectedParameterCount != 1)
                {
                    throw new System.InvalidOperationException($"Internal error: variadic {functionName} expects {expectedParameterCount} parameters but none were given.");
                }
            }

            // if we have a variadic function we want to declare the array now and add it to the end of the parameter array
            // that way as we walk the parameters we don't have to remember to do any checking and can just slot the values in
            if (isVariadic)
            {
                variadicParameters = Array.CreateInstance(concreteParameterTypes[^1], variadicParameterCount);
                delegateParameters[^1] = variadicParameters;
            }

            // now we run through the provided parameters and jam them into the array at the right point
            for (int i = 0; i < providedParameterCount; i++)
            {
                if (i < requiredParameterCount)
                {
                    delegateParameters[i] = Convert.ChangeType(parameters[i], concreteParameterTypes[i], System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    if (variadicParameters == null)
                    {
                        throw new System.InvalidOperationException($"Internal error: attempting to convert and add parameter {i} to the variadic function \"{functionName}\" but the variadic array is null.");
                    }
                    var variadicValue = Convert.ChangeType(parameters[i], concreteParameterTypes[^1], System.Globalization.CultureInfo.InvariantCulture);
                    variadicParameters.SetValue(variadicValue, i - requiredParameterCount);
                }
            }

            var task = BasicThunk(func, delegateParameters, token);
            IConvertible returnValue = (IConvertible)await task;
            
            if ( returnValue != null)
            {
                return returnValue;
            }
            else
            {
                throw new System.InvalidOperationException($"Internal error: Return value of the function is not an IConvertible");
            }
        }
        private async static ValueTask<Object> BasicThunk(Delegate func, object[] parameters, CancellationToken token)
        {
            var functionName = func.Method.Name;
            var returnType = func.Method.ReturnType;

            // if the method is void we want to early out
            if (returnType == typeof(void))
            {
                throw new System.InvalidOperationException($"Internal error: Functions are required to return a Yarn value but {functionName} is a void function");
            }

            // if the delegate returns a non-async type
            // and the return type is a type yarn intrinsically understands
            // we just immediately run the function and return that, no need to wait around
            if (Types.TypeMappings.TryGetValue(returnType, out _))
            {
                return func.DynamicInvoke(parameters);
            }

            // ok so we aren't a basic type
            // which means we are either a type that can be awaited with a yarn compatible subtype, or we are an unsupported type
            // regardless the next step is the same
            // we invoke the method knowing it will be possible to cast it to something we can await
            // and if it can't then it's an error
            var uncast = func.DynamicInvoke(parameters);

            return uncast switch
            {
                ValueTask<int> task => await task,
                ValueTask<float> task => await task,
                ValueTask<double> task => await task,
                ValueTask<sbyte> task => await task,
                ValueTask<byte> task => await task,
                ValueTask<short> task => await task,
                ValueTask<ushort> task => await task,
                ValueTask<uint> task => await task,
                ValueTask<long> task => await task,
                ValueTask<ulong> task => await task,
                ValueTask<decimal> task => await task,
                ValueTask<string> task => await task,
                ValueTask<bool> task => await task,

                Task<int> task => await task,
                Task<float> task => await task,
                Task<double> task => await task,
                Task<sbyte> task => await task,
                Task<byte> task => await task,
                Task<short> task => await task,
                Task<ushort> task => await task,
                Task<uint> task => await task,
                Task<long> task => await task,
                Task<ulong> task => await task,
                Task<decimal> task => await task,
                Task<string> task => await task,
                Task<bool> task => await task,

                // at this point we are something we don't really understand
                _ => throw new System.InvalidOperationException($"Internal error: Functions are required to return a Yarn value but {functionName} return type is {returnType}"),
            };
        }
        public void Clear()
        {
            functions.Clear();
            delegates.Clear();
        }
    }
}
