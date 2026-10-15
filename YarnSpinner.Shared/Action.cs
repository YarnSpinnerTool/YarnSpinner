// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.Shared;

using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Provides a common interface to logging during source gen to support any different type of logger.
/// Technically for the source gen system but I suppose anything can use it...
/// </summary>
/// <remarks>
/// Logging is extremely intensive and will generate enormous logs very quickly due to the nature of how source generators and analysers work.
/// You almost certainly do not need this interface, nor type conforming to it, and if you do beware here be dragons.
/// </remarks>
public interface ILogger
{
    /// <summary>
    /// The standard "write a single line containing the value" of a logger.
    /// </summary>
    /// <remarks>
    /// This line should be indented by the current depth of the logger
    /// </remarks>
    /// <param name="obj">The value to be written, will generally always be a string</param>
    void WriteLine(object obj);
    
    /// <summary>
    /// Logs an exception encountered during
    /// </summary>
    /// <param name="ex">The exception to be logged</param>
    /// <param name="message">An optional message to go along with it</param>
    void WriteException(System.Exception ex, string? message = null);

    /// <summary>
    /// Increment the current depth of the logger
    /// </summary>
    void Inc();
    /// <summary>
    /// Decrement the current depth of the logger
    /// </summary>
    void Dec();
}

/// <summary>
/// The standard logger that is used when logging isn't necessary.
/// This exists so that you don't have to null check every single instance of the logger.
/// </summary>
/// <remarks>
/// You do not make your own instances of this but instead use the static <see cref="Default"/> instance.
/// </remarks>
public class NullLogger: ILogger
{
    private NullLogger() {}
    
    /// <inheritdoc/>
    public void WriteLine(object obj){}
    /// <inheritdoc/>
    public void WriteException(Exception ex, string? message = null){}
    /// <inheritdoc/>
    public void Inc(){}
    /// <inheritdoc/>
    public void Dec(){}

    private static readonly NullLogger defaultLogger = new NullLogger();
    
    /// <summary>
    /// The default and only instance of the logger, if you are using this class you want to use this
    /// </summary>
    public static NullLogger Default
    {
        get
        {
            return defaultLogger;
        }
    }
}

/// <summary>
/// Represents the concept of a yarn action, so either a command or a function
/// </summary>
public record Action
{
    /// <summary>
    /// Creates a new action representing a command or a function.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Actions are a common shared type across numerous different parts of Yarn Spinner and it's not expected that you would be creating one yourself.
    /// They are made by the source generator and analysers and used as part of the diagnostic system, the command and function invocation system, ysls generation, and compilation.
    /// </para>
    /// <para>
    /// This class should be always kept in a form where it cache and comparison friendly as this type is a common output of a lot of other elements of the analysis pipeline which do caching.
    /// Types contained within should always be either a basic type or a record basically.
    /// </para>
    /// </remarks>
    /// <param name="Name">The name of the action itself. it's Yarn Name</param>
    /// <param name="FullMethodName">The fully qualified name of the method that is being used to define this action</param>
    /// <param name="ShortMethodName">A shorter convenience form of the full method name</param>
    /// <param name="Type">Is the action a command or function?</param>
    /// <param name="HasTarget">Is the action an instance action or not?</param>
    /// <param name="ReturnType">What type the action returns</param>
    /// <param name="Container">The type that the method lives within. This is necessary for other parts to verify and double check if the method can be called and how</param>
    /// <param name="Parameters">The parameters of the action. The order of this array must reflect the order of both the yarn and c# side</param>
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

    /// <summary>
    /// What is the return type of the action.
    /// </summary>
    /// <remarks>
    /// This is Yarn Normalised for lack of a better word, so a method that returns an int, and another that returns a float are both something that returns a <see cref="ReturnType.Number"/>
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

    /// <summary>
    /// An action is async if it returns an awaitable type.
    /// In Unity coroutines and ienumerator are not async.
    /// </summary>
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

    /// <summary>
    /// The parameters of the action.
    /// The order of this array must reflect the expected order of both the yarn and c# sides of the action.
    /// </summary>
    public Parameter[] Parameters;

    /// <summary>
    /// The type that the method lives within.
    /// This is necessary for other parts to verify and double check if the method can be called and how.
    /// </summary>
    public NamedType containingNamedType;

    /// <summary>
    /// Convenience wrapper around <see cref="containingNamedType"/>
    /// </summary>
    public string containingTypeFullName
    {
        get
        {
            return containingNamedType.fullyQualifiedType;
        }
    }
    /// <summary>
    /// Convenience wrapper around <see cref="containingNamedType"/>
    /// </summary>
    public string containingTypeShortName
    {
        get
        {
            return containingNamedType.shortType;
        }
    }
    
    /// <summary>
    /// Calculates and returns the number of parameters that we should expect to see in the Yarn for this action.
    /// </summary>
    /// <remarks>
    /// This property understands and handles:
    /// <list type="bullet">
        /// <item>
        /// commands are always one parameter more than functions due to the name of the command being a part of the invocation.
        /// </item>
        /// 
        /// <item>
        /// instance methods have an additional parameter which is the lookup of the target of the action.
        /// </item>
        /// 
        /// <item>
        /// optional parameters and array parameters can change the minimum and maximum number of parameters to a minimum of 0 and maximum of 8196.
        /// </item>
        /// 
        /// <item>
        /// cancellation tokens aren't a parameter as far as yarn is concerned.
        /// </item>
    /// </list>
    /// </remarks>
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
    
    /// <summary>
    /// Returns true if any parameter in the action has a default value
    /// </summary>
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
    
    /// <summary>
    /// Returns true if any parameter in the action is an array
    /// </summary>
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

    /// <summary>
    /// Given a named type and list of converters determines what type of parameter the named type is to be according to the needs of Unity.
    /// </summary>
    /// <remarks>
    /// Each engine has their own specific quirks which means a parameter type that is perfectly fine for one engine may not be the case in another.
    /// This method given a named type converts this into the Unity specific parameter form, or invalid if that isn't possible for this named type.
    /// This is necessary because being able to detect something that is flagged as an action in code isn't the same thing as being able to bind it to an action in Yarn and then also be able to go the other way around.
    /// </remarks>
    /// <param name="namedType">The type to be converted and validated</param>
    /// <param name="converters">The list of converters available to help determine the type</param>
    /// <param name="logger">A logger, will almost certainly be null</param>
    /// <returns>The type of the parameter of the named type depending on the whims and needs of Unity.</returns>
    public static ParameterTypes DetermineParameterTypeFromNamedTypeForUnity(NamedType namedType, IReadOnlyList<YarnConverter> converters, ILogger? logger = null)
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

    
    /// <summary>
    /// Given a fully qualified type name finds the appropriate calling string of the converter for this type.
    /// Essentially can be thought of as knows that for type Blah you need to call Blah.MakeBlah(input, output) and returns that method as a string so it can be used in the source generator.
    /// </summary>
    /// <param name="paramType">The fully qualified type name, it MUST be the fully qualified name!</param>
    /// <param name="converters">The list of converters, one of which hopefully know about the type</param>
    /// <param name="logger">The logger, will likely be null</param>
    /// <returns>The string representation of a method call for the converter of the requested type, or null if it couldn't find one.</returns>
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

    /// <summary>
    /// Returns a string of the appropriate usage of the action, understanding the parameters and the differences between calling a command or function in Yarn Spinner.
    /// </summary>
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

    /// <summary>
    /// Attempts to validate if an action is valid.
    /// This means all it's parameters and returns are of a type that is expected and possible to be called.
    /// </summary>
    /// <remarks>
    /// This realistically should probably do more checking as all it currently does is validate that we have a finalised type for each parameter and the containing type is one that is possible to be constructed for instance methods.
    /// This is mostly fine because other steps have done their own checking but it's probably worth having a single all encompassing validator.
    /// A problem for later.
    /// </remarks>
    /// <param name="action">The action seeking validation</param>
    /// <param name="converters">List of converters needed to validate any unknown parameters</param>
    /// <param name="logger">The logger, most likely will be null</param>
    /// <param name="isUnknownParameterInvalid">
    /// If this is true an unknown parameter that doesn't eventually find a converter in the <see cref="converters"/> list will cause validation to fail.
    /// If set to false the opposite is true, an unresolved param doesn't prevent validation.
    /// This is needed because of runtime actions in Unity, we won't be able to guarantee they have a converter (because it might live in a different assembly) for them at compile time so have to trust that there will be one a runtime.
    /// </param>
    /// <returns>True if the action passed validation</returns>
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

        var actionType = Action.DetermineParameterTypeFromNamedTypeForUnity(action.containingNamedType, converters, logger);
        if (actionType == ParameterTypes.UnityComponent || actionType == ParameterTypes.Converter)
        {
            logger?.WriteLine($"Found a converter for it");
            return true;
        }

        logger?.WriteLine($"after analysing {action.Name} with converters it was still a {actionType}");
        return false;
    }
}

/// <summary>
/// Specific subtype of action that represents an action that is invalid.
/// Why it is invalid could be to any number of reasons, but this action cannot be invoked.
/// </summary>
public record InvalidAction(string Name, ActionType Type): Action(Name, "invalid", "invalid", Type, false, ReturnType.Void, new NamedType(), []) { }

/// <summary>
/// Represents a more easily digestable form of the C# docstring.
/// Intended to be used to populate the descriptions in ysls files
/// </summary>
/// <param name="summary">The value of the summary field in the docstring</param>
/// <param name="retvrn">The value of the return field in the docstring</param>
/// <param name="parameters">The value of the param field in the docstring, for each parameter</param>
public record class DocumentationPayload(string? summary, string? retvrn, Dictionary<string, string>? parameters)
{
    /// <summary>
    /// Convenience constructor for an empty or failed extraction docstring
    /// </summary>
    public DocumentationPayload(): this(null, null, null) { }
}

/// <summary>
/// Represents a simpler and more sourcegen cache friendly version of roslyns <see cref="INamedTypeSymbol"/>.
/// This could be a lot more advanced but in practice we've seen this works well enough to give us the necessary functionality of the roslyn equivalent.
/// </summary>
public record NamedType
{
    // these three represent different ways of looking at the type name
    // all kinda are the same thing but with different amounts of detail

    /// <summary>
    /// the fully qualified name (so including the little global::) of the type.
    /// So for a line cancellation token you'd expect to see <c>global::Yarn.Unity.LineCancellationToken</c> here
    /// </summary>
    public readonly string fullyQualifiedType;
    
    /// <summary>
    /// The short form name of the type
    /// So for a line cancellation token you'd expect to see <c>LineCancellationToken</c> here
    /// </summary>
    public readonly string shortType;
    
    /// <summary>
    /// The long form name of the type, includes the namespace basically.
    /// So for a line cancellation token you'd expect to see <c>Yarn.Unity.LineCancellationToken</c> here
    /// </summary>
    public readonly string longType;
    
    /// <summary>
    /// The special type of this type, this is just a shorter and often more convenient way to filter a lot of types.
    /// For basic types it will just be that type, for everything else (including tokens and enums and components) it will be other
    /// </summary>
    public readonly YarnSpecialType specialType;

    /// <summary>
    /// From inside this layer we can't know if a type is actually a unity component type or not.
    /// There are some tricks we could do to make an educated guess but its much easier to just be told by another layer with more insight
    /// </summary>
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

/// <summary>
/// Represents the types of actions we encounter while scanning code.
/// Most of the time will be Command or Function
/// </summary>
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

/// <summary>
/// A Yarn Normalised form of the allowed return types.
/// In Yarn a function that returns a float or an int are effectively the same thing, they both return a number.
/// This enum represents a simpler cutdown list of those types, additionally handles when
/// </summary>
public enum ReturnType
{
    /// <summary>
    /// Have been unable to determine this type of return
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

/// <summary>
/// Represents the type a parameter in an action can be.
/// Is overly simplified and needs to go hand in hand with a <see cref="NamedType"/> to ge fully useful but there is loads of easier filtering that can be done with this simpler representation of a type.
/// </summary>
public enum ParameterTypes
{
    /// <summary>
    /// represents the situation where we MAY be a converted type but are unsure at this stage
    /// </summary>
    Unknown,
    
    /// <summary>
    /// The type is invalid to be a parameter
    /// </summary>
    Invalid,

    /// <summary>
    /// The type is anything that boils down into a Yarn Number, Bool, or String.
    /// </summary>
    Basic,

    /// <summary>
    /// Is a cancellation token, could be a wombotoken or a regular one
    /// </summary>
    Token,
    
    /// <summary>
    /// Is a Unity GameObject
    /// </summary>
    UnityGameObject,
    
    /// <summary>
    /// Is a Unity component or subclass of one (such as MonoBehaviour)
    /// </summary>
    UnityComponent,
    
    /// <summary>
    /// Is a type that has a custom converter to handle it
    /// </summary>
    Converter,
}

/// <summary>
/// Represents the concept of a Yarn enum including it's backing type
/// </summary>
/// <param name="Name">The name of the enum (no it's cases)</param>
/// <param name="Backing">The backing type of the enum, defaults to Int</param>
public record YarnEnum(string Name, YarnEnum.BackingType Backing)
{
    /// <summary>
    /// Unlike C# enums in Yarn can be numeric or string backed, and they have some slight differences depending on that
    /// </summary>
    public enum BackingType
    {
        Int, String,
    }
}

/// <summary>
/// Represents the way the action was registered.
/// Necessary because there are some differences in diagnostics depending on how an action has been registered.
/// </summary>
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

/// <summary>
/// Represents the concept of a yarn converter method, what type it supports and how to call it.
/// </summary>
/// <remarks>
/// Because all converters have a fixed structure don't need to know the parameter names or types as these can be trivially derived.
/// </remarks>
public record YarnConverter
{
    /// <summary>
    /// The fully qualified name of the type this converter supports
    /// </summary>
    public string FullyQualifiedTypeName;
    
    /// <summary>
    /// The string form of how you'd invoke this converter.
    /// Because converters get called by the source generator knowing what actual characters to dump to disk is all oyu need
    /// </summary>
    public string StaticCallingString;

    public YarnConverter(string FullyQualifiedTypeName, string StaticCallingString)
    {
        this.FullyQualifiedTypeName = FullyQualifiedTypeName;
        this.StaticCallingString = StaticCallingString;
    }
}

/// <summary>
/// The Yarn source generator specific form of the Roslyn <see cref="SpecialType"/>.
/// Has far less fields but we need less so it's fine.
/// </summary>
public enum YarnSpecialType
{
    /// <summary>
    /// Handles everything else a type could be
    /// </summary>
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