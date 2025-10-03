using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Yarn.QuestGraphs
{
    static class EnumerableExtensions
    {
        public static IEnumerable<T> NonNull<T>(this IEnumerable<T?> values) where T : class
        {
            return values.Where(v => v != null)!;
        }
    }

    internal partial class Expression
    {


        public static Expression And(IEnumerable<Expression?> children)
        {
            // Filter out nulls
            var expressions = children.NonNull();

            // Simplify the expression - if all of our terms are all boolean
            // true, or any of them are boolean false, the expression
            // immediately simplifies
            if (expressions.All(e => e.Type == ConditionType.Boolean && e.Value == true))
            {
                return Constant(true);
            }
            if (expressions.Any(e => e.Type == ConditionType.Boolean && e.Value == false))
            {
                return Constant(false);
            }

            // Filter out lingering 'true's, which are meaningless
            expressions = expressions.Where(e => !(e.Type == ConditionType.Boolean && e.Value == true));

            if (!expressions.Any())
            {
                // No remaining operands, so become 'false'
                return Constant(false);
            }
            if (expressions.Count() == 1)
            {
                // If there's only one expression, we don't need to do an 'and'
                return expressions.Single();
            }

            return new Expression
            {
                Type = ConditionType.And,
                Children = new List<Expression?>(expressions),
            };
        }

        public static Expression Or(IEnumerable<Expression?> children)
        {
            // Filter out nulls
            var expressions = children.NonNull();

            // Simplify the expression if any of our terms are a true value -
            // this expression is true no matter what the values of any other
            // terms are
            if (expressions.Any(e => e.Type == ConditionType.Boolean && e.Value == true))
            {
                return Constant(true);
            }

            // Filter out 'false's, which are meaningless
            expressions = expressions.Where(e => !(e.Type == ConditionType.Boolean && e.Value == false));

            if (!expressions.Any())
            {
                // No remaining operands, so become 'false'
                return Constant(false);
            }
            if (expressions.Count() == 1)
            {
                // If there's only one expression, we don't need to do an 'or',
                // so just return the single operand
                return expressions.Single();
            }

            return new Expression
            {
                Type = ConditionType.Or,
                Children = new List<Expression?>(expressions),
            };
        }

        public static Expression Not(Expression? child)
        {
            if (child == null)
            {
                return Constant(false);
            }

            if (child.Type == ConditionType.Boolean && child.Value.HasValue)
            {
                return Constant(!child.Value.Value);
            }

            return new Expression
            {
                Type = ConditionType.Not,
                Children = new List<Expression?>(new[] { child }),
            };
        }

        public static Expression Constant(bool value)
        {
            return new Expression
            {
                Type = ConditionType.Boolean,
                Value = value
            };
        }
    }

    internal partial class QuestGraph
    {
        public bool ContainsCycle
        {
            get
            {
                var seenSet = new HashSet<string>();

                var unseenSet = new HashSet<string>(Nodes.Select(n => n.Id));

                while (unseenSet.Count > 0)
                {
                    var searchStack = new Stack<string>();

                    {
                        var startNode = unseenSet.First();
                        unseenSet.Remove(startNode);

                        searchStack.Push(startNode);
                    }

                    while (searchStack.Count > 0)
                    {
                        var current = searchStack.Pop();
                        if (seenSet.Contains(current))
                        {
                            // We've seen this node before - the graph contains
                            // a cycle!
                            return true;
                        }

                        unseenSet.Remove(current);

                        foreach (var outgoing in GetOutgoingEdges(current))
                        {
                            searchStack.Push(outgoing.End);
                        }
                    }
                }

                return false;
            }
        }

        private (Node Start, Node End) GetNodes(Edge e)
        {
            var start = this.Nodes.Single(n => n.Id == e.Start);
            var end = this.Nodes.Single(n => n.Id == e.End);
            return (start, end);
        }

        private IEnumerable<Edge> GetIncomingEdges(Node n)
        {
            return this.Edges.Where(e => e.End == n.Id);
        }

        private IEnumerable<Edge> GetOutgoingEdges(Node n)
        {
            return this.Edges.Where(e => e.Start == n.Id);
        }

        private IEnumerable<Edge> GetIncomingEdges(string id)
        {
            return this.Edges.Where(e => e.End == id);
        }

        private IEnumerable<Edge> GetOutgoingEdges(string id)
        {
            return this.Edges.Where(e => e.Start == id);
        }

        private IEnumerable<Node> GetDescendants(Node node)
        {
            var searchStack = new Stack<Edge>(this.GetOutgoingEdges(node));
            var result = new HashSet<Node>();

            while (searchStack.Count > 0)
            {
                var edge = searchStack.Pop();
                var targetNode = this.Nodes.Single(n => n.Id == edge.End);
                result.Add(targetNode);

                foreach (var outgoingEdge in GetOutgoingEdges(targetNode))
                {
                    searchStack.Push(outgoingEdge);
                }
            }
            return result;
        }

        private IEnumerable<Node> GetAncestors(Node node)
        {
            var searchStack = new Stack<Edge>(this.GetIncomingEdges(node));
            var result = new HashSet<Node>();

            while (searchStack.Count > 0)
            {
                var edge = searchStack.Pop();
                var targetNode = this.Nodes.Single(n => n.Id == edge.Start);
                result.Add(targetNode);

                foreach (var incomingEdge in GetIncomingEdges(targetNode))
                {
                    searchStack.Push(incomingEdge);
                }
            }
            return result;
        }

        internal Expression GetReachableExpression(Node node)
        {
            // A node is reachable if it has no incoming edges, or if the right
            // number of incoming edges (determined by requirementMode) are
            // complete

            var incomingEdges = GetIncomingEdges(node);

            // If a node has no incoming edges, then it is always reachable
            if (incomingEdges.Count() == 0)
            {
                return new Expression
                {
                    Type = ConditionType.Boolean,
                    Value = true,
                };

            }

            // Otherwise, a node is reachable if the right number of its
            // incoming edges are complete
            List<Expression> parentExpressions = incomingEdges.Select(e => this.GetCompleteExpression(e)).ToList();


            if (node.RequirementMode.Enum.HasValue)
            {
                switch (node.RequirementMode.Enum)
                {
                    case RequirementModeEnum.RequiresAll:
                        return Expression.And(parentExpressions);

                    case RequirementModeEnum.RequiresAny:
                        return Expression.Or(parentExpressions);
                }
            }
            throw new ArgumentException("Unhandled requirement kind " + node.RequirementMode.ToString());

        }

        internal Expression GetActiveExpression(Node node)
        {
            if (node.Type == NodeType.Step)
            {
                // A step is active if it is reachable and none of its child
                // steps in the same quest are reachable, or it has no
                // descendant goals in the same quest

                var stepIsReachable = GetReachableExpression(node);

                var descendantStepsInQuest = GetDescendants(node).Where(s => s.Type == NodeType.Step && s.Quest == node.Quest);

                var noDescendantStepsReachable = Expression.Not(
                    Expression.Or(descendantStepsInQuest.Select(s => GetReachableExpression(s)))
                );

                return Expression.And(new[] { stepIsReachable, noDescendantStepsReachable });
            }
            else if (node.Type == NodeType.Task)
            {
                // A task is active if it is reachable, none of its outgoing
                // edges are complete, it is not 'no longer needed', and none of
                // its children are reachable

                var taskIsReachable = GetReachableExpression(node);

                var outgoingEdges = GetOutgoingEdges(node);
                var noOutgoingEdgeIsComplete = Expression.Not(
                    Expression.Or(outgoingEdges.Select(e => GetCompleteExpression(e)))
                );

                var noChildIsReachable = Expression.Not(
                    Expression.Or(GetDescendants(node).Select(c => GetReachableExpression(c)))
                );

                var notNoLongerNeeded = Expression.Not(GetNoLongerNeededExpression(node));

                return Expression.And(new[] {
                taskIsReachable, noOutgoingEdgeIsComplete, noChildIsReachable, notNoLongerNeeded
            });
            }
            else
            {
                throw new ArgumentException("Unhandled node type " + node.Type);
            }

        }

        internal Expression GetNoLongerNeededExpression(Node task)
        {
            if (task.Type != NodeType.Task)
            {
                throw new ArgumentException("Can't get a NoLongerNeeded expression for non-task node");
            }
            // A task is no longer needed if it is reachable, is not complete,
            // none of its parent steps in the same quest are active, but none
            // of its outgoing edges are complete. That is to say, our parent
            // state got dealt with via a different path.

            var nodeIsReachable = GetReachableExpression(task);

            var nodeIsNotComplete = Expression.Not(GetCompleteExpression(task));

            var ancestorSteps = GetAncestors(task)
                .Where(n => n.Type == NodeType.Step)
                .Where(step => step.Quest == task.Quest);

            var noAncestorStepIsActive = Expression.Not(
                Expression.Or(ancestorSteps.Select(g => GetActiveExpression(g)))
            );

            var noOutgoingEdgeIsComplete = Expression.Not(
                Expression.Or(GetOutgoingEdges(task).Select(e => GetCompleteExpression(e)))
            );

            return Expression.And(new[] {
                nodeIsReachable, nodeIsNotComplete, noAncestorStepIsActive, noOutgoingEdgeIsComplete
            });
        }

        internal Expression GetCompleteExpression(Node node)
        {
            if (node.Type == NodeType.Step)
            {
                // A step is complete if it is reachable and not active (i.e. it
                // was active in the past, but it is no longer), or if it is
                // reachable and has no outgoing edges to any nodes in the same
                // quest (i.e. once reached, we never leave it)

                var stepIsReachable = GetReachableExpression(node);
                var notActive = Expression.Not(GetActiveExpression(node));
                var hasNoOutgoingEdgesToSameQuest = Expression.Constant(
                    !GetOutgoingEdges(node).Any(e =>
                    {
                        var (start, end) = GetNodes(e);
                        return end.Quest == node.Quest;
                    })
                );

                return Expression.And(new[] {
                stepIsReachable,
                Expression.Or(new[] {notActive, hasNoOutgoingEdgesToSameQuest})
            });
            }
            else if (node.Type == NodeType.Task)
            {
                // A task is complete if it is reachable and any of its outgoing
                // edges are satisfied

                var stepIsReachable = GetReachableExpression(node);

                var anyOutgoingEdgeSatisfied = Expression.Or(GetOutgoingEdges(node).Select(e => GetCompleteExpression(e)));

                return Expression.And(new[] { stepIsReachable, anyOutgoingEdgeSatisfied });
            }
            else
            {
                throw new ArgumentException("Unhandled node type " + node.Type);
            }
        }


        private Expression GetCompleteExpression(Edge e)
        {
            var (start, _) = GetNodes(e);
            var startReachable = this.GetReachableExpression(start);

            if (e.Condition == null)
            {
                return startReachable;
            }
            else
            {
                return Expression.And(new[] { startReachable, e.Condition });
            }
        }

        public string GetExpressionAsYarnString(Expression? expression)
        {
            if (expression == null)
            {
                return "false";
            }


            switch (expression.Type)
            {
                case ConditionType.And:
                    if (expression.Children == null || expression.Children?.NonNull().Count() == 0)
                    {
                        return "false";
                    }
                    return $"({string.Join("&&", expression.Children?.NonNull().Select(c => this.GetExpressionAsYarnString(c)))})";

                case ConditionType.Not:
                    if (expression.Children?.NonNull().Count() != 1)
                    {
                        return "false";
                    }
                    return $"!({this.GetExpressionAsYarnString(expression.Children[0])})";

                case ConditionType.Or:
                    if (expression.Children == null || ((List<Expression?>?)expression.Children)?.NonNull().Count() == 0)
                    {
                        return "false";
                    }
                    return $"({string.Join("||", expression.Children?.NonNull().Select(c => GetExpressionAsYarnString(c)))})";

                case ConditionType.Boolean:
                    return $"{((expression.Value ?? false) ? "true" : "false")}";

                case ConditionType.Equals:
                    if (expression.Children?.NonNull().Count() != 2)
                    {
                        return "false";
                    }
                    return $"({GetExpressionAsYarnString(expression.Children[0])}=={GetExpressionAsYarnString(expression.Children[1])})";

                case ConditionType.Implies:
                    if (expression.Children?.NonNull().Count() != 2)
                    {
                        return "false";
                    }
                    return GetExpressionAsYarnString(
                        Expression.Or(new[] {
                            Expression.Not(expression.Children[0]), expression.Children[1] }
                        ));

                case ConditionType.Node:
                    {
                        var node = Nodes.Single(n => n.Id == expression.Node);

                        return expression.State switch
                        {
                            NodeStateLabel.Active => GetExpressionAsYarnString(this.GetActiveExpression(node)),
                            NodeStateLabel.Complete => GetExpressionAsYarnString(this.GetCompleteExpression(node)),
                            NodeStateLabel.NoLongerNeeded => GetExpressionAsYarnString(this.GetNoLongerNeededExpression(node)),
                            NodeStateLabel.Reachable => GetExpressionAsYarnString(this.GetReachableExpression(node)),
                            _ => throw new InvalidOperationException("Invalid node state " + expression.State),
                        };
                    }

                case ConditionType.Variable:
                    return this.Variables.Single(v => v.Id == expression.Variable).YarnName;
                default:
                    throw new InvalidOperationException("Unknwn expression type " + expression.Type);
            }

        }

        public string GetYarnDefinitionScript()
        {
            var yarnFileContentsSB = new System.Text.StringBuilder();
            yarnFileContentsSB.AppendLine("title: " + this.Title.Replace(" ", "") + "_Variables_");
            yarnFileContentsSB.AppendLine("---");

            yarnFileContentsSB.AppendLine("// The contents of this file are automatically generated by Yarn Spinner.");
            yarnFileContentsSB.AppendLine("// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated.");
            yarnFileContentsSB.AppendLine();

            foreach (var questNode in GetQuestNodeSmartVariables(this))
            {
                yarnFileContentsSB.AppendLine(questNode);
            }

            foreach (var variables in this.Variables.Where(v => v.Source == VariableKind.CreatedInEditor))
            {
                yarnFileContentsSB.AppendLine($"<<declare {variables.YarnName} = false>>");
            }

            yarnFileContentsSB.AppendLine("===");
            return yarnFileContentsSB.ToString();
        }

        internal string GetNodeVariableName(Node node, NodeStateLabel state)
        {
            if (Nodes.Contains(node) == false)
            {
                throw new ArgumentException("Node " + node + " is not present in this quest graph");
            }

            string questName;
            if (node.Quest != null)
            {

                var quest = Quests.FirstOrDefault(q => q.Id == node.Quest)
                    ?? throw new InvalidOperationException("Failed to find a quest with id " + node.Quest);

                questName = quest.YarnName;
            }
            else
            {
                questName = "NoQuest";
            }

            var variableName = $"$Quest_{questName}_{node.YarnName}_{state}";
            return variableName;

        }

        static IEnumerable<string> GetQuestNodeSmartVariables(QuestGraph questGraphDocument)
        {
            if (questGraphDocument.ContainsCycle)
            {
                throw new Exception("Graph contains cycle");
            }

            var quests = questGraphDocument.Quests.ToDictionary(q => q.Id);

            string GetVariableDeclaration(Node node, Expression expression, Yarn.QuestGraphs.NodeStateLabel property)
            {
                var variableName = questGraphDocument.GetNodeVariableName(node, property);
                var expressionString = questGraphDocument.GetExpressionAsYarnString(expression);

                if (expression.Type == ConditionType.Boolean)
                {
                    // Wrap raw boolean values to ensure that it's parsed as an
                    // expression and becomes a smart variable
                    expressionString = $"({expressionString})";
                }

                return $"<<declare {variableName} = {expressionString}>>";

            }

            foreach (var node in questGraphDocument.Nodes)
            {

                var sb = new System.Text.StringBuilder();

                string questName = node.Quest != null && quests.ContainsKey(node.Quest)
                    ? (quests[node.Quest].Name ?? quests[node.Quest].YarnName)
                    : "NoQuest";

                sb.AppendLine($"// [{node.Type} {node.Id}] {questName}: {node.DisplayName}");

                sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetReachableExpression(node), NodeStateLabel.Reachable));

                if (node.Type == NodeType.Step)
                {
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetActiveExpression(node), NodeStateLabel.Active));
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetCompleteExpression(node), NodeStateLabel.Complete));
                }
                else if (node.Type == NodeType.Task)
                {
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetActiveExpression(node), NodeStateLabel.Active));
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetNoLongerNeededExpression(node), NodeStateLabel.NoLongerNeeded));
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetCompleteExpression(node), NodeStateLabel.Complete));
                }
                else
                {
                    throw new System.InvalidOperationException($"Can't get expressions for node {node} ({questName}: {node.DisplayName ?? node.YarnName})  of type {node.Type}");
                }

                yield return sb.ToString();

            }
        }
    }
}
