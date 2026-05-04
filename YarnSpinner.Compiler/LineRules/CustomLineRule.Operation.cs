using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

#nullable enable

namespace Yarn.Compiler
{
    partial class CustomLineRule
    {
        enum OperationType
        {
            And, Or, Not, Xor,
            GT, GTE, LT, LTE,
            EQ, NEQ,
            Constant,
            FuncLength,
            FuncLineHasHashtag,
            FuncGetLineCharacter,
            FuncLineHasAnyCharacter,
            FuncRegex,
        }

        [DebuggerDisplay("{GetDebuggrDisplay(),nq}")]
        readonly struct Operation
        {
            public const string FunctionMatches = "matches";
            public const string FunctionLength = "length";
            public const string FunctionHasCharacter = "has_character";
            public const string FunctionCharacter = "character";
            public const string FunctionHasHashtag = "has_hashtag";

            readonly OperationType operationType;
            readonly Operation[] children;
            readonly Value constant;
            readonly System.Text.RegularExpressions.Regex? regex;

            public Operation(OperationType t, params Operation[] children)
            {
                operationType = t;
                this.children = children;
                this.constant = default;
                this.regex = default;

                if (t == OperationType.FuncRegex && children.Length > 0 && children[0].operationType == OperationType.Constant)
                {
                    // This is a regex-checking operation and our first child is
                    // a constant, so create and cache a regex from that
                    // constant. (If the child is not constant, we'll need to
                    // dynamically produce the regex every time we're
                    // evaluated.)
                    var value = children[0].Evaluate(null!);
                    if (value.Type != ValueType.String)
                    {
                        throw new InvalidOperationException($"{this}: {FunctionMatches} expects the first parameter to be a string");
                    }
                    regex = new System.Text.RegularExpressions.Regex(children[0].Evaluate(null!));
                }
            }

            public Operation(Value constant)
            {
                this.constant = constant;
                this.operationType = OperationType.Constant;
                this.children = Array.Empty<Operation>();
                this.regex = default;
            }

            public readonly Value Evaluate(LineStatementSyntaxNode context)
            {
                return this.operationType switch
                {
                    OperationType.Constant => this.constant,
                    OperationType.And => children[0].Evaluate(context) && children[1].Evaluate(context),
                    OperationType.Or => children[0].Evaluate(context) || children[1].Evaluate(context),
                    OperationType.Not => !children[0].Evaluate(context),
                    OperationType.Xor => children[0].Evaluate(context) ^ children[1].Evaluate(context),
                    OperationType.GT => children[0].Evaluate(context) > children[1].Evaluate(context),
                    OperationType.GTE => children[0].Evaluate(context) >= children[1].Evaluate(context),
                    OperationType.LT => children[0].Evaluate(context) < children[1].Evaluate(context),
                    OperationType.LTE => children[0].Evaluate(context) <= children[1].Evaluate(context),
                    OperationType.EQ => children[0].Evaluate(context) == children[1].Evaluate(context),
                    OperationType.NEQ => children[0].Evaluate(context) != children[1].Evaluate(context),
                    OperationType.FuncRegex => MatchesRegex(context),
                    OperationType.FuncLineHasHashtag => HasHashtag(context),
                    OperationType.FuncGetLineCharacter => GetCharacter(context),
                    OperationType.FuncLineHasAnyCharacter => GetCharacter(context).Length > 0,
                    OperationType.FuncLength => GetLength(context),

                    _ => throw new System.InvalidOperationException($"{this}: Unknown operation type " + operationType),
                };
            }

            private bool TryGetChild(LineStatementSyntaxNode context, int index, [NotNullWhen(true)] out string? output)
            {
                if ((this.children.Length - 1) < index)
                {
                    output = default;
                    return false;
                }
                var result = this.children[index].Evaluate(context);
                if (result.Type != ValueType.String)
                {
                    output = default;
                    return false;
                }
                output = result;
                return true;
            }

            readonly bool MatchesRegex(LineStatementSyntaxNode context)
            {
                System.Text.RegularExpressions.Regex? regex = this.regex;
                if (regex == null)
                {
                    if (TryGetChild(context, 0, out var pattern))
                    {
                        regex = new System.Text.RegularExpressions.Regex(pattern);
                    }
                    else
                    {
                        throw new System.InvalidOperationException($"{this}: Failed to get a regex");
                    }
                }

                if (!TryGetChild(context, 1, out var text))
                {
                    text = context.LineText ?? string.Empty;
                }

                return regex.IsMatch(text);
            }

            bool HasHashtag(LineStatementSyntaxNode context)
            {
                if (TryGetChild(context, 0, out var hashtag) == false)
                {
                    throw new InvalidOperationException($"{this}: Expected first parameter of {FunctionHasHashtag} to be a string");
                }

                return context.Hashtags.Contains(hashtag);
            }

            static string GetCharacter(LineStatementSyntaxNode context)
            {
                return Yarn.Markup.LineParser.GetCharacter(context.LineText) ?? string.Empty;
            }

            int GetLength(LineStatementSyntaxNode context)
            {
                if (this.TryGetChild(context, 0, out var text) == false)
                {
                    // If no parameter is provided, use the line text
                    text = context.LineText;
                }
                return text.Length;
            }

            private string GetDebuggerDisplay()
            {
                return ToString();
            }

            public override string ToString()
            {
                if (this.operationType == OperationType.Constant)
                {
                    return this.constant!.ToString();
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("(" + this.operationType.ToString());
                    foreach (var c in children)
                    {
                        sb.Append(" " + c.ToString());
                    }
                    sb.Append(")");
                    return sb.ToString();
                }
            }
        }

    }
}
