#nullable enable

namespace Yarn.QuestGraphs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    static class EnumerableExtensions
    {
        public static IEnumerable<T> NonNull<T>(this IEnumerable<T?> values) where T : class
        {
            return values.Where(v => v != null)!;
        }
        public static IEnumerable<Expression> NonEmpty(this IEnumerable<Expression?> values)
        {
            return values.Where(v => v.IsEmpty() == false)!;
        }
        public static bool IsEmpty(this Expression? expression)
        {
            return expression == null || expression.TreeNodeCase == Expression.TreeNodeOneofCase.Empty || expression.TreeNodeCase == Expression.TreeNodeOneofCase.None;
        }
    }

    public partial class Expression
    {
        public static Expression MakeAnd(IEnumerable<Expression?> children)
        {
            // Filter out nulls
            var expressions = children.NonEmpty();

            // Simplify the expression - if all of our terms are all boolean
            // true, or any of them are boolean false, the expression
            // immediately simplifies
            if (expressions.All(e => e.TreeNodeCase == TreeNodeOneofCase.Boolean && e.Boolean == true))
            {
                return MakeConstant(true);
            }
            if (expressions.Any(e => e.TreeNodeCase == TreeNodeOneofCase.Boolean && e.Boolean == false))
            {
                return MakeConstant(false);
            }

            // Filter out lingering 'true's, which are meaningless
            expressions = expressions.Where(e => !(e.TreeNodeCase == TreeNodeOneofCase.Boolean && e.Boolean == true));

            if (!expressions.Any())
            {
                // No remaining operands, so become 'false'
                return MakeConstant(false);
            }
            if (expressions.Count() == 1)
            {
                // If there's only one expression, we don't need to do an 'and'
                return expressions.Single();
            }



            var result = new Expression
            {
                And = new NAryExpression()
            };
            result.And.Children.AddRange(expressions);
            return result;
        }

        public static Expression MakeOr(IEnumerable<Expression?> children)
        {
            // Filter out nulls
            var expressions = children.NonEmpty();

            // Simplify the expression if any of our terms are a true value -
            // this expression is true no matter what the values of any other
            // terms are
            if (expressions.Any(e => e.TreeNodeCase == TreeNodeOneofCase.Boolean && e.Boolean == true))
            {
                return MakeConstant(true);
            }

            // Filter out 'false's, which are meaningless
            expressions = expressions.Where(e => !(e.TreeNodeCase == TreeNodeOneofCase.Boolean && e.Boolean == false));

            if (!expressions.Any())
            {
                // No remaining operands, so become 'false'
                return MakeConstant(false);
            }
            if (expressions.Count() == 1)
            {
                // If there's only one expression, we don't need to do an 'or',
                // so just return the single operand
                return expressions.Single();
            }

            var result = new Expression
            {
                Or = new NAryExpression()
            };
            result.Or.Children.AddRange(expressions);
            return result;
        }

        public static Expression MakeNot(Expression? child)
        {
            if (child == null)
            {
                return MakeConstant(false);
            }

            if (child.TreeNodeCase == TreeNodeOneofCase.Boolean)
            {
                return MakeConstant(!child.Boolean);
            }

            return new Expression
            {
                Not = new UnaryExpression { Expr = child },
            };
        }

        public static Expression MakeConstant(bool value)
        {
            return new Expression
            {
                Boolean = value,
            };
        }
    }

    public partial class QuestGraph
    {
        /// <summary>
        /// Gets a value indicating that the quest graph contains at least one
        /// cycle.
        /// </summary>
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

            var incomingEdges = GetIncomingEdges(node).ToList();

            // If a node has no incoming edges, then it is always reachable
            if (incomingEdges.Count == 0)
            {
                return Expression.MakeConstant(true);
            }

            // Otherwise, a node is reachable if the right number of its
            // incoming edges are complete
            List<Expression> parentExpressions = incomingEdges.Select(e => this.GetCompleteExpression(e)).ToList();

            switch (node.Requirement.Type)
            {
                case NodeRequirementType.All:
                    return Expression.MakeAnd(parentExpressions);
                case NodeRequirementType.Any:
                    return Expression.MakeOr(parentExpressions);
                default:
                    throw new ArgumentException("Unhandled requirement kind " + node.Requirement.ToString());
            }


        }

        internal Expression GetActiveExpression(Node node)
        {
            if (node.TypeCase == Node.TypeOneofCase.Step)
            {
                // A step is active if it is reachable and none of its child
                // steps in the same quest are reachable, or it has no
                // descendant goals in the same quest

                var stepIsReachable = GetReachableExpression(node);

                var descendantStepsInQuest = GetDescendants(node).Where(s => s.TypeCase == Node.TypeOneofCase.Step && s.Quest == node.Quest);

                var noDescendantStepsReachable = Expression.MakeNot(
                    Expression.MakeOr(descendantStepsInQuest.Select(s => GetReachableExpression(s)))
                );

                return Expression.MakeAnd(new[] { stepIsReachable, noDescendantStepsReachable });
            }
            if (node.TypeCase == Node.TypeOneofCase.Task)
            {
                // A task is active if it is reachable, none of its outgoing
                // edges are complete, it is not 'no longer needed', and none of
                // its children are reachable

                var taskIsReachable = GetReachableExpression(node);

                var outgoingEdges = GetOutgoingEdges(node);
                var noOutgoingEdgeIsComplete = Expression.MakeNot(
                    Expression.MakeOr(outgoingEdges.Select(e => GetCompleteExpression(e)))
                );

                var noChildIsReachable = Expression.MakeNot(
                    Expression.MakeOr(GetDescendants(node).Select(c => GetReachableExpression(c)))
                );

                var notNoLongerNeeded = Expression.MakeNot(GetNoLongerNeededExpression(node));

                return Expression.MakeAnd(new[] {
                taskIsReachable, noOutgoingEdgeIsComplete, noChildIsReachable, notNoLongerNeeded
            });
            }
            else
            {
                throw new ArgumentException("Unhandled node type " + node.TypeCase);
            }

        }

        internal Expression GetNoLongerNeededExpression(Node task)
        {
            if (task.TypeCase != Node.TypeOneofCase.Task)
            {
                throw new ArgumentException("Can't get a NoLongerNeeded expression for non-task node");
            }
            // A task is no longer needed if it is reachable, is not complete,
            // none of its parent steps in the same quest are active, but none
            // of its outgoing edges are complete. That is to say, our parent
            // state got dealt with via a different path.

            var nodeIsReachable = GetReachableExpression(task);

            var nodeIsNotComplete = Expression.MakeNot(GetCompleteExpression(task));

            var ancestorSteps = GetAncestors(task)
                .Where(n => n.TypeCase == Node.TypeOneofCase.Step)
                .Where(step => step.Quest == task.Quest);

            var noAncestorStepIsActive = Expression.MakeNot(
                Expression.MakeOr(ancestorSteps.Select(g => GetActiveExpression(g)))
            );

            var noOutgoingEdgeIsComplete = Expression.MakeNot(
                Expression.MakeOr(GetOutgoingEdges(task).Select(e => GetCompleteExpression(e)))
            );

            return Expression.MakeAnd(new[] {
                nodeIsReachable, nodeIsNotComplete, noAncestorStepIsActive, noOutgoingEdgeIsComplete
            });
        }

        internal Expression GetCompleteExpression(Node node)
        {
            if (node.TypeCase == Node.TypeOneofCase.Step)
            {
                // A step is complete if it is reachable and not active (i.e. it
                // was active in the past, but it is no longer), or if it is
                // reachable and has no outgoing edges to any nodes in the same
                // quest (i.e. once reached, we never leave it)

                var stepIsReachable = GetReachableExpression(node);
                var notActive = Expression.MakeNot(GetActiveExpression(node));
                var hasNoOutgoingEdgesToSameQuest = Expression.MakeConstant(
                    !GetOutgoingEdges(node).Any(e =>
                    {
                        var (start, end) = GetNodes(e);
                        return end.Quest == node.Quest;
                    })
                );

                return Expression.MakeAnd(new[] {
                stepIsReachable,
                Expression.MakeOr(new[] {notActive, hasNoOutgoingEdgesToSameQuest})
            });
            }
            else if (node.TypeCase == Node.TypeOneofCase.Task)
            {
                // A task is complete if it is reachable and any of its outgoing
                // edges are satisfied

                var stepIsReachable = GetReachableExpression(node);

                var anyOutgoingEdgeSatisfied = Expression.MakeOr(GetOutgoingEdges(node).Select(e => GetCompleteExpression(e)));

                return Expression.MakeAnd(new[] { stepIsReachable, anyOutgoingEdgeSatisfied });
            }
            else
            {
                throw new ArgumentException("Unhandled node type " + node.TypeCase);
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
                return Expression.MakeAnd(new[] { startReachable, e.Condition });
            }
        }

        internal string GetExpressionAsYarnString(Expression? expression)
        {
            if (expression == null)
            {
                return "false";
            }

            switch (expression.TreeNodeCase)
            {
                case Expression.TreeNodeOneofCase.And:
                    if (expression.And.Children == null || !expression.And.Children.NonEmpty().Any())
                    {
                        return "false";
                    }
                    return $"({string.Join("&&", expression.And.Children.NonNull().Select(c => this.GetExpressionAsYarnString(c)))})";

                case Expression.TreeNodeOneofCase.Not:
                    if (expression.Not.Expr.IsEmpty())
                    {
                        return "false";
                    }
                    return $"!({this.GetExpressionAsYarnString(expression.Not.Expr)})";

                case Expression.TreeNodeOneofCase.Or:
                    if (expression.Or.Children == null || !expression.Or.Children.NonNull().Any())
                    {
                        return "false";
                    }
                    return $"({string.Join("||", expression.Or.Children.NonNull().Select(c => GetExpressionAsYarnString(c)))})";

                case Expression.TreeNodeOneofCase.Boolean:
                    return $"{(expression.Boolean ? "true" : "false")}";

                case Expression.TreeNodeOneofCase.Equals_:
                    if (expression.Equals_.First.IsEmpty() || expression.Equals_.Second.IsEmpty())
                    {
                        return "false";
                    }
                    return $"({GetExpressionAsYarnString(expression.Equals_.First)}=={GetExpressionAsYarnString(expression.Equals_.Second)})";

                case Expression.TreeNodeOneofCase.Implies:
                    if (expression.Equals_.First.IsEmpty() || expression.Equals_.Second.IsEmpty())
                    {
                        return "false";
                    }
                    return GetExpressionAsYarnString(
                        Expression.MakeOr(new[] {
                            Expression.MakeNot(expression.Equals_.First), expression.Equals_.Second }
                        ));

                case Expression.TreeNodeOneofCase.Node:
                    {
                        var node = Nodes.Single(n => n.Id == expression.Node.Node);

                        return expression.Node.State switch
                        {
                            NodeStateType.Active => GetExpressionAsYarnString(this.GetActiveExpression(node)),
                            NodeStateType.Complete => GetExpressionAsYarnString(this.GetCompleteExpression(node)),
                            NodeStateType.NoLongerNeeded => GetExpressionAsYarnString(this.GetNoLongerNeededExpression(node)),
                            NodeStateType.Reachable => GetExpressionAsYarnString(this.GetReachableExpression(node)),
                            _ => throw new InvalidOperationException("Invalid node state " + expression.Node.State),
                        };
                    }

                case Expression.TreeNodeOneofCase.Variable:
                    return this.Variables.Single(v => v.Id == expression.Variable).YarnName;
                default:
                    throw new InvalidOperationException("Unknown expression type " + expression.TreeNodeCase);
            }
        }

        internal string GetYarnDefinitionScript()
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

            foreach (var variables in this.Variables.Where(v => v.Source == VariableSourceType.CreatedInEditor))
            {
                yarnFileContentsSB.AppendLine($"<<declare {variables.YarnName} = false>>");
            }

            yarnFileContentsSB.AppendLine("===");
            return yarnFileContentsSB.ToString();
        }

        public string GetNodeVariableName(Node node, NodeStateType state)
        {
            if (node == null) { throw new ArgumentNullException(nameof(node)); }

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

            var variableName = $"${this.Id}_{questName}_{node.YarnName}_{state}";
            return variableName;
        }

        static IEnumerable<string> GetQuestNodeSmartVariables(QuestGraph questGraphDocument)
        {
            if (questGraphDocument.ContainsCycle)
            {
                throw new InvalidOperationException("Graph contains cycle");
            }

            var quests = questGraphDocument.Quests.ToDictionary(q => q.Id);

            string GetVariableDeclaration(Node node, Expression expression, Yarn.QuestGraphs.NodeStateType property)
            {
                var variableName = questGraphDocument.GetNodeVariableName(node, property);
                var expressionString = questGraphDocument.GetExpressionAsYarnString(expression);

                if (expression.TreeNodeCase == Expression.TreeNodeOneofCase.Boolean)
                {
                    // Wrap raw boolean values to ensure that it's parsed as an
                    // Expression.MakeAnd becomes a smart variable
                    expressionString = $"({expressionString})";
                }

                return $"<<declare {variableName} = {expressionString}>>";

            }

            foreach (var node in questGraphDocument.Nodes)
            {

                var sb = new System.Text.StringBuilder();

                string questName = node.Quest != null && quests.TryGetValue(node.Quest, out Quest? quest)
                    ? (quest.Name ?? quest.YarnName)
                    : "NoQuest";

                sb.AppendLine($"// [{node.TypeCase} {node.Id}] {questName}: {node.DisplayName}");

                sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetReachableExpression(node), NodeStateType.Reachable));

                if (node.TypeCase == Node.TypeOneofCase.Step)
                {
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetActiveExpression(node), NodeStateType.Active));
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetCompleteExpression(node), NodeStateType.Complete));
                }
                else if (node.TypeCase == Node.TypeOneofCase.Task)
                {
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetActiveExpression(node), NodeStateType.Active));
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetNoLongerNeededExpression(node), NodeStateType.NoLongerNeeded));
                    sb.AppendLine(GetVariableDeclaration(node, questGraphDocument.GetCompleteExpression(node), NodeStateType.Complete));
                }
                else
                {
                    throw new System.InvalidOperationException($"Can't get expressions for node {node} ({questName}: {node.DisplayName ?? node.YarnName})  of type {node.TypeCase}");
                }

                yield return sb.ToString();

            }
        }
    }
}
