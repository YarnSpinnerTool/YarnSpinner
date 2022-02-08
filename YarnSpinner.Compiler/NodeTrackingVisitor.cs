namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    /*
    ok so need to be a visitor
    this has two different pieces
    firts piece goes into functions:
        look for any visited or visited_count functions
        if they exist visit their expression
            if it is a constant add that to the list of tracking
            otherwise throw up a warning or ignore it for now?
    second piece will go into the headers:
        look for a title so we can set the node name
        look for tracking key so we can work out if we need to track or not
    */
    class NodeTrackingVisitor : YarnSpinnerParserBaseVisitor<string>
    {
        HashSet<string> TrackingNode;
        HashSet<string> NeverVisitNodes;

        public NodeTrackingVisitor(HashSet<string> ExistingTrackedNodes, HashSet<string> ExistingBlockedNodes)
        {
            this.TrackingNode = ExistingTrackedNodes;
            this.NeverVisitNodes = ExistingBlockedNodes;
        }

        public override string VisitFunction_call([NotNull] YarnSpinnerParser.Function_callContext context)
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
                    Console.WriteLine($"tracking {result}");
                    TrackingNode.Add(result);
                }
            }

            return null;
        }

        public override string VisitValueString([NotNull] YarnSpinnerParser.ValueStringContext context)
        {
            return context.STRING().GetText().Trim('"');
        }

        public override string VisitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            /*
            title = null
            tracking = null
            
            foreach header in headers
                if header is "title"
                    title = header.value
                if header is "tracking"
                    tracking = header.value
            
            if header and tracking
                if tracking is always
                    add to trackingnodes
                else
                    add to ignoreNodes
            */

            return null;
        }
    }
}