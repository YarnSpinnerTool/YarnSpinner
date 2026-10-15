// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

/// <summary>
/// A collection of useful methods for creating and validating actions, parameters, converters, and other elements necessary for completing source analysis
/// </summary>
public static partial class Creators
{
    /// <summary>
    /// Creates and validates an action from a methodsymbol
    /// </summary>
    /// <param name="method">The method to convert and validate into an action</param>
    /// <param name="yarnName">the name of the action in Yarn</param>
    /// <param name="actionType">Is this a command or function because that alters the validation</param>
    /// <param name="declarationType">Was it direct registerd via a method or registered via the attribute?</param>
    /// <param name="configuration">The configuration file to adjust the analysis</param>
    /// <param name="diagnostics">The list of any diagnostics encountered during creation</param>
    /// <param name="earlyOut">If this is true then upon encountering a warning or higher diagnostic will abort creation</param>
    /// <param name="isDelegateRegistered">Was the action registered via a delegate? This has some specific quirks around how we determine if a method is an instance method or not but often won't be something to think about</param>
    /// <param name="nameLocation">
        /// The location of where the yarn name of the method was set, necessary for diagnostics.
        /// In case of a <c>[YarnCommand]</c> the nameLocation is null, it has no name.
        /// In the case of <c>[YarnCommand("somename")]</c> is is the attribute.
        /// In the case of <c>runner.AddCommand("somename", somemethod)</c> it is the first parameter.
    /// </param>
    /// <param name="invocationLocation">
        /// The location of call itself, necessary for diagnostics.
        /// In the case of <c>[YarnCommand]</c> it is null.
        /// In the case <c>runner.AddCommand("somename", somemethod)</c> it is the second parameter.
    /// </param>
    /// <param name="logger">The logger will only be used during development</param>
    /// <returns>a tuple of the action and it's validation state. If validation has failed the action will be an <see cref="InvalidAction"/> and it's values will be useless.</returns>
    public static (Action action, Validators.ActionValidation validation) ValidActionFromMethodSymbol(IMethodSymbol method, string yarnName, ActionType actionType, DeclarationType declarationType, AnalysisConfiguration? configuration, out List<Diagnostic> diagnostics, bool earlyOut, bool isDelegateRegistered, Location? nameLocation = null, Location? invocationLocation = null, ILogger? logger = null)
    {
        diagnostics = [];

        logger ??= NullLogger.Default;
        
        // if we fail to get the parameters we can't make an action
        if (!TryCreateNewParameters(method.Parameters, yarnName, method.Locations.First(), configuration, out var parameters, out var paramDiags, earlyOut, logger))
        {
            logger.WriteLine($"Failed to create parameters for {yarnName}");

            if (earlyOut)
            {
                return (new InvalidAction(yarnName, actionType), Validators.ActionValidation.FailedValidation);
            }
        }

        // there is one last little check we need to do
        // functions can't take in a linecancellationtoken
        // The VM will run all functions needed for the next piece of content before then making use of them
        // so if we have a line like: "Alice: hell there {player_name()}"
        // then the "player_name" function will be run BEFORE the line is processed
        // so the concept of hurrying up doesn't really make sense, how do you hurry up the function when it will be invoked BEFORE it is used in content?
        // it is however also possible though that the parameter creation failed in some way that means we have NO parameters
        // and that would prevent us getting a location back out so we also need to check if they are in sync
        // because if they aren't then it sorta doesn't matter as there will be other errors to worry about
        if (actionType == ActionType.Function && parameters.Length == method.Parameters.Length)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p is TokenParameter tp)
                {
                    if (tp.IsYarnToken)
                    {
                        if (earlyOut)
                        {
                            return (new InvalidAction(yarnName, actionType), Validators.ActionValidation.FailedValidation);
                        }
                        if (configuration == null)
                        {
                            configuration = new AnalysisConfiguration();
                        }
                        Location? pLoc = method.Parameters[i].Locations.FirstOrDefault();
                        diagnostics.Add(configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1026FunctionUsesMetaToken, pLoc));
                    }
                }
            }
        }

        logger.WriteLine("validated and made parameters");

        // now we validate the action itself
        // this tells us if the action is valid for compile time invocation, runtime invocation, or invalid to invoke for some reason
        // if invalid we can just leave right then and there
        // but if it is valid for runtime invoke then we need to change our behaviour
        // because if we are in the mode for source gen we instead want to just early out of all of this because we can't gen for runtime actions
        // neither can we report diagnostics there
        var validAction = Validators.TryValidateMethodAsAction(method, yarnName, actionType, declarationType, configuration, out var actionDiags, nameLocation, invocationLocation, logger);
        diagnostics.AddRange(paramDiags);
        diagnostics.AddRange(actionDiags);

        if (validAction == Validators.ActionValidation.FailedValidation)
        {
            return (new InvalidAction(yarnName, actionType), Validators.ActionValidation.FailedValidation);
        }

        // an instance method is any method that isn't a lambda, isn't a local function, and isn't static
        var isInstanceMethod = method.MethodKind != MethodKind.AnonymousFunction && method.MethodKind != MethodKind.LocalFunction && !method.IsStatic && !isDelegateRegistered;

        if (method.ContainingType != null)
        {
            var action = new Action(
                yarnName,
                method.GetStaticCallingString(),
                method.Name,
                actionType,
                isInstanceMethod,
                method.ReturnType.UnityReturnType(logger),
                method.ContainingType.YarnNamedType(),
                parameters
            );
            return (action, validAction);
        }
        else
        {
            logger.WriteLine($"Failed to find the containing type for {yarnName}");
        }

        return (new InvalidAction(yarnName, actionType), Validators.ActionValidation.FailedValidation);
    }
    
    /// <summary>
    /// Creates and validates an action from a method symbol.
    /// Similar to <see cref="ValidActionFromMethodSymbol"/> but does not return the validation information.
    /// This may go away to be replaced by the <see cref="ValidActionFromMethodSymbol"/> in the future
    /// </summary>
    /// <param name="method">The method to convert and validate into an action</param>
    /// <param name="yarnName">the name of the action in Yarn</param>
    /// <param name="actionType">Is this a command or function because that alters the validation</param>
    /// <param name="declarationType">Was it direct registerd via a method or registered via the attribute?</param>
    /// <param name="configuration">The configuration file to adjust the analysis</param>
    /// <param name="diagnostics">The list of any diagnostics encountered during creation</param>
    /// <param name="earlyOut">If this is true then upon encountering a warning or higher diagnostic will abort creation</param>
    /// <param name="isDelegateRegistered">Was the action registered via a delegate? This has some specific quirks around how we determine if a method is an instance method or not but often won't be something to think about</param>
    /// <param name="nameLocation">
        /// The location of where the yarn name of the method was set, necessary for diagnostics.
        /// In case of a <c>[YarnCommand]</c> the nameLocation is null, it has no name.
        /// In the case of <c>[YarnCommand("somename")]</c> is is the attribute.
        /// In the case of <c>runner.AddCommand("somename", somemethod)</c> it is the first parameter.
    /// </param>
    /// <param name="invocationLocation">
        /// The location of call itself, necessary for diagnostics.
        /// In the case of <c>[YarnCommand]</c> it is null.
        /// In the case <c>runner.AddCommand("somename", somemethod)</c> it is the second parameter.
    /// </param>
    /// <param name="logger">The logger will only be used during development</param>
    /// <returns>The action if it validated or an InvalidAction if validation failed</returns>
    public static Action ActionFromMethodSymbol(IMethodSymbol method, string yarnName, ActionType actionType, DeclarationType declarationType, out List<Diagnostic> diagnostics, bool earlyOut, bool isDelegateRegistered, AnalysisConfiguration? configuration, Location? nameLocation = null, Location? invocationLocation = null, ILogger? logger = null)
    {        
        if (configuration == null)
        {
            configuration = new AnalysisConfiguration();
        }

        diagnostics = [];
        
        // if we fail to get the parameters we can't make an action
        if (!TryCreateNewParameters(method.Parameters, yarnName, method.Locations.First(), configuration, out var parameters, out var paramDiags, earlyOut, logger))
        {
            logger?.WriteLine($"Failed to create parameters for {yarnName}");

            if (earlyOut)
            {
                return new InvalidAction(yarnName, actionType);
            }
        }

        // there is one last little check we need to do
        // functions can't take in a linecancellationtoken
        // The VM will run all functions needed for the next piece of content before then making use of them
        // so if we have a line like: "Alice: hell there {player_name()}"
        // then the "player_name" function will be run BEFORE the line is processed
        // so the concept of hurrying up doesn't really make sense, how do you hurry up the function when it will be invoked BEFORE it is used in content?
        // it is however also possible though that the parameter creation failed in some way that means we have NO parameters
        // and that would prevent us getting a location back out so we also need to check if they are in sync
        // because if they aren't then it sorta doesn't matter as there will be other errors to worry about
        if (actionType == ActionType.Function && parameters.Length == method.Parameters.Length)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p is TokenParameter tp)
                {
                    if (tp.IsYarnToken)
                    {
                        if (earlyOut)
                        {
                            return new InvalidAction(yarnName, actionType);
                        }
                        Location? pLoc = method.Parameters[i].Locations.FirstOrDefault();
                        var diag = configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1026FunctionUsesMetaToken, pLoc);
                        diagnostics.Add(diag);
                    }
                }
            }
        }

        logger?.WriteLine("validated and made parameters");

        // now we validate the action itself
        // this tells us if the action is valid for compile time invocation, runtime invocation, or invalid to invoke for some reason
        // if invalid we can just leave right then and there
        // but if it is valid for runtime invoke then we need to change our behaviour
        // because if we are in the mode for source gen we instead want to just early out of all of this because we can't gen for runtime actions
        // neither can we report diagnostics there
        var validAction = Validators.TryValidateMethodAsAction(method, yarnName, actionType, declarationType, configuration, out var actionDiags, nameLocation, invocationLocation, logger);
        diagnostics.AddRange(paramDiags);
        diagnostics.AddRange(actionDiags);

        if (validAction == Validators.ActionValidation.FailedValidation)
        {
            return new InvalidAction(yarnName, actionType);
        }

        if (validAction == Validators.ActionValidation.RunTimeValid && earlyOut)
        {
            return new InvalidAction(yarnName, actionType);
        }

        // an instance method is any method that isn't a lambda, isn't a local function, and isn't static
        var isInstanceMethod = method.MethodKind != MethodKind.AnonymousFunction && method.MethodKind != MethodKind.LocalFunction && !method.IsStatic && !isDelegateRegistered;

        if (method.ContainingType != null)
        {
            var action = new Action(
                yarnName,
                method.GetStaticCallingString(),
                method.Name,
                actionType,
                isInstanceMethod,
                method.ReturnType.UnityReturnType(logger),
                method.ContainingType.YarnNamedType(),
                parameters
            );
            return action;
        }
        else
        {
            logger?.WriteLine($"Failed to find the containing type for {yarnName}");
        }

        return new InvalidAction(yarnName, actionType);
    }
}