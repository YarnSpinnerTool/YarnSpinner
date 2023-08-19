using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Yarn.Compiler;

namespace YarnLanguageServer;

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptOut)]
public record DebugOutput
{
    public DocumentUri? SourceProjectUri { get; set; }

    public List<Variable> Variables { get; set; } = new ();

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), MemberSerialization = MemberSerialization.OptOut)]
    public record Variable {
        public string Name { get; set; } = "(unknown)";
        public string Type { get; set; } = "Error";
        public JToken? ExpressionJSON { get; set; }
        public string? DiagnosticMessage { get; set; }
        public bool IsSmartVariable { get; set; }
    }
}

public record class JSONExpression
{
    public enum ExpressionType
    {
        Error = -1,
        And = 0,
        Or,
        Not,
        Implies,
        Equals,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual,
        Literal,
        Constant,
    }

    public ExpressionType Type { get; set; }
    public IEnumerable<JSONExpression> Children { get; set; } = Array.Empty<JSONExpression>();

    public bool IsError
    {
        get
        {
            return this.Type == ExpressionType.Error || this.Children.Any(c => c.IsError);
        }
    }

    /// <summary>
    /// The underlying value of this expression. This is the backing store for
    /// when the <see cref="Type"/> is <see cref="ExpressionType.Literal"/> or
    /// <see cref="ExpressionType.Constant"/>.
    /// </summary>
    private IConvertible? value;

    public IConvertible? Constant
    {
        get => Type switch
        {
            ExpressionType.Constant => value,
            _ => null
        };
        set
        {
            this.Type = ExpressionType.Constant;
            this.value = value;
        }
    }

    public string? Literal
    {
        get => Type switch
        {
            ExpressionType.Literal => value as string,
            _ => null
        };
        set
        {
            this.Type = ExpressionType.Literal;
            this.value = value;
        }
    }

    public JSONExpression? Parent { get; set; }

    public JToken JSONValue {
        get {
            if (IsError) {
                return new JValue("error");
            }
            return Type switch
            {
                ExpressionType.Error => new JValue("error"),
                ExpressionType.And => new JObject
                    {
                        { "and", new JArray(Children.Select(c => c.JSONValue)) },
                    },
                ExpressionType.Or => new JObject
                    {
                        { "or", new JArray(Children.Select(c => c.JSONValue)) },
                    },
                ExpressionType.Not => new JObject
                    {
                        { "not", Children.Single().JSONValue },
                    },
                ExpressionType.Implies => new JObject
                    {
                        { "implies", new JArray(Children.Select(c => c.JSONValue)) },
                    },
                ExpressionType.Equals => new JObject
                    {
                        { "equals", new JArray(Children.Select(c => c.JSONValue)) },
                    },
                ExpressionType.Literal => (JToken)this.Literal,
                ExpressionType.Constant => this.Constant switch
                    {
                        int intValue => intValue,
                        bool boolValue => boolValue,
                        string stringValue => stringValue,
                        float floatValue => floatValue,
                        _ => "error"
                    },
                ExpressionType.LessThan => new JObject
                    {
                        { "lt", new JArray(Children.Select(c => c.JSONValue))},
                    },
                ExpressionType.LessThanOrEqual => new JObject
                    {
                        { "lte", new JArray(Children.Select(c => c.JSONValue))},
                    },
                ExpressionType.GreaterThan => new JObject
                    {
                        { "gt", new JArray(Children.Select(c => c.JSONValue))},
                    },
                ExpressionType.GreaterThanOrEqual => new JObject
                    {
                        { "gte", new JArray(Children.Select(c => c.JSONValue))},
                    },
                _ => (JToken)"error",
            };
        }
    }
}

internal class ExpressionToJSONVisitor : YarnSpinnerParserBaseVisitor<JSONExpression> {

    protected override JSONExpression DefaultResult => new ()
    {
        Type = JSONExpression.ExpressionType.Error,
    };

    public override JSONExpression VisitExpAndOrXor([NotNull] YarnSpinnerParser.ExpAndOrXorContext context)
    {

        return new JSONExpression
        {
            Type = context.op.Type switch
            {
                YarnSpinnerLexer.OPERATOR_LOGICAL_AND => JSONExpression.ExpressionType.And,
                YarnSpinnerLexer.OPERATOR_LOGICAL_OR => JSONExpression.ExpressionType.Or,
                _ => JSONExpression.ExpressionType.Error,
            },
            Children = context.expression().Select(Visit),
        };
    }

    public override JSONExpression VisitExpNot([NotNull] YarnSpinnerParser.ExpNotContext context)
    {
        return new JSONExpression
        {
            Type = JSONExpression.ExpressionType.Not,
            Children = new[] { Visit(context.expression()) },
        };
    }

    public override JSONExpression VisitExpParens([NotNull] YarnSpinnerParser.ExpParensContext context)
    {
        return Visit(context.expression());
    }

    public override JSONExpression VisitExpValue([NotNull] YarnSpinnerParser.ExpValueContext context)
    {
        return Visit(context.value());
    }

    public override JSONExpression VisitValueVar([NotNull] YarnSpinnerParser.ValueVarContext context)
    {
        return new JSONExpression
        {
            Type = JSONExpression.ExpressionType.Literal,
            Literal = context.variable().GetText(),
        };
    }

    public override JSONExpression VisitValueTrue([NotNull] YarnSpinnerParser.ValueTrueContext context)
    {
        return new JSONExpression
        {
            Type = JSONExpression.ExpressionType.Constant,
            Constant = true,
        };
    }

    public override JSONExpression VisitValueFalse([NotNull] YarnSpinnerParser.ValueFalseContext context)
    {
        return new JSONExpression
        {
            Type = JSONExpression.ExpressionType.Constant,
            Constant = false,
        };
    }

    public override JSONExpression VisitValueNumber([NotNull] YarnSpinnerParser.ValueNumberContext context)
    {
        return new JSONExpression
        {
            Type = JSONExpression.ExpressionType.Constant,
            Constant = float.Parse(context.GetText(), System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    public override JSONExpression VisitExpEquality([NotNull] YarnSpinnerParser.ExpEqualityContext context)
    {
        JSONExpression equalityExpression = new JSONExpression
        {
            Type = JSONExpression.ExpressionType.Equals,
            Children = context.expression().Select(Visit),
        };
        if (context.op.Type == YarnSpinnerLexer.OPERATOR_LOGICAL_EQUALS) {
            return equalityExpression;
        } else if (context.op.Type == YarnSpinnerLexer.OPERATOR_LOGICAL_NOT_EQUALS) {
            // If it's a not-equals, wrap the entire thing in a 'not'
            return new JSONExpression
            {
                Type = JSONExpression.ExpressionType.Not,
                Children = new[] { equalityExpression },
            };
        } else {
            throw new InvalidOperationException($"Unexpected operator in equality expression {context.op.Text}");
        }
    }

    public override JSONExpression VisitExpComparison([NotNull] YarnSpinnerParser.ExpComparisonContext context)
    {
        return new JSONExpression
        {
            Type = context.op.Type switch
            {
                YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER => JSONExpression.ExpressionType.GreaterThan,
                YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER_THAN_EQUALS => JSONExpression.ExpressionType.GreaterThanOrEqual,
                YarnSpinnerLexer.OPERATOR_LOGICAL_LESS => JSONExpression.ExpressionType.LessThan,
                YarnSpinnerLexer.OPERATOR_LOGICAL_LESS_THAN_EQUALS => JSONExpression.ExpressionType.LessThanOrEqual,
                _ => JSONExpression.ExpressionType.Error,
            },
            Children = context.expression().Select(Visit).ToList(),
        };
    }
}
