#nullable enable

namespace Yarn.Shared;

using System;
using System.Linq;
using System.Collections.Generic;

public interface ILogger
{
    void Write(object obj);
    void WriteLine(object obj);
    void WriteException(System.Exception ex, string? message = null);

    void Inc();
    void Dec();
    void SetDepth(int depth);
}
public class NullLogger: ILogger
{
    public void Write(object obj){}
    public void WriteLine(object obj){}
    public void WriteException(Exception ex, string? message = null){}
    public void Inc(){}
    public void Dec(){}
    public void SetDepth(int depth){}

    public void Dispose() {}
}

public record Action
{
    public Action(string Name, string FullMethodName, string ShortMethodName, ActionType Type, bool HasTarget, ReturnType ReturnType, NamedType Container, Parameter[] Parameters)
    {
        this.Name = Name;
        this.MethodName = FullMethodName;
        this.ShortMethodName = ShortMethodName;
        this.Type = Type;
        this.IsInstance = HasTarget;
        this.Return = ReturnType;
        this.containingNamedType = Container;
        this.Parameters = Parameters;
    }

    /// <summary>
    /// The name of this action.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// The type of the action.
    /// </summary>
    public ActionType Type { get; internal set; }

    public ReturnType Return { get; internal set; }

    /// <summary>
    /// The fully-qualified name for this method, including the global
    /// prefix.
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// The short calling form of the method, this is also captured by <see cref="MethodName"/> but is more convenient as it doesn't have the little <c>global::</c> bits.
    /// </summary>
    public string? ShortMethodName { get; set; }

    /// <summary>
    /// Whether this action requires a target upon which to be invoked.
    /// </summary>
    /// <remarks>
    /// This is not the same as being not static, as lambdas and local functions aren't static but aren't called on an instance.
    /// </remarks>
    public bool IsInstance { get; internal set; }

    /// <summary>
    /// The declaration type of the action.
    /// </summary>
    public DeclarationType DeclarationType { get; internal set; }

    public bool IsAsync
    {
        get
        {
            return Return switch
            {
                ReturnType.Unknown or ReturnType.Void or ReturnType.String or ReturnType.Number or ReturnType.Boolean or ReturnType.CoroutineVoid or ReturnType.IEnumeratorVoid => false,
                _ => true,
            };
        }
    }

    public Parameter[] Parameters;

    public NamedType containingNamedType;

    public string containingTypeFullName
    {
        get
        {
            return containingNamedType.fullyQualifiedType;
        }
    }
    public string containingTypeShortName
    {
        get
        {
            return containingNamedType.shortType;
        }
    }

    // this returns the number of parameters that we should expect to see in the yarn itself
    // understands that commands are always one more than functions due to the name of the command being part of the invocation itself
    // also know that instance methods have an additional parameter which is the lookup of the target
    // finally it handles the min and max changing if there are defaulted values or arrays
    public (int min, int max) NumberOfParameters
    {
        get
        {
            // instance actions always have their target as a parameter
            int min = this.IsInstance ? 1 : 0;
            int max = this.IsInstance ? 1 : 0;

            // commands always have their name as a parameter
            if (this.Type == ActionType.Command)
            {
                min += 1;
                max += 1;
            }

            foreach (var param in Parameters)
            {
                // arrays need to be the last parameter
                // and this is validated before the action is used so we can early out here
                // but arrays make the max size infinite and don't change the minimum
                if (param.IsArray)
                {
                    // need to pick a size of the most parameters an action can have
                    // it needs to be very large but not int max because when bounds checking we need this number to be +1 if an instance method (for the target name)
                    // and +1 if it is a command (for the command name)
                    // the below blog implies 8192 is the max anyways and that feels big enough to me!
                    // https://www.tabsoverspaces.com/233892-whats-the-maximum-number-of-arguments-for-method-in-csharp-and-in-net
                    return (min, 8192);
                }

                // tokens don't count as a parameter as far as the calling convention is concerned
                // so for example: do(int, token)
                // has 2 parameters as far as C# is concerned
                // but only one in yarns view (do(4))
                // as the token is injected later by the invoker
                if (param is TokenParameter)
                {
                    continue;
                }

                // default valued parameters don't increase the min but do increase the max
                if (param.HasDefaultValue)
                {
                    max += 1;
                    continue;
                }

                // otherwise we need to add one to both, required parameters increase both min and max
                min += 1;
                max += 1;
            }

            return (min, max);
        }
    }
    public bool HasDefaultParameters
    {
        get
        {
            foreach (var p in Parameters)
            {
                if (p.HasDefaultValue)
                {
                    return true;
                }
            }
            return false;
        }
    }
    public bool HasParameterArray
    {
        get
        {
            if (Parameters != null)
            {
                if (Parameters.Count() > 0)
                {
                    return Parameters[Parameters.Count() - 1].IsArray;
                }
            }
            return false;
        }
    }

    public static ParameterTypes isValidYarnableTypeForUnity(NamedType namedType, IReadOnlyList<YarnConverter> converters, ILogger? logger = null)
    {
        logger?.WriteLine($"Attempting to determine the specific type of {namedType.shortType}");
        switch (namedType.specialType)
        {
            case YarnSpecialType.Boolean: return ParameterTypes.Basic;
            case YarnSpecialType.SByte: return ParameterTypes.Basic;
            case YarnSpecialType.Byte: return ParameterTypes.Basic;
            case YarnSpecialType.Int16: return ParameterTypes.Basic;
            case YarnSpecialType.UInt16: return ParameterTypes.Basic;
            case YarnSpecialType.Int32: return ParameterTypes.Basic;
            case YarnSpecialType.UInt32: return ParameterTypes.Basic;
            case YarnSpecialType.Int64: return ParameterTypes.Basic;
            case YarnSpecialType.UInt64: return ParameterTypes.Basic;
            case YarnSpecialType.Decimal: return ParameterTypes.Basic;
            case YarnSpecialType.Single: return ParameterTypes.Basic;
            case YarnSpecialType.Double: return ParameterTypes.Basic;
            case YarnSpecialType.String: return ParameterTypes.Basic;
        }

        logger?.WriteLine("it isn't a basic type");

        // checking if it is token
        if (namedType.shortType == "CancellationToken" || namedType.shortType == "LineCancellationToken")
        {
            logger?.WriteLine("it's a token!");
            return ParameterTypes.Token;
        }

        logger?.WriteLine("type was not a basic type or a token, checking converters");
        logger?.WriteLine($"checking it as {namedType.fullyQualifiedType}");

        if (ConverterCallingString(namedType.fullyQualifiedType ?? "(NULL)", converters, logger) != null)
        {
            logger?.WriteLine($"found a converter for: {namedType.shortType}");
            return ParameterTypes.Converter;
        }

        logger?.WriteLine("unable to find a convertor, checking if it is a unity type");

        // firs we look for game object as it isn't a monobehaviour or component
        if (namedType.fullyQualifiedType == "global::UnityEngine.GameObject")
        {
            logger?.WriteLine("it's a gameobject");
            return ParameterTypes.UnityGameObject;
        }

        // then we look for monobehaviour/component subclasses
        if (namedType.isUnityComponentType)
        {
            return ParameterTypes.UnityComponent;
        }

        logger?.WriteLine("Unable to resolve the type");

        return ParameterTypes.Invalid;
    }

    public static string? ConverterCallingString(string paramType, IReadOnlyList<YarnConverter> converters, ILogger? logger)
    {
        logger?.WriteLine($"Checking {converters.Count} converters for {paramType}");
        logger?.Inc();
        foreach (var converter in converters)
        {
            logger?.WriteLine($"comparing {paramType} to {converter.FullyQualifiedTypeName}");
            if (converter.FullyQualifiedTypeName != paramType)
            {
                continue;
            }
            logger?.Dec();
            return converter.StaticCallingString;
        }
        logger?.Dec();
        return null;
    }

    public string Usage
    {
        get
        {
            List<string> parameterNames = new();
            foreach (var p in Parameters)
            {
                if (p is TokenParameter)
                {
                    continue;
                }
                parameterNames.Add(p.Name);
            }

            if (this.Type == ActionType.Function)
            {
                return $"{this.Name}({string.Join(", ", parameterNames)})";
            }

            parameterNames.Insert(0, this.Name);
            return $"<<{string.Join(" ", parameterNames)}>>";
        }
    }

    // this should probably be beefed up to do more
    // all it really checks is "do we have a finalised type for each parameter"
    // but it should do a full check of everything
    // it early outs at all stages so shouldn't hurt
    public static bool TryValidateAction(Action action, IReadOnlyList<YarnConverter> converters, ILogger? logger = null, bool isUnknownParameterInvalid = true)
    {
        logger?.WriteLine($"Validating {action.Name}");
        
        // there is an early out which is the invalid action
        // this exists solely to be a marker that something has gone wrong but in a way that we want to persist information
        // so there is no need to do more validation, it's invalid by nature
        if (action is InvalidAction)
        {
            return false;
        }

        for (int i = 0; i < action.Parameters.Length; i++)
        {
            var parameter = action.Parameters[i];

            if (parameter == null)
            {
                logger?.WriteLine($"Parameter at {i} is null");
                return false;
            }

            // if we aren't unknown it means we are either already resolved, a basic type, or a token
            // regardless we can just skip clean over this one for now
            if (parameter is not UnknownParameter up)
            {
                logger?.WriteLine($"Parameter is a resolved type: {parameter.GetType()}");
                continue;
            }

            // first we see if we are a converter
            YarnConverter? converter = null;
            foreach (var c in converters)
            {
                if (c.FullyQualifiedTypeName == up.Type.fullyQualifiedType)
                {
                    converter = c;
                    break;
                }
            }

            // we failed conversion but we might still end up being a component
            if (converter == null)
            {
                logger?.WriteLine($"Parameter is not a converter");
                if (up.Type.isUnityComponentType)
                {
                    logger?.WriteLine($"but is a ");
                    action.Parameters[i] = new ComponentParameter(up.Name, up.Type, up.IsArray, up.IsOut, up.HasDefaultValue, up.DefaultValueDisplay, up.IsNodeAttributed, up.AttributedEnumSubtype);
                }
                else
                {
                    logger?.WriteLine($"Parameter could not be resolved");
                    if (isUnknownParameterInvalid)
                    {
                        return false;
                    }
                }
            }
            else
            {
                logger?.WriteLine($"Parameter resolved as a converted type");
                action.Parameters[i] = new ConverterParameter(up.Name, converter, up.IsArray, up.IsOut, up.HasDefaultValue, up.DefaultValueDisplay, up.IsNodeAttributed, up.AttributedEnumSubtype);
            }
        }
        
        // the final check is if the action is an instance method we need to check that we have a converter for this action type
        if (!action.IsInstance)
        {
            logger?.WriteLine($"Action is static so needs no converter");
            return true;
        }
        logger?.WriteLine($"Action is instance so needs a converter");

        var actionType = Action.isValidYarnableTypeForUnity(action.containingNamedType, converters, logger);
        if (actionType == ParameterTypes.UnityComponent || actionType == ParameterTypes.Converter)
        {
            logger?.WriteLine($"Found a converter for it");
            return true;
        }

        logger?.WriteLine($"after analysing {action.Name} with converters it was still a {actionType}");
        return false;
    }
}

public record InvalidAction(string Name, ActionType Type): Action(Name, "invalid", "invalid", Type, false, ReturnType.Void, new NamedType(), []) { }

public record NamedType
{
    // these represent different ways of looking at the type name
    // all kinda are the same thing but with different amounts of detail
    public readonly string fullyQualifiedType;  // global::Yarn.Unity.LineCancellationToken
    public readonly string shortType;           // LineCancellationToken
    public readonly string longType;            // Yarn.Unity.LineCancellationToken
    public readonly YarnSpecialType specialType;

    public readonly bool isUnityComponentType;

    public NamedType(string fullyQualifiedType, string shortType, string longType, YarnSpecialType specialType, bool isUnityComponentType)
    {
        this.fullyQualifiedType = fullyQualifiedType;
        this.shortType = shortType;
        this.longType = longType;
        this.specialType = specialType;
        this.isUnityComponentType = isUnityComponentType;
    }

    public NamedType()
    {
        fullyQualifiedType = "invalid";
        shortType = "invalid";
        longType = "invalid";
        specialType = YarnSpecialType.Other;
        isUnityComponentType = false;
    }
}

public enum ActionType
{
    /// <summary>
    /// The method represents a command.
    /// </summary>
    Command,
    /// <summary>
    /// The method represents a function.
    /// </summary>
    Function,
    /// <summary>
    /// The method may have been intended to be an action, but its type
    /// cannot be determined.
    /// </summary>
    Invalid,
    /// <summary>
    /// The method is not a Yarn action.
    /// </summary>
    NotAnAction,
}

public enum ReturnType
{
    /// <summary>
    /// 
    /// </summary>
    Unknown,

    /// <summary>
    /// Action returns void.
    /// </summary>
    Void, 

    /// <summary>
    /// Action returns effective void as part of returning an awaitable async type
    /// </summary>
    AsyncVoid,

    /// <summary>
    /// Action returns effective void through being a unity coroutine.
    /// </summary>
    /// <remarks>
    /// <para>Unity cororoutines may or may not actually be async, just that they can be.
    /// Dialogue Runners should check the return value of the action to determine whether to block on the method call or not.
    /// </para>
    /// <para>Only applies to Unity</para></remarks>
    CoroutineVoid,

    /// <summary>
    /// Action returns effective void through being an ienumerator inside of Unity
    /// </summary>
    /// <remarks>Only applies to Unity</remarks>
    IEnumeratorVoid,

    /// <summary>
    /// Action returns a string
    /// </summary>
    String,

    /// <summary>
    /// Action returns a number, can be an integer or real.
    /// </summary>
    Number,

    /// <summary>
    /// Action returns a boolean
    /// </summary>
    Boolean,

    /// <summary>
    /// Action returns a string but through an async awaitable type
    /// </summary>
    AsyncString,
    /// <summary>
    /// Action returns a number but through an async awaitable type
    /// </summary>
    AsyncNumber,
    /// <summary>
    /// Action returns a boolean but through an async awaitable type
    /// </summary>
    AsyncBoolean,
}

public enum ParameterTypes
{
    Unknown, // this represents the situation where we MAY be a converted type but are unsure at this stage
    Invalid,
    Basic, // I might need to change this into: string, number, token, etc etc
    Token,
    UnityGameObject,
    UnityComponent,
    Converter,
}

public record YarnEnum(string Name, YarnEnum.BackingType Backing)
{
    public enum BackingType
    {
        Int, String,
    }
}

public enum DeclarationType
{
    /// <summary>
    /// </summary>
    /// The action is declared via a YarnCommand or YarnFunction attribute.
    Attribute,
    /// <summary>
    /// The action is declared by calling AddCommandHandler or AddFunction
    /// on a DialogueRunner.
    /// </summary>
    DirectRegistration
}

public record YarnConverter
{
    public string FullyQualifiedTypeName;
    public string StaticCallingString;

    public YarnConverter(string FullyQualifiedTypeName, string StaticCallingString)
    {
        this.FullyQualifiedTypeName = FullyQualifiedTypeName;
        this.StaticCallingString = StaticCallingString;
    }
}

public enum YarnSpecialType
{
    Other,
    Boolean,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Decimal,
    Single,
    Double,
    String,
}