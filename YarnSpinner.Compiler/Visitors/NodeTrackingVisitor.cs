// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime.Misc;
    using System.Collections.Generic;

    class NodeTrackingVisitor : YarnSpinnerParserBaseVisitor<string?>
    {
        HashSet<string> TrackingNode;
        HashSet<string> NeverVisitNodes;

        public NodeTrackingVisitor(HashSet<string> ExistingTrackedNodes, HashSet<string> ExistingBlockedNodes)
        {
            this.TrackingNode = ExistingTrackedNodes;
            this.NeverVisitNodes = ExistingBlockedNodes;
        }

        public override string? VisitFunction_call([NotNull] YarnSpinnerParser.Function_callContext context)
        {
            var functionName = context.FUNC_ID().GetText();

            if (functionName.Equals("visited") || functionName.Equals("visited_count"))
            {
                // we aren't bothering to test anything about the value itself
                // if it isn't a static string we'll get back null so can ignore it
                // if the func has more than one parameter later on it will cause an error so again can ignore
                var result = Visit(context.expression()[0]);

                if (result != null)
                {
                    TrackingNode.Add(result);
                }
            }

            return null;
        }

        public override string? VisitValueString([NotNull] YarnSpinnerParser.ValueStringContext context)
        {
            return context.STRING()?.GetText().Trim('"');
        }

        public override string? VisitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            string? title = context.NodeTitle;
            string? tracking = null;

            foreach (var header in context.header())
            {
                if (header.header_key.Text.Equals("tracking"))
                {
                    tracking = header.header_value?.Text;
                }
            }

            if (title != null && tracking != null)
            {
                if (tracking.Equals("always"))
                {
                    TrackingNode.Add(title);
                }
                else if (tracking.Equals("never"))
                {
                    NeverVisitNodes.Add(title);
                }
            }

            if (context.body() != null)
            {
                return Visit(context.body());
            }
            return null;
        }
    }
}
