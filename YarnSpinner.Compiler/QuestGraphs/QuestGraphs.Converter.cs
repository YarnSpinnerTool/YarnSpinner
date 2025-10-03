using System;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Yarn.QuestGraphs
{
    internal static class Converter
    {
        class NamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (name == "Schema")
                {
                    return "$schema";
                }
                return CamelCase.ConvertName(name);
            }
        }
        public static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = new NamingPolicy(),
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters =
            {
                NodeStateLabelConverter.Singleton,
                ConditionTypeConverter.Singleton,
                CompletionStyleConverter.Singleton,
                RequirementModeUnionConverter.Singleton,
                RequirementModeEnumConverter.Singleton,
                NodeTypeConverter.Singleton,
                EdgeStateLabelConverter.Singleton,
                VariableKindConverter.Singleton,
                VariableTypeConverter.Singleton,

            },
        };
    }

    internal class NodeStateLabelConverter : JsonConverter<NodeStateLabel>
    {
        public override bool CanConvert(Type t) => t == typeof(NodeStateLabel);

        public override NodeStateLabel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "Active":
                    return NodeStateLabel.Active;
                case "Complete":
                    return NodeStateLabel.Complete;
                case "NoLongerNeeded":
                    return NodeStateLabel.NoLongerNeeded;
                case "Reachable":
                    return NodeStateLabel.Reachable;
            }
            throw new Exception("Cannot unmarshal type NodeStateLabel");
        }

        public override void Write(Utf8JsonWriter writer, NodeStateLabel value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case NodeStateLabel.Active:
                    JsonSerializer.Serialize(writer, "Active", options);
                    return;
                case NodeStateLabel.Complete:
                    JsonSerializer.Serialize(writer, "Complete", options);
                    return;
                case NodeStateLabel.NoLongerNeeded:
                    JsonSerializer.Serialize(writer, "NoLongerNeeded", options);
                    return;
                case NodeStateLabel.Reachable:
                    JsonSerializer.Serialize(writer, "Reachable", options);
                    return;
            }
            throw new Exception("Cannot marshal type NodeStateLabel");
        }

        public static readonly NodeStateLabelConverter Singleton = new NodeStateLabelConverter();
    }

    internal class ConditionTypeConverter : JsonConverter<ConditionType>
    {
        public override bool CanConvert(Type t) => t == typeof(ConditionType);

        public override ConditionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "and":
                    return ConditionType.And;
                case "boolean":
                    return ConditionType.Boolean;
                case "equals":
                    return ConditionType.Equals;
                case "implies":
                    return ConditionType.Implies;
                case "node":
                    return ConditionType.Node;
                case "not":
                    return ConditionType.Not;
                case "or":
                    return ConditionType.Or;
                case "variable":
                    return ConditionType.Variable;
            }
            throw new Exception("Cannot unmarshal type ConditionType");
        }

        public override void Write(Utf8JsonWriter writer, ConditionType value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case ConditionType.And:
                    JsonSerializer.Serialize(writer, "and", options);
                    return;
                case ConditionType.Boolean:
                    JsonSerializer.Serialize(writer, "boolean", options);
                    return;
                case ConditionType.Equals:
                    JsonSerializer.Serialize(writer, "equals", options);
                    return;
                case ConditionType.Implies:
                    JsonSerializer.Serialize(writer, "implies", options);
                    return;
                case ConditionType.Node:
                    JsonSerializer.Serialize(writer, "node", options);
                    return;
                case ConditionType.Not:
                    JsonSerializer.Serialize(writer, "not", options);
                    return;
                case ConditionType.Or:
                    JsonSerializer.Serialize(writer, "or", options);
                    return;
                case ConditionType.Variable:
                    JsonSerializer.Serialize(writer, "variable", options);
                    return;
            }
            throw new Exception("Cannot marshal type ConditionType");
        }

        public static readonly ConditionTypeConverter Singleton = new ConditionTypeConverter();
    }

    internal class CompletionStyleConverter : JsonConverter<CompletionStyle>
    {
        public override bool CanConvert(Type t) => t == typeof(CompletionStyle);

        public override CompletionStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "failure":
                    return CompletionStyle.Failure;
                case "success":
                    return CompletionStyle.Success;
            }
            throw new Exception("Cannot unmarshal type CompletionStyle");
        }

        public override void Write(Utf8JsonWriter writer, CompletionStyle value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case CompletionStyle.Failure:
                    JsonSerializer.Serialize(writer, "failure", options);
                    return;
                case CompletionStyle.Success:
                    JsonSerializer.Serialize(writer, "success", options);
                    return;
            }
            throw new Exception("Cannot marshal type CompletionStyle");
        }

        public static readonly CompletionStyleConverter Singleton = new CompletionStyleConverter();
    }

    internal class RequirementModeUnionConverter : JsonConverter<RequirementModeUnion>
    {
        public override bool CanConvert(Type t) => t == typeof(RequirementModeUnion);

        public override RequirementModeUnion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    var stringValue = reader.GetString();
                    switch (stringValue)
                    {
                        case "RequiresAll":
                            return new RequirementModeUnion { Enum = RequirementModeEnum.RequiresAll };
                        case "RequiresAny":
                            return new RequirementModeUnion { Enum = RequirementModeEnum.RequiresAny };
                    }
                    break;
                case JsonTokenType.StartObject:
                    var objectValue = JsonSerializer.Deserialize<RequirementModeClass>(ref reader, options);
                    return new RequirementModeUnion { RequirementModeClass = objectValue };
            }
            throw new Exception("Cannot unmarshal type RequirementModeUnion");
        }

        public override void Write(Utf8JsonWriter writer, RequirementModeUnion value, JsonSerializerOptions options)
        {
            if (value.Enum != null)
            {
                switch (value.Enum)
                {
                    case RequirementModeEnum.RequiresAll:
                        JsonSerializer.Serialize(writer, "RequiresAll", options);
                        return;
                    case RequirementModeEnum.RequiresAny:
                        JsonSerializer.Serialize(writer, "RequiresAny", options);
                        return;
                }
            }
            if (value.RequirementModeClass != null)
            {
                JsonSerializer.Serialize(writer, value.RequirementModeClass, options);
                return;
            }
            throw new Exception("Cannot marshal type RequirementModeUnion");
        }

        public static readonly RequirementModeUnionConverter Singleton = new RequirementModeUnionConverter();
    }

    internal class RequirementModeEnumConverter : JsonConverter<RequirementModeEnum>
    {
        public override bool CanConvert(Type t) => t == typeof(RequirementModeEnum);

        public override RequirementModeEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "RequiresAll":
                    return RequirementModeEnum.RequiresAll;
                case "RequiresAny":
                    return RequirementModeEnum.RequiresAny;
            }
            throw new Exception("Cannot unmarshal type RequirementModeEnum");
        }

        public override void Write(Utf8JsonWriter writer, RequirementModeEnum value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case RequirementModeEnum.RequiresAll:
                    JsonSerializer.Serialize(writer, "RequiresAll", options);
                    return;
                case RequirementModeEnum.RequiresAny:
                    JsonSerializer.Serialize(writer, "RequiresAny", options);
                    return;
            }
            throw new Exception("Cannot marshal type RequirementModeEnum");
        }

        public static readonly RequirementModeEnumConverter Singleton = new RequirementModeEnumConverter();
    }

    internal class NodeTypeConverter : JsonConverter<NodeType>
    {
        public override bool CanConvert(Type t) => t == typeof(NodeType);

        public override NodeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "step":
                    return NodeType.Step;
                case "task":
                    return NodeType.Task;
            }
            throw new Exception("Cannot unmarshal type NodeType");
        }

        public override void Write(Utf8JsonWriter writer, NodeType value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case NodeType.Step:
                    JsonSerializer.Serialize(writer, "step", options);
                    return;
                case NodeType.Task:
                    JsonSerializer.Serialize(writer, "task", options);
                    return;
            }
            throw new Exception("Cannot marshal type NodeType");
        }

        public static readonly NodeTypeConverter Singleton = new NodeTypeConverter();
    }

    internal class EdgeStateLabelConverter : JsonConverter<EdgeStateLabel>
    {
        public override bool CanConvert(Type t) => t == typeof(EdgeStateLabel);

        public override EdgeStateLabel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "Active":
                    return EdgeStateLabel.Active;
                case "Complete":
                    return EdgeStateLabel.Complete;
            }
            throw new Exception("Cannot unmarshal type EdgeStateLabel");
        }

        public override void Write(Utf8JsonWriter writer, EdgeStateLabel value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case EdgeStateLabel.Active:
                    JsonSerializer.Serialize(writer, "Active", options);
                    return;
                case EdgeStateLabel.Complete:
                    JsonSerializer.Serialize(writer, "Complete", options);
                    return;
            }
            throw new Exception("Cannot marshal type EdgeStateLabel");
        }

        public static readonly EdgeStateLabelConverter Singleton = new EdgeStateLabelConverter();
    }

    internal class VariableKindConverter : JsonConverter<VariableKind>
    {
        public override bool CanConvert(Type t) => t == typeof(VariableKind);

        public override VariableKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            switch (value)
            {
                case "CreatedInEditor":
                    return VariableKind.CreatedInEditor;
                case "ImportedFromYarnScript":
                    return VariableKind.ImportedFromYarnScript;
                case "SystemGenerated":
                    return VariableKind.SystemGenerated;
            }
            throw new Exception("Cannot unmarshal type VariableKind");
        }

        public override void Write(Utf8JsonWriter writer, VariableKind value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case VariableKind.CreatedInEditor:
                    JsonSerializer.Serialize(writer, "CreatedInEditor", options);
                    return;
                case VariableKind.ImportedFromYarnScript:
                    JsonSerializer.Serialize(writer, "ImportedFromYarnScript", options);
                    return;
                case VariableKind.SystemGenerated:
                    JsonSerializer.Serialize(writer, "SystemGenerated", options);
                    return;
            }
            throw new Exception("Cannot marshal type VariableKind");
        }

        public static readonly VariableKindConverter Singleton = new VariableKindConverter();
    }

    internal class VariableTypeConverter : JsonConverter<VariableType>
    {
        public override bool CanConvert(Type t) => t == typeof(VariableType);

        public override VariableType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (value == "bool")
            {
                return VariableType.Bool;
            }
            throw new Exception("Cannot unmarshal type VariableType");
        }

        public override void Write(Utf8JsonWriter writer, VariableType value, JsonSerializerOptions options)
        {
            if (value == VariableType.Bool)
            {
                JsonSerializer.Serialize(writer, "bool", options);
                return;
            }
            throw new Exception("Cannot marshal type VariableType");
        }

        public static readonly VariableTypeConverter Singleton = new VariableTypeConverter();
    }
}
