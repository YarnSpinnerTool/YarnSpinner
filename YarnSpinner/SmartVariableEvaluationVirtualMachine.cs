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
        private static int programCounter = 0;
        private static readonly Stack<IConvertible> _stack = new Stack<IConvertible>(32);
        private static readonly List<VirtualMachine.LineGroupCandidate> _lineGroupCandidates = new List<VirtualMachine.LineGroupCandidate>();

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

            programCounter = 0;

            try
            {
                while (programCounter < smartVariableNode.Instructions.Count) {
                    var instruction = smartVariableNode.Instructions[programCounter];
                    if (EvaluateInstruction(instruction, variableAccess, library, _stack, ref programCounter) == false)
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

        public static IEnumerable<Yarn.Saliency.IContentSaliencyOption> GetAvailableContentForNodeGroup(
            string nodeGroupName,
            IVariableAccess variableAccess,
            Library library
            )
        {

            var name = nodeGroupName;

            var program = variableAccess.Program;

            if (program.Nodes.TryGetValue(name, out Node nodeGroupEntryPointNode) == false)
            {
                throw new ArgumentException($"Error getting available content for node group {nodeGroupName}: not a valid node group name");
            }

            _lineGroupCandidates.Clear();

            programCounter = 0;

            try
            {
                while (true) {
                    var instruction = nodeGroupEntryPointNode.Instructions[programCounter];

                    if (EvaluateInstruction(instruction, variableAccess, library, _stack, ref programCounter) == false)
                    {
                        break;
                    }
                }
                
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException($"Error when evaluating smart variable {name}: {e.Message}");
            }

            _stack.Clear();
            programCounter = default;
            var result = new List<Yarn.Saliency.IContentSaliencyOption>(_lineGroupCandidates);

            _lineGroupCandidates.Clear();
            
            return result;
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
            Stack<IConvertible> stack,
            ref int programCounter)
        {
            switch (instruction.InstructionTypeCase)
            {
                case Instruction.InstructionTypeOneofCase.PushString:
                    stack.Push(instruction.PushString.Value);
                    break;
                case Instruction.InstructionTypeOneofCase.PushFloat:
                    stack.Push(instruction.PushFloat.Value);
                    break;
                case Instruction.InstructionTypeOneofCase.PushBool:
                    stack.Push(instruction.PushBool.Value);
                    break;
                case Instruction.InstructionTypeOneofCase.Pop:
                    stack.Pop();
                    break;
                case Instruction.InstructionTypeOneofCase.CallFunc:
                    programCounter += 1;
                    return CallFunction(instruction, library, stack);
                case Instruction.InstructionTypeOneofCase.PushVariable:
                    string variableName = instruction.PushVariable.VariableName;
                    variableAccess.TryGetValue<IConvertible>(variableName, out var variableContents);
                    stack.Push(variableContents);
                    break;
                case Instruction.InstructionTypeOneofCase.JumpIfFalse:
                    if (_stack.Peek().ToBoolean(CultureInfo.InvariantCulture) == false)
                    {
                        programCounter = instruction.JumpIfFalse.Destination;
                    }
                    else
                    {
                        programCounter += 1;
                    }
                    return true;

                case Instruction.InstructionTypeOneofCase.Stop:
                    return false;
                default:
                    throw new InvalidOperationException($"Invalid opcode {instruction.InstructionTypeCase}");
            }

            programCounter += 1;

            // Return true to indicate that we should continue
            return true;
        }

        private static bool CallFunction(Instruction i, Library Library, Stack<IConvertible> stack)
        {
            // Get the function to call
            var functionName = i.CallFunc.FunctionName;

            // If functionName is a special-cased internal compiler
            // function, handle that
            if (functionName.Equals(VirtualMachine.AddLineGroupCandidateFunctionName, StringComparison.Ordinal))
            {
                HandleAddLineGroupCandidate();
                return true;
            }

            if (functionName.Equals(VirtualMachine.SelectLineGroupCandidateFunctionName, StringComparison.Ordinal))
            {
                return false;
            }

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

            return true;
        }

        private static void HandleAddLineGroupCandidate()
        {
            // 'Add Line Group Candidate' expects 3 parameters pushed in reverse order:
            // -label (str)
            // - condition count (num)
            // - line id (str)
            var actualParamCount = _stack.Pop().ToInt32(CultureInfo.InvariantCulture);
            const int expectedParamCount = 3;

            if (expectedParamCount != actualParamCount)
            {
                throw new InvalidOperationException($"Function {VirtualMachine.AddLineGroupCandidateFunctionName} expected {expectedParamCount} parameters, but received {actualParamCount}");
            }

            _lineGroupCandidates.Add(new VirtualMachine.LineGroupCandidate
            {
                DestinationIfSelected = _stack.Pop().ToInt32(CultureInfo.InvariantCulture),
                ConditionValueCount = _stack.Pop().ToInt32(CultureInfo.InvariantCulture),
                ContentID = _stack.Pop().ToString(CultureInfo.InvariantCulture)
            });
        }
    }
}
