namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    // The standard, built-in library of functions and operators.
    internal class StandardLibrary
    {
        /// <summary>
        /// The internal random number generator used by functions like
        /// 'random' and 'dice'.
        /// </summary>
        private static readonly System.Random Random = new Random();

        private static List<string> keys = new List<string>
        {
            "Number.EqualTo",
            "Number.NotEqualTo",
            "Number.Add",
            "Number.Minus",
            "Number.Divide",
            "Number.Multiply",
            "Number.Modulo",
            "Number.UnaryMinus",
            "Number.GreaterThan",
            "Number.GreaterThanOrEqualTo",
            "Number.LessThan",
            "Number.LessThanOrEqualTo",
            "String.EqualTo",
            "String.NotEqualTo",
            "String.Add",
            "Bool.EqualTo",
            "Bool.NotEqualTo",
            "Bool.And",
            "Bool.Or",
            "Bool.Xor",
            "Bool.Not",
            "Enum.EqualTo",
            "Enum.NotEqualTo",
            "random",
            "random_range",
            "random_range_float",
            "dice",
            "min",
            "max",
            "round",
            "round_places",
            "floor",
            "ceil",
            "inc",
            "dec",
            "decimal",
            "int",
            "string",
            "number",
            "bool",
            "format_invariant",
            "format",
        };

        public static Dictionary<string, FunctionDefinition> AllFunctions()
        {
            Dictionary<string, FunctionDefinition> functions = new();
            foreach (var key in keys)
            {
                if (TryGetFunction(key, out var definition))
                {
                    functions.Add(key, definition);
                }
                else
                {
                    throw new ArgumentException($"Was unable to find a definition for the standard library function {key}");
                }
            }
            return functions;
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

        internal static string? GetCanonicalNameForMethod(IType implementingType, string methodName)
        {
            if (implementingType is null)
            {
                throw new System.ArgumentNullException(nameof(implementingType));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new System.ArgumentException($"'{nameof(methodName)}' cannot be null or empty.", nameof(methodName));
            }

            if (implementingType is EnumType)
            {
                // TODO: Come up with a better way for multiple types to share
                // the same methods. The reason why we do this is because if we
                // have two enums, A and B, the current mechanism would come up
                // with a different name for 'EqualTo' for each of them:
                // 'A.EqualTo' and 'B.EqualTo', even though they do the exact
                // same thing. Worse, runners don't know that A and B exist,
                // because they only know to register the built-in types and
                // their methods.
                //
                // A better solution would be to let types identify the
                // canonical names for their methods themselves - i.e. enum A
                // could say 'my EqualTo method is named Enum.EqualTo'.
                //
                // (See also note in the constructor for StandardLibrary.)
                return $"Enum.{methodName}";
            }

            var canonicalName = $"{implementingType.Name}.{methodName}";

            if (keys.Contains(canonicalName))
            {
                return canonicalName;
            }
            else
            {
                return null;
            }
        }

        public static bool TryGetFunction(string functionName, out FunctionDefinition function)
        {
            switch (functionName)
            {
                case "Number.EqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.NotEqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.Add":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.Minus":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.Divide":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.Multiply":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.Modulo":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.UnaryMinus":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.GreaterThan":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.GreaterThanOrEqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.LessThan":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Number.LessThanOrEqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "String.EqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.String);
                    functionType.AddParameter(Types.String);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "String.NotEqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.String);
                    functionType.AddParameter(Types.String);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "String.Add":
                {
                    var functionType = new FunctionType(Types.String);
                    functionType.AddParameter(Types.String);
                    functionType.AddParameter(Types.String);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Bool.EqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Bool.NotEqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Bool.And":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Bool.Or":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Bool.Xor":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Bool.Not":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Boolean);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Enum.EqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Enum);
                    functionType.AddParameter(Types.Enum);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "Enum.NotEqualTo":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Enum);
                    functionType.AddParameter(Types.Enum);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "random":
                {
                    var functionType = new FunctionType(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "random_range":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "random_range_float":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "dice":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "min":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "max":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "round":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "round_places":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "floor":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "ceil":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "inc":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "dec":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "decimal":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "int":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Number);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "string":
                {
                    var functionType = new FunctionType(Types.String);
                    functionType.AddParameter(Types.Any);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "number":
                {
                    var functionType = new FunctionType(Types.Number);
                    functionType.AddParameter(Types.Any);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "bool":
                {
                    var functionType = new FunctionType(Types.Boolean);
                    functionType.AddParameter(Types.Any);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "format_invariant":
                {
                    var functionType = new FunctionType(Types.String);
                    functionType.AddParameter(Types.Any);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
                case "format":
                {
                    var functionType = new FunctionType(Types.String);
                    functionType.AddParameter(Types.String);
                    functionType.AddParameter(Types.Any);
                    function = new()
                    {
                        Name = functionName,
                        functionType = functionType,
                    };
                    return true;
                }
            }

            function = new();
            return false;
        }

        public static ValueTask<Yarn.Value> CallFunc(FunctionDefinition function, Value[] parameters, CancellationToken token)
        {
            // all the standard library functions are synchronous
            // but we'll still have this operate as if it were async because later on we might add some
            // and the rest of the codebase is all async anyways so because we are changing it might as well just change it once
            switch (function.Name)
            {
                // the number operators
                case "Number.EqualTo":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() == parameters[1].ConvertTo<float>());
                }
                case "Number.NotEqualTo":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() != parameters[1].ConvertTo<float>());
                }
                case "Number.Add":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() + parameters[1].ConvertTo<float>());
                }
                case "Number.Minus":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() - parameters[1].ConvertTo<float>());
                }
                case "Number.Divide":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() / parameters[1].ConvertTo<float>());
                }
                case "Number.Multiply":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() * parameters[1].ConvertTo<float>());
                }
                case "Number.Modulo":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() % parameters[1].ConvertTo<float>());
                }
                case "Number.UnaryMinus":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(-parameters[0].ConvertTo<float>());
                }
                case "Number.GreaterThan":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() > parameters[1].ConvertTo<float>());
                }
                case "Number.GreaterThanOrEqualTo":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() >= parameters[1].ConvertTo<float>());
                }
                case "Number.LessThan":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() < parameters[1].ConvertTo<float>());
                }
                case "Number.LessThanOrEqualTo":
                {
                    // we need exactly two parameters
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<float>() <= parameters[1].ConvertTo<float>());
                }
                
                // string operators
                case "String.EqualTo":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<string>() == parameters[1].ConvertTo<string>());
                }
                case "String.NotEqualTo":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<string>() != parameters[1].ConvertTo<string>());
                }
                case "String.Add":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<string>() + parameters[1].ConvertTo<string>());
                }
                
                // the bool operators
                case "Bool.EqualTo":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<bool>() == parameters[1].ConvertTo<bool>());
                }
                case "Bool.NotEqualTo":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<bool>() != parameters[1].ConvertTo<bool>());
                }
                case "Bool.And":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<bool>() && parameters[1].ConvertTo<bool>());
                }
                case "Bool.Or":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<bool>() || parameters[1].ConvertTo<bool>());
                }
                case "Bool.Xor":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(parameters[0].ConvertTo<bool>() ^ parameters[1].ConvertTo<bool>());
                }
                case "Bool.Not":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(!parameters[0].ConvertTo<bool>());
                }

                // enum operators
                case "Enum.EqualTo":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(EnumEqualTo(parameters[0], parameters[1]));
                }
                case "Enum.NotEqualTo":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(!EnumEqualTo(parameters[0], parameters[1]));
                }

#pragma warning disable CA5394 // System.Random is cryptographically insecure
                case "random":
                {
                    if (parameters.Length != 0)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects no parameters but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>((float)Random.NextDouble());
                }
                case "random_range":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    
                    var min = parameters[0].ConvertTo<int>();
                    var max = parameters[1].ConvertTo<int>();
                    return new ValueTask<Value>(Random.Next((int)max - (int)min + 1) + min);
                }
                case "random_range_float":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }

                    var minInclusive = parameters[0].ConvertTo<int>();
                    var maxInclusive = parameters[1].ConvertTo<int>();
                    return new ValueTask<Value>(Random.Next((int)maxInclusive - (int)minInclusive + 1) + minInclusive);
                }
                case "dice":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    var sides = parameters[0].ConvertTo<int>();
                    return new ValueTask<Value>(Random.Next(sides) + 1);
                }
#pragma warning restore CA5394 // System.Random is cryptographically insecure

                // numeric helpers
                case "min":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>(Math.Min(parameters[0].ConvertTo<float>(), parameters[1].ConvertTo<float>()));
                }
                case "max":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>(Math.Max(parameters[0].ConvertTo<float>(), parameters[1].ConvertTo<float>()));
                }
                case "round":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>((int)Math.Round(parameters[0].ConvertTo<float>()));
                }
                case "round_places":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }

                    var number = parameters[0].ConvertTo<float>();
                    var places = parameters[1].ConvertTo<int>();
                    return new ValueTask<Value>((float)Math.Round(number, places));
                }
                case "floor":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>((int)Math.Floor(parameters[0].ConvertTo<float>()));
                }
                case "ceil":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>((int)Math.Ceiling(parameters[0].ConvertTo<float>()));
                }
                case "inc":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    int inc(float value)
                    {
                        if (Decimal(value) == 0)
                        {
                            return (int)(value + 1);
                        }
                        else
                        {
                            return (int)Math.Ceiling(value);
                        }
                    }

                    return new ValueTask<Value>(inc(parameters[0].ConvertTo<float>()));
                }
                case "dec":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    int dec(float value)
                    {
                        if (Decimal(value) == 0)
                        {
                            return (int)value - 1;
                        }
                        else
                        {
                            return (int)Math.Floor(value);
                        }
                    }

                    return new ValueTask<Value>(dec(parameters[0].ConvertTo<float>()));
                }
                case "decimal":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>(Decimal(parameters[0].ConvertTo<float>()));
                }
                case "int":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }

                    return new ValueTask<Value>(Integer(parameters[0].ConvertTo<float>()));
                }

                // converters
                case "string":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(Convert.ToString(parameters[0], System.Globalization.CultureInfo.CurrentCulture));
                }
                case "number":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(Convert.ToSingle(parameters[0], System.Globalization.CultureInfo.CurrentCulture));
                }
                case "bool":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(Convert.ToBoolean(parameters[0], System.Globalization.CultureInfo.CurrentCulture));
                }

                // formatters
                case "format_invariant":
                {
                    if (parameters.Length != 1)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects one parameter but was given {parameters.Length}");
                    }
                    return new ValueTask<Value>(Convert.ToString(parameters[0], System.Globalization.CultureInfo.InvariantCulture));
                }
                case "format":
                {
                    if (parameters.Length != 2)
                    {
                        throw new System.ArgumentException($"Internal error: The function {function.Name} expects two parameters but was given {parameters.Length}");
                    }
                    
                    var formatString = parameters[0].ConvertTo<string>();
                    var argument = parameters[1].ConvertTo<string>();
                    return new ValueTask<Value>(string.Format(System.Globalization.CultureInfo.CurrentCulture, formatString, argument));
                }
                
                default:
                    throw new System.ArgumentException($"Internal error: The function {function.Name} is not known to the standard library");
            }
        }

        private static bool EnumEqualTo(Value a, Value b)
        {
            if (a.InternalValue is string)
            {
                return a.ConvertTo<string>() == b.ConvertTo<string>();
            }
            else
            {
                return a.ConvertTo<int>() == b.ConvertTo<int>();
            }
        }
        private static float Decimal(float value)
        {
            return value - Integer(value);
        }
        private static int Integer(float value)
        {
            return (int)Math.Truncate(value);
        }
    }

    /// <summary>
    /// Lists the available operators that can be used with Yarn values.
    /// </summary>
    internal enum Operator
    {
        /// <summary>A unary operator that returns its input.</summary>
        None,

        /// <summary>A binary operator that represents equality.</summary>
        EqualTo,

        /// <summary>A binary operator that represents a value being
        /// greater than another.</summary>
        GreaterThan,

        /// <summary>A binary operator that represents a value being
        /// greater than or equal to another.</summary>
        GreaterThanOrEqualTo,

        /// <summary>A binary operator that represents a value being less
        /// than another.</summary>
        LessThan,

        /// <summary>A binary operator that represents a value being less
        /// than or equal to another.</summary>
        LessThanOrEqualTo,

        /// <summary>A binary operator that represents
        /// inequality.</summary>
        NotEqualTo,

        /// <summary>A binary operator that represents a logical
        /// or.</summary>
        Or,

        /// <summary>A binary operator that represents a logical
        /// and.</summary>
        And,

        /// <summary>A binary operator that represents a logical exclusive
        /// or.</summary>
        Xor,

        /// <summary>A binary operator that represents a logical
        /// not.</summary>
        Not,

        /// <summary>A unary operator that represents negation.</summary>
        UnaryMinus,

        /// <summary>A binary operator that represents addition.</summary>
        Add,

        /// <summary>A binary operator that represents
        /// subtraction.</summary>
        Minus,

        /// <summary>A binary operator that represents
        /// multiplication.</summary>
        Multiply,

        /// <summary>A binary operator that represents division.</summary>
        Divide,

        /// <summary>A binary operator that represents the remainder
        /// operation.</summary>
        Modulo,
    }
}