namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime.Misc;

    internal class JumpGraphListener : YarnSpinnerParserBaseListener
    {
        private List<string> jumps;
        private string currentNode;

        private List<(string, List<string>)> connections;

        public JumpGraphListener(List<(string, List<string>)> connections)
        {
            this.connections = connections;
        }

        public override void EnterNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            jumps = new List<string>();
            currentNode = null;
        }
        public override void ExitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            if (jumps.Count() > 0 && !string.IsNullOrEmpty(currentNode))
            {
                connections.Add((currentNode, jumps));
            }
        }

        public override void ExitHeader([NotNull] YarnSpinnerParser.HeaderContext context) 
        {
            if (context.header_key.Text.Equals("title"))
            {
                currentNode = context.header_value.Text;
            }
            // later make it also extract x and y where possible
            // else if (context.header_key.Text.Equals("position"))
            // {
            //     var positionalString = context.header_value.Text;
            //     var split = positionalString.Split(',');
            //     var xString = split[0].Trim();
            //     var yString = split[1].Trim();
                
            //     int x;
            //     int y;
            //     if (int.TryParse(xString, out x) && int.TryParse(yString, out y))
            //     {
            //         var position = (x, y);
            //     }
            // }
        }

        public override void EnterJumpToNodeName([NotNull] YarnSpinnerParser.JumpToNodeNameContext context)
        {
            var destination = context.destination.Text;
            jumps.Add(destination);
        }
        // if we hit an expression jump we just bundle it all up as if it were a single string
        // inelegant but simple enough
        public override void EnterJumpToExpression([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            var destination = context.expression().GetText();
            jumps.Add(destination);
        }
    }
}
