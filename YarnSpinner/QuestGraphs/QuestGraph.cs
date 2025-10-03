#nullable enable

namespace Yarn.QuestGraphs
{
    using System.Collections.Generic;

    public partial class QuestGraph
    {

        public string? Schema { get; set; }

        public List<Edge> Edges { get; set; } = new();

        public string Id { get; set; } = "";

        public List<Node> Nodes { get; set; } = new();

        public List<Note> Notes { get; set; } = new();

        public List<Quest> Quests { get; set; } = new();

        public List<NamedCondition> Rules { get; set; } = new();

        public QuestState State { get; set; } = new();

        public string Title { get; set; } = "";

        public List<Variable> Variables { get; set; } = new();
    }

    public partial class Edge
    {

        public Expression? Condition { get; set; } = new();

        public string End { get; set; } = "";

        public bool HideInPlayMode { get; set; }

        public string Id { get; set; } = "";

        public string? Label { get; set; } = "";

        public string Start { get; set; } = "";
    }

    public partial class Expression
    {

        public ConditionType Type { get; set; }

        public string? Variable { get; set; }

        public bool Value { get; set; } = false;

        public string? Node { get; set; }

        public NodeStateLabel State { get; set; } = NodeStateLabel.Reachable;

        public List<Expression?>? Children { get; set; }
    }

    public partial class Node
    {

        public CompletionStyle? CompletionStyle { get; set; }

        public string? Description { get; set; }

        public string? DisplayName { get; set; }

        public string Id { get; set; } = "";

        public bool IsHidden { get; set; } = false;

        public string? Note { get; set; }

        public Position Position { get; set; } = new Position { X = 0, Y = 0 };

        public string? Quest { get; set; }

        public RequirementModeUnion RequirementMode { get; set; }

        public NodeType Type { get; set; }

        public string YarnName { get; set; } = "";

        public bool CompletesQuest { get; set; } = false;

        public bool StaysCompleteForever { get; set; } = true;
    }

    public partial class Position
    {

        public double X { get; set; }

        public double Y { get; set; }
    }

    public partial class RequirementModeClass
    {

        public double? RequiresAtLeast { get; set; }

        public double? RequiresExactly { get; set; }
    }

    public partial class Note
    {

        public string Contents { get; set; } = "";

        public string Id { get; set; } = "";

        public Position Position { get; set; } = new Position { X = 0, Y = 0 }
;
    }

    public partial class Quest
    {

        public string? Description { get; set; }

        public string Id { get; set; } = "";

        public string? Name { get; set; }

        public string YarnName { get; set; } = "";
    }

    public partial class NamedCondition
    {

        public Expression? Condition { get; set; }

        public string? Description { get; set; }

        public string DisplayName { get; set; } = "";

        public string Id { get; set; } = "";
    }

    public partial class QuestState
    {

        public Dictionary<string, bool> Constraints { get; set; } = new();

        public List<EdgeState> EdgeStatus { get; set; } = new();

        public List<NodeState> NodeStatus { get; set; } = new();

        public Dictionary<string, bool> PreferredState { get; set; } = new();
    }

    public partial class EdgeState
    {

        public string Edge { get; set; } = "";

        public EdgeStateLabel State { get; set; }

        public bool? Value { get; set; }
    }

    public partial class NodeState
    {

        public string Node { get; set; } = "";

        public NodeStateLabel State { get; set; }

        public bool? Value { get; set; }
    }

    public partial class Variable
    {

        public Expression? Definition { get; set; }

        public string? Description { get; set; }

        public string Id { get; set; } = "";

        public bool IsPermanent { get; set; } = true;

        public string? Name { get; set; }

        public VariableKind Source { get; set; }

        public VariableType Type { get; set; }

        public string YarnName { get; set; } = "";
    }

    public enum NodeStateLabel { Active, Complete, NoLongerNeeded, Reachable };

    public enum ConditionType { And, Boolean, Equals, Implies, Node, Not, Or, Variable };

    public enum CompletionStyle { Failure, Success };

    public enum RequirementModeEnum { RequiresAll, RequiresAny };

    public enum NodeType { Step, Task };

    public enum EdgeStateLabel { Active, Complete };

    public enum VariableKind { CreatedInEditor, ImportedFromYarnScript, SystemGenerated };

    public enum VariableType { Bool };

    public partial struct RequirementModeUnion
    {
        public RequirementModeEnum? Enum;
        public RequirementModeClass? RequirementModeClass;

        public static implicit operator RequirementModeUnion(RequirementModeEnum Enum) => new RequirementModeUnion { Enum = Enum };
        public static implicit operator RequirementModeUnion(RequirementModeClass RequirementModeClass) => new RequirementModeUnion { RequirementModeClass = RequirementModeClass };
    }

}
