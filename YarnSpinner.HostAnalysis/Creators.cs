#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

public static partial class Creators
{   
    public static Action ActionFromMethodSymbol(IMethodSymbol method, string yarnName, ActionType actionType, DeclarationType declarationType, out List<Diagnostic> diagnostics, bool earlyOut, Location? nameLocation = null, Location? invocationLocation = null, ILogger? logger = null)
    {
        // note to self:
        // nameLocation is the location of the name
            // so in case of a [YarnCommand] the nameLocation is null, it has no name
            // in the case of [YarnCommand("somename")] is is the attribute
            // in the case of runner.AddCommand("somename", somemethod) it is the first parameter
        // invocationLocation is the location of the call itself
            // so in the case of [YarnCommand] it is null
            // in the case runner.AddCommand("somename", somemethod) it is the second parameter

        diagnostics = [];
        
        // if we fail to get the parameters we can't make an action
        if (!TryCreateNewParameters(method.Parameters, yarnName, method.Locations.First(), out var parameters, out var paramDiags, earlyOut, logger))
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
                        diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1026FunctionUsesMetaToken, pLoc));
                    }
                }
            }
        }

        logger?.WriteLine("validated and made parameters");

        if (Validators.TryValidateMethodAsAction(method, yarnName, actionType, declarationType, out var actionDiags, nameLocation, invocationLocation, earlyOut, logger) != Validators.ActionValidation.Succeeded)
        {
            logger?.WriteLine($"Failed to validate '{yarnName}' as an action, we have {actionDiags.Count} diagnostics");

            if (earlyOut)
            {
                return new InvalidAction(yarnName, actionType);
            }
        }

        diagnostics.AddRange(paramDiags);
        diagnostics.AddRange(actionDiags);

        if (method.ContainingType != null)
        {
            var action = new Action(
                yarnName,
                method.GetStaticCallingString(),
                method.Name,
                actionType,
                method.IsStatic,
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