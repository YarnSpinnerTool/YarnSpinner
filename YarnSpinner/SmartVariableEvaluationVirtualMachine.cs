using System;
using System.Collections.Generic;
using System.Globalization;

namespace Yarn
{

    /// <summary>
    /// A lightweight, static 'virtual machine' designed for evaluating smart
    /// variables.
    /// </summary>
    /// <remarks>
    /// This class implements a subset of the <see cref="Instruction.Opcode"/>
    /// operations, and is designed for evaluating smart variable implementation
    /// nodes.
    /// </remarks>
    internal static class SmartVariableEvaluationVirtualMachine
    {
        private static readonly Stack<IConvertible> _stack = new Stack<IConvertible>(32);

        /// <summary>
        /// Evaluates a smart variable.
        /// </summary>
        /// <typeparam name="T">The type of the variable to return.</typeparam>
        /// <param name="name">The name of the smart variable.</param>
        /// <param name="variableAccess">An <see cref="IVariableAccess"/> object
        /// that can be used for fetching variable values.</param>
        /// <param name="library">A <see cref="Library"/> containing functions
        /// to use while evaluating the variable.</param>
        /// <param name="result">On return, the computed value of the smart
        /// variable.</param>
        /// <returns><see langword="true"/> if the variable could be fetched as
        /// type <typeparamref name="T"/>; <see langword="false"/>
        /// otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref
        /// name="variableAccess"/> or <paramref name="library"/> is
        /// null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref
        /// name="name"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an error
        /// occurs during the evaluation of the variable.</exception>
        public static bool TryGetSmartVariable<T>(
            string name,
            IVariableAccess variableAccess,
            Library library,
            out T result)
        {
            if (variableAccess is null)
            {
                throw new ArgumentNullException(nameof(variableAccess));
            }

            if (library is null)
            {
                throw new ArgumentNullException(nameof(library));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
            }

            int startStackValue = _stack.Count;

            var program = variableAccess.Program;

            if (program.Nodes.TryGetValue(name, out Node smartVariableNode) == false)
            {
                result = default!;
                return false;
            }

            try
            {
                foreach (var instruction in smartVariableNode.Instructions)
                {
                    if (EvaluateInstruction(instruction, variableAccess, library, _stack) == false)
                    {
                        break;
                    }
                }
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException($"Error when evaluating smart variable {name}: {e.Message}");
            }

            if (_stack.Count < 1)
            {
                throw new System.InvalidOperationException("Error when evaluating smart variable: stack did not contain a value after evaluation");
            }

            IConvertible calculatedResult = _stack.Pop();

            int endStackValue = _stack.Count;

            if (startStackValue != endStackValue)
            {
                throw new InvalidOperationException($"Error when evaluating smart variable: stack had {endStackValue - startStackValue} dangling value(s)");
            }

            return TryConvertToType(calculatedResult, out result);
        }

        private static bool TryConvertToType<T>(IConvertible value, out T result)
        {
            if (value.GetType() == typeof(T)
                || typeof(T).IsAssignableFrom(value.GetType()))
            {
                result = (T)value;
                return true;
            }

            try
            {
                result = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch (ArgumentException)
            {
                result = default!;
                return false;
            }
        }

        private static bool EvaluateInstruction(
            Instruction instruction,
            IVariableAccess variableAccess,
            Library library,
            Stack<IConvertible> stack)
        {
            switch (instruction.Opcode)
            {
                case Instruction.Types.OpCode.PushString:
                    stack.Push(instruction.Operands[0].StringValue);
                    break;
                case Instruction.Types.OpCode.PushFloat:
                    stack.Push(instruction.Operands[0].FloatValue);
                    break;
                case Instruction.Types.OpCode.PushBool:
                    stack.Push(instruction.Operands[0].BoolValue);
                    break;
                case Instruction.Types.OpCode.Pop:
                    stack.Pop();
                    break;
                case Instruction.Types.OpCode.CallFunc:
                    CallFunction(instruction, library, stack);
                    break;
                case Instruction.Types.OpCode.PushVariable:
                    string variableName = instruction.Operands[0].StringValue;
                    variableAccess.TryGetValue<IConvertible>(variableName, out var variableContents);
                    stack.Push(variableContents);
                    break;
                case Instruction.Types.OpCode.Stop:
                    return false;
                default:
                    throw new InvalidOperationException($"Invalid opcode {instruction.Opcode}");
            }

            // Return true to indicate that we should continue
            return true;
        }

        private static void CallFunction(Instruction i, Library Library, Stack<IConvertible> stack)
        {
            // Get the function to call
            var functionName = i.Operands[0].StringValue;

            var function = Library.GetFunction(functionName);

            // Get the parameters that we'll pass to the function
            var parameterInfos = function.Method.GetParameters();

            var expectedParamCount = parameterInfos.Length;

            // Expect the compiler to have placed the number of parameters
            // actually passed at the top of the stack.
            var actualParamCount = Convert.ToInt32(stack.Pop(), CultureInfo.InvariantCulture);

            if (expectedParamCount != actualParamCount)
            {
                throw new InvalidOperationException($"Function {functionName} expected {expectedParamCount} parameters, but received {actualParamCount}");
            }

            // Get the parameter values, which were pushed in reverse
            var parametersToUse = new object[actualParamCount];

            for (int param = actualParamCount - 1; param >= 0; param--)
            {
                var value = stack.Pop();
                var parameterType = parameterInfos[param].ParameterType;

                if (parameterType == typeof(Value))
                {
                    if (Types.TypeMappings.TryGetValue(value.GetType(), out var yarnType))
                        parametersToUse[param] = new Value(yarnType, value);
                }
                else
                {
                    parametersToUse[param] = Convert.ChangeType(value, parameterType, CultureInfo.InvariantCulture);
                }
            }

            // Invoke the function
            try
            {
                IConvertible returnValue = (IConvertible)function.DynamicInvoke(parametersToUse);
                // If the function returns a value, push it
                bool functionReturnsValue = function.Method.ReturnType != typeof(void);

                if (functionReturnsValue)
                {
                    stack.Push(returnValue);
                }
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                // The function threw an exception. Re-throw the exception it
                // threw.
                throw ex.InnerException;
            }
        }
    }
}
