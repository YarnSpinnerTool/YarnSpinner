// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable
namespace Yarn.Shared
{
    /// <summary>
    /// Represents a single parameter inside a Yarn action.
    /// </summary>
    /// <remarks>
    /// Because Actions are used in many places, parameters are likewise used all over the place.
    /// This can be thought of as a less flexible but more yarn specific form of both the ParameterSyntax and ParameterInfo types in C#.
    /// </remarks>
    /// <param name="Name">The name of the parameter, the little label of the parameter itself</param>
    /// <param name="IsArray">
        /// Is the parameter an array? Unlike C# we don't treat array of type as a different type from the container. So this information needs to be provided ahead of time.
        /// <remarks>
        /// This might change in the future to be part of a collection of attributes/modifiers on the parameter
        /// </remarks>
    /// </param>
    /// <param name="IsOut">
        /// Is the parameter an out param? This is needed for diagnostics.
        /// <remarks>
        /// This might change in the future to be part of a collection of attributes/modifiers on the parameter
        /// </remarks>
    /// </param>
    /// <param name="HasDefaultValue">
        /// Does the parameter have a default value provided?
        /// <remarks>
        /// This might change in the future to be part of a collection of attributes/modifiers on the parameter
        /// </remarks>
    /// </param>
    /// <param name="DefaultValueDisplay">
        /// What is the string form of the default value? Because the default values are only needed during source gen we don't actually care what the resolved final type of the default value is, just how to write it in a text form.
        /// <remarks>
        /// This might change in the future to be part of a collection of attributes/modifiers on the parameter
        /// </remarks>
    /// </param>
    /// <param name="IsNodeAttributed">
        /// Has the parameter been attributed as being a node?
        /// <remarks>
        /// This might change in the future to be part of a collection of attributes/modifiers on the parameter
        /// </remarks>
    /// </param>
    /// <param name="AttributedEnumSubtype">
        /// What enum subtype has the parameter been attributed as?
        /// <remarks>
        /// <p>
        /// This field is optional, it's existence also implies that the parameter has been attributed as a node.
        /// </p>
        /// <p>
        /// This might change in the future to be part of a collection of attributes/modifiers on the parameter
        /// </p>
        /// </remarks>
    /// </param>
    public abstract record Parameter(string Name, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype)
    {
        /// <summary>
        /// When it comes time to invoke a function all parameters need to convert their value from the values provided by Yarn into their appropriate C# form.
        /// Because we use source gen to actually invoke the function we need to know what the string representation of the C# to do this would look like.
        /// This is what this method does, given it's own understanding of the parameter and where it is in the action create valid C# to perform the conversion.
        /// </summary>
        /// <param name="parameterIndex">
        /// what index is the current parameter?
        /// So if the method is something like <c>name(a,b)</c> then the <c>a</c> parameter will have an index here of 0.
        /// </param>
        /// <param name="arrayIndex">
        /// what index is the current into the array of provided values from Yarn?
        /// <remarks>
        /// This seems similar to the <see cref="parameterIndex"/> but may not be the same, in the case of instance methods there is an implied <c>this</c> in c# which is explicit in the Yarn meaning the index values here will be shifted along one.
        /// Relatedly some parameter types (such as tokens) have no corresponding values in the array of parameters provided from Yarn.
        /// </remarks>
        /// </param>
        /// <returns>The fully built function parameter string ready to be injected into the generated source.</returns>
        public abstract string CreateFunctionParameterString(int parameterIndex, int arrayIndex);
        
        /// <summary>
        /// When it comes time to invoke a command all parameters need to convert their value from the strings provided by Yarn into their appropriate C# form.
        /// Because we use source gen to actually invoke the commands we need to know what the string representation of the C# to do this would look like.
        /// This is what this method does, given it's own understanding of the parameter and where it is in the action create valid C# to perform the conversion.
        /// </summary>
        /// <param name="parameterIndex">
        /// what index is the current parameter?
        /// So if the method is something like <c>name(a,b)</c> then the <c>a</c> parameter will have an index here of 0.
        /// </param>
        /// <param name="arrayIndex">
        /// what index is the current into the array of provided values from Yarn?
        /// <remarks>
        /// This seems similar to the <see cref="parameterIndex"/> but may not be the same, Commands have as element 0 the name of the command, and in the case of instance methods there is an implied <c>this</c> in c# which is explicit in the Yarn meaning the index values here will be shifted along one.
        /// Relatedly some parameter types (such as tokens) have no corresponding values in the array of parameters provided from Yarn.
        /// </remarks>
        /// </param>
        /// <returns>The fully built command parameter string ready to be injected into the generated source.</returns>
        public abstract string CreateCommandParameterString(int parameterIndex, int arrayIndex);
        
        /// <summary>
        /// What is the short form for display and injection into generated scripts of this type?
        /// So for a boolean parameter this would return <c>"bool"</c>
        /// </summary>
        public abstract string ShortFormType { get; }
    }
    
    /// <summary>
    /// Represents a parameter whos type either is something we can't handle in a Yarn action, or one whos conversion hasn't been resolved yet.
    /// </summary>
    /// <remarks>
    /// Because the different stages of accumulating potential actions can run in different timings we may well have a collection of potential actions before we can resolve all the types.
    /// This parameter type exists to represent them, maybe it will be resolved to something at a later stage, maybe it won't.
    /// In general if you encounter one of these at source gen it means something has failed.
    /// </remarks>
    public record UnknownParameter(string Name, NamedType Type, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentException">Because there is no concept of converting a value from Yarn into an unknown type this method always throws</exception>
        public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
        {
            throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is still of an unresolved type, this should be impossible at this point!");
        }
        
        /// <inheritdoc />
        /// <exception cref="System.ArgumentException">Because there is no concept of converting a value from Yarn into an unknown type this method always throws</exception>
        public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
        {
            throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is still of an unresolved type, this should be impossible at this point!");
        }

        /// <inheritdoc />
        public override string ShortFormType => Type.shortType;
    }
    
    /// <summary>
    /// Represents a parameter that is a cancellation token
    /// </summary>
    public record TokenParameter(string Name, bool IsYarnToken, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
    {
        private string CreateParameterString(int parameterIndex, int arrayIndex, bool isFunction)
        {
            if (IsArray)
            {
                return "throw new System.ArgumentException(\"Asked to create a token array but this is forbidden, encountering this should be impossible at this point.\");";
            }

            // functions can't take a wombo cancellation token, just a normal token
            if (isFunction)
            {
                return $"var p{parameterIndex} = token;";
            }

            if (IsYarnToken)
            {
                return $"var p{parameterIndex} = token;";
            }
            else
            {
                return $"var p{parameterIndex} = token.HurryUpToken;";
            }
        }
        /// <inheritdoc />
        public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
        {
            return CreateParameterString(parameterIndex, arrayIndex, true);
        }
        /// <inheritdoc />
        public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
        {
            if (IsArray)
            {
                return "throw new System.ArgumentException(\"Asked to create a token array but this is forbidden, encountering this should be impossible at this point.\");";
            }
            return CreateParameterString(parameterIndex, arrayIndex, false);
        }

        /// <inheritdoc />
        public override string ShortFormType => IsYarnToken ? "LineCancellationToken" : "CancellationToken";
    }
}
namespace System.Runtime.CompilerServices { class IsExternalInit { } }