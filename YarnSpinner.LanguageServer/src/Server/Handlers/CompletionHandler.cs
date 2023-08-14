using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn.Compiler;

using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer.Handlers
{
    internal class CompletionHandler : ICompletionHandler
    {
        private Workspace workspace;
        private List<CompletionItem> statementCompletions;
        private List<CompletionItem> keywordCompletions;

        public CompletionHandler(Workspace workspace)
        {
            this.workspace = workspace;

            this.statementCompletions = new List<CompletionItem>()
            {
                new CompletionItem
                {
                    Label = "if statement",
                    Kind = CompletionItemKind.Snippet,
                    Documentation = "If statements selects a block of statements to present based on the value of an expression.",
                    InsertText = "<<if ${1:expression}>>\n    ${2}\n<<endif>>",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "jump command",
                    Kind = CompletionItemKind.Snippet,
                    Documentation = "Jump to another node",
                    InsertText = "<<jump ${1:node}>>",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "elseif statement",
                    Kind = CompletionItemKind.Snippet,
                    Documentation = "Else if statements are used with if statements to present content based on a different condition.",
                    InsertText = "<<elseif ${1:expression}>>",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "else statement",
                    Kind = CompletionItemKind.Snippet,
                    Documentation = "Else statements are used with if statements to present an alternate path",
                    InsertText = "<<else>>",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "endif statement",
                    Kind = CompletionItemKind.Snippet,
                    Documentation = "Endif ends an if, else, or else if statement",
                    InsertText = "<<endif>>",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "declare statement",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "<<declare ${1:\\$variable} = ${2:value} as ${3:type}>>",
                    Documentation = "Declares a variable with a name, an initial value, and optionally a type.\nIf you don't provide a type it will instead be inferred.",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "set statement",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "<<set ${1:\\$variable} to ${2:value}>>",
                    Documentation = "Set assigns the value of the expression to a variable",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "stop command",
                    Kind = CompletionItemKind.Function,
                    InsertText = "stop",
                    Documentation = "Stop ends the current dialogue.",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
            };

            this.keywordCompletions = new List<CompletionItem> {
                new CompletionItem
                {
                    Label = "if",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "If statements selects a block of statements to present based on the value of an expression.",
                    InsertText = "if",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "jump",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Jump to another node",
                    InsertText = "jump",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "elseif",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Else if statements are used with if statements to present content based on a different condition.",
                    InsertText = "elseif",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "else",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Else statements are used with if statements to present an alternate path",
                    InsertText = "else",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "endif",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Endif ends an if, else, or else if statement",
                    InsertText = "endif",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "declare",
                    Kind = CompletionItemKind.Keyword,
                    InsertText = "declare",
                    Documentation = "Declares a variable with a name, an initial value, and optionally a type.\nIf you don't provide a type it will instead be inferred.",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "set",
                    Kind = CompletionItemKind.Keyword,
                    InsertText = "set",
                    Documentation = "Set assigns the value of the expression to a variable",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
            };
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var documentUri = request.TextDocument.Uri.ToUri();
            Project? project = workspace.GetProjectsForUri(documentUri).FirstOrDefault();
            YarnFileData? yarnFile = project?.GetFileData(documentUri);

            if (project == null || yarnFile == null)
            {
                // We can't find the file or its project. Return an empty list
                // of completions.
                return Task.FromResult(new CompletionList());
            }

            var startOfLine = request.Position with
            {
                Character = 0,
            };

            if (yarnFile.IsNullOrWhitespace(startOfLine, request.Position))
            {
                // There are no tokens on this line. Offer to code-complete full
                // statements, or character names.
                var statementCompletions = new List<CompletionItem>();

                var cursorLineIndex = request.Position.Line;

                // Build a list of character names, sorted by distance from the
                // cursor.
                //
                // To do this, get all (character name, line index) pairs from
                // all nodes in the file, calculate the distance from the line
                // they appear on from the current position, group them by name,
                // pick the closest one of each name, and then finally sort the
                // list by distance.
                var charactersByDistance = yarnFile.NodeInfos
                    .SelectMany(ni => ni.CharacterNames)
                    .Select(c => (c.Name, Distance: System.Math.Abs(cursorLineIndex - c.LineIndex)))
                    .GroupBy(c => c.Name)
                    .Select(group => group.MinBy(c => c.Distance))
                    .OrderBy(c => c.Distance);

                foreach (var (name, distance) in charactersByDistance)
                {
                    var newText = $"{name}: ";
                    statementCompletions.Add(new CompletionItem
                    {
                        Label = name,
                        Kind = CompletionItemKind.Text,
                        InsertText = newText,
                        Documentation = "Add a character name",
                        InsertTextFormat = InsertTextFormat.PlainText,
                        TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit
                        {
                            NewText = newText,
                            Range = new Range(request.Position, request.Position),
                        }),
                    });
                }

                // giving every special command the requisite text edit range
                foreach (var cmd in this.statementCompletions)
                {
                    statementCompletions.Add(cmd with
                    {
                        TextEdit = new TextEditOrInsertReplaceEdit(
                            new TextEdit
                            {
                                NewText = cmd?.InsertText ?? string.Empty,
                                Range = new Range(request.Position, request.Position),
                            }
                        ),
                    });
                }

                return Task.FromResult(new CompletionList(statementCompletions));
            }

            var context = ParseTreeFromPosition(yarnFile.ParseTree, request.Position.Character, request.Position.Line + 1);

            var maybeTokenAtRequestPosition = yarnFile.GetRawToken(request.Position);
            if (!maybeTokenAtRequestPosition.HasValue)
            {
                // We don't know what completions to offer for here.
                return Task.FromResult(new CompletionList());
            }

            var tokenAtRequestPosition = yarnFile.Tokens[maybeTokenAtRequestPosition.Value];

            var rangeOfTokenAtRequestPosition = PositionHelper.GetRange(yarnFile.LineStarts, tokenAtRequestPosition);
            if (tokenAtRequestPosition.Type == YarnSpinnerLexer.COMMAND_END
                || tokenAtRequestPosition.Type == YarnSpinnerLexer.RPAREN
                || tokenAtRequestPosition.Type == YarnSpinnerLexer.EXPRESSION_END)
            {
                rangeOfTokenAtRequestPosition = rangeOfTokenAtRequestPosition.CollapseToStart(); // don't replace closing braces
            }

            var results = new List<CompletionItem>();

            var vocabulary = yarnFile.Lexer.Vocabulary;
            var tokenName = vocabulary.GetSymbolicName(tokenAtRequestPosition.Type);

            void ExpandRangeToEndOfPreviousTokenOfType(int tokenType, int startIndex, ref Range range)
            {
                var startToken = yarnFile.Tokens[startIndex];
                var current = startToken;
                var index = startIndex;
                while (index >= 0 && current.Line == startToken.Line)
                {
                    if (current.Type == tokenType)
                    {
                        var newRange = new Range
                        {
                            End = range.End,
                            Start = PositionHelper.GetPosition(yarnFile.LineStarts, current.StopIndex + 1),
                        };
                        range = newRange;
                        return;
                    }

                    current = yarnFile.Tokens[--index];
                }
            }

            if (context?.IsChildOfContext<YarnSpinnerParser.Jump_statementContext>() ?? false)
            {
                // We're in the middle of a jump statement. Expand the
                // replacement range to the end of the '<<jump ', and offer the
                // list of nodes.
                ExpandRangeToEndOfPreviousTokenOfType(YarnSpinnerLexer.COMMAND_JUMP, maybeTokenAtRequestPosition.Value, ref rangeOfTokenAtRequestPosition);

                GetNodeNameCompletions(
                    project,
                    request,
                    rangeOfTokenAtRequestPosition,
                    results
                );
            }
            else
            {
                switch (tokenAtRequestPosition.Type)
                {
                    case YarnSpinnerLexer.COMMAND_START:
                        {
                            // The token we're at is the start of a command
                            // statement. Collapse our replacement range to the end
                            // of that token.
                            rangeOfTokenAtRequestPosition = rangeOfTokenAtRequestPosition.CollapseToEnd();
                            GetCommandCompletions(request, rangeOfTokenAtRequestPosition, results);

                            break;
                        }

                    case YarnSpinnerLexer.COMMAND_TEXT:
                        {
                            // The token we're at is in the middle of a command
                            // statement. Expand our range to the end of our
                            // starting command token, so that the results we send
                            // back can be filtered.
                            ExpandRangeToEndOfPreviousTokenOfType(YarnSpinnerLexer.COMMAND_START, maybeTokenAtRequestPosition.Value, ref rangeOfTokenAtRequestPosition);
                            GetCommandCompletions(request, rangeOfTokenAtRequestPosition, results);

                            break;
                        }

                    // inline expressions, if, and elseif are the same thing
                    case YarnSpinnerLexer.EXPRESSION_START:
                    case YarnSpinnerLexer.COMMAND_EXPRESSION_START:
                    case YarnSpinnerLexer.COMMAND_IF:
                    case YarnSpinnerLexer.COMMAND_ELSEIF:
                        {
                            GetVariableNameCompletions(project, rangeOfTokenAtRequestPosition, results);

                            break;
                        }
                }
            }
            return Task.FromResult(new CompletionList(results));
        }

        private static void GetNodeNameCompletions(Project project, CompletionParams request, Range indexTokenRange, List<CompletionItem> results)
        {
            foreach (var node in project.Nodes)
            {
                results.Add(new CompletionItem
                {
                    Label = node.Title,
                    Kind = CompletionItemKind.Method,
                    Detail = System.IO.Path.GetFileName(node.File.Uri.AbsolutePath),
                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit {
                        NewText = node.Title,
                        Range = new Range {
                            Start = indexTokenRange.Start,
                            End = request.Position,
                        },
                    }),
                });
            }
        }

        private static void GetVariableNameCompletions(Project project, Range indexTokenRange, List<CompletionItem> results)
        {
            System.Text.StringBuilder builder = new ();
            foreach (var function in project.Functions.DistinctBy(f => f.YarnName))
            {
                builder.Append(function.YarnName);
                builder.Append('(');

                var parameters = new List<string>();
                int i = 1;
                foreach (var param in function.Parameters)
                {
                    if (param.IsParamsArray)
                    {
                        parameters.Add($"${{{i}:{param.Name}...}}");
                    }
                    else
                    {
                        parameters.Add($"${{{i}:{param.Name}}}");
                    }

                    i++;
                }

                builder.Append(string.Join(", ", parameters));

                builder.Append(')');

                results.Add(new CompletionItem
                {
                    Label = function.YarnName,
                    Kind = CompletionItemKind.Function,
                    Documentation = function.Documentation,

                    // would be good in the future to also show the return type but we don't know that at this stage, something for the future
                    Detail = (function.SourceFileUri?.AbsolutePath == null || function.IsBuiltIn) ? null : System.IO.Path.GetFileName(function.SourceFileUri.AbsolutePath),
                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = builder.ToString(), Range = indexTokenRange.CollapseToEnd() }),
                    InsertTextFormat = InsertTextFormat.Snippet,
                });
                builder.Clear();
            }

            foreach (var variable in project.Variables)
            {
                results.Add(new CompletionItem
                {
                    Label = variable.Name,
                    Kind = CompletionItemKind.Variable,
                    Documentation = variable.Description,
                    Detail = variable.Type.Name,
                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = variable.Name, Range = indexTokenRange.CollapseToEnd() }),
                    InsertTextFormat = InsertTextFormat.PlainText,
                });
            }
        }

        private void GetCommandCompletions(CompletionParams request, Range indexTokenRange, List<CompletionItem> results)
        {
            // Add keyword completions
            foreach (var keyword in keywordCompletions)
            {
                results.Add(keyword with
                {
                    TextEdit = new TextEditOrInsertReplaceEdit(
                        new TextEdit
                        {
                            NewText = keyword?.InsertText ?? string.Empty,
                            Range = new Range(indexTokenRange.Start, request.Position),
                        }
                    ),
                });
            }

            var uri = request.TextDocument.Uri;
            var project = workspace.GetProjectsForUri(uri).First();

            // adding any known commands
            System.Text.StringBuilder builder = new ();
            foreach (var cmd in project.Commands.DistinctBy(c => c.YarnName))
            {
                builder.Append(cmd.YarnName);

                int i = 1;
                foreach (var param in cmd.Parameters)
                {
                    if (param.IsParamsArray)
                    {
                        builder.Append($" ${{{i}:{param.Name}...}}");
                    }
                    else
                    {
                        builder.Append($" ${{{i}:{param.Name}}}");
                    }

                    i++;
                }

                string detailText = cmd.IsBuiltIn ? "(built-in)"
                    : (cmd.SourceFileUri == null || cmd.IsBuiltIn)
                        ? $"{cmd.ImplementationName}"
                        : $"{cmd.ImplementationName} ({System.IO.Path.GetFileName(cmd.SourceFileUri.AbsolutePath)})";

                results.Add(new CompletionItem
                {
                    Label = cmd.YarnName,
                    Kind = CompletionItemKind.Function,
                    Documentation = cmd.Documentation,
                    Detail = detailText,
                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit {
                        NewText = builder.ToString(),
                        Range = new Range {
                            Start = indexTokenRange.Start,
                            End = request.Position,
                        },
                    }),
                    InsertTextFormat = InsertTextFormat.Snippet,
                });
                builder.Clear();
            }
        }

        public static readonly HashSet<int> PreferedRules = new ()
        {
            YarnSpinnerParser.RULE_command_statement,
            YarnSpinnerParser.RULE_variable,
            YarnSpinnerParser.RULE_function_call,
            YarnSpinnerParser.RULE_function_call,
            YarnSpinnerParser.RULE_jump_statement,

            // YarnSpinnerLexer.FUNC_ID,
            // YarnSpinnerLexer.COMMAND_NAME,
            // YarnSpinnerLexer.ID,
            // YarnSpinnerLexer.VAR_ID
        };

        public static readonly HashSet<int> IgnoredTokens = new ()
        {
            YarnSpinnerLexer.OPERATOR_ASSIGNMENT,
            YarnSpinnerLexer.OPERATOR_MATHS_ADDITION,
            YarnSpinnerLexer.OPERATOR_MATHS_ADDITION_EQUALS,
            YarnSpinnerLexer.OPERATOR_MATHS_DIVISION,
            YarnSpinnerLexer.OPERATOR_MATHS_DIVISION_EQUALS,
            YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION,
            YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION_EQUALS,
            YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION,
            YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION_EQUALS,
            YarnSpinnerLexer.OPERATOR_MATHS_MODULUS,
            YarnSpinnerLexer.OPERATOR_MATHS_MODULUS_EQUALS,
            YarnSpinnerLexer.OPERATOR_LOGICAL_NOT,
            YarnSpinnerLexer.OPERATOR_LOGICAL_NOT_EQUALS,
            YarnSpinnerLexer.LPAREN,
            YarnSpinnerLexer.RPAREN,
            YarnSpinnerLexer.SHORTCUT_ARROW,
            YarnSpinnerLexer.TEXT,
            YarnSpinnerLexer.EXPRESSION_START,
            YarnSpinnerLexer.HASHTAG,
            YarnSpinnerLexer.COMMAND_TEXT,
            YarnSpinnerLexer.COMMAND_TEXT_END,
            YarnSpinnerLexer.COMMAND_EXPRESSION_START,
            YarnSpinnerLexer.INDENT,
            YarnSpinnerLexer.DEDENT,
            YarnSpinnerLexer.WHITESPACE,
            YarnSpinnerLexer.NUMBER,
            YarnSpinnerLexer.STRING,
            YarnSpinnerLexer.BODY_END,
            YarnSpinnerLexer.COMMAND_START,
            YarnSpinnerLexer.COMMAND_END,
            YarnSpinnerLexer.FUNC_ID, // This and var id ideally taken care of with rules
            YarnSpinnerLexer.VAR_ID,
        };

        public static readonly Dictionary<string, string> UserFriendlyTokenText = new ()
        {
            { "COMMAND_IF", "if" },
            { "COMMAND_ELSEIF", "elseif" },
            { "COMMAND_ELSE", "else" },
            { "COMMAND_SET", "set" },
            { "COMMAND_ENDIF", "endif" },
            { "COMMAND_CALL", "call" },
            { "COMMAND_DECLARE", "declare" },
            { "COMMAND_JUMP", "jump " },
            { "KEYWORD_FALSE", "false" },
            { "KEYWORD_TRUE", "true" },
            { "KEYWORD_NULL", "null" },
        };

        public static readonly Dictionary<string, string> TokenSnippets = new ()
        {
            { "COMMAND_SET", "set \\$$1 to ${2:value}" },
            { "COMMAND_DECLARE", "declare \\$$1 to ${2:value}" },
        };

        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = Utils.YarnDocumentSelector,
                TriggerCharacters = new Container<string>(new List<string> { "$", "<", " ", "{" }),
                AllCommitCharacters = new Container<string>(new List<string> { " " }), // maybe >> or }
            };
        }

        /// <summary>
        /// Checks to see if a parse rule context of type <typeparamref
        /// name="T"/> is an ancestor of <paramref name="tree"/>.
        /// </summary>
        /// <typeparam name="T">A type of <see
        /// cref="Antlr4.Runtime.ParserRuleContext"/>. </typeparam>
        /// <param name="tree">The tree to check.</param>
        /// <returns>true if any parent of <paramref name="tree"/> is of type
        /// <typeparamref name="T"/>.</returns>
        public static bool IsChildOfContext<T>(Antlr4.Runtime.Tree.IParseTree tree)
            where T : Antlr4.Runtime.ParserRuleContext
        {
            var type = typeof(T);
            var current = tree;
            while (current != null)
            {
                if (type.IsAssignableFrom(tree.Payload.GetType()))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        public static Antlr4.Runtime.Tree.IParseTree? ParseTreeFromPosition(Antlr4.Runtime.Tree.IParseTree root, int column, int row)
        {
            if (root is Antlr4.Runtime.Tree.ITerminalNode terminal)
            {
                var token = terminal.Symbol;
                if (token.Line != row)
                {
                    return null;
                }

                var tokenStop = token.Column + (token.StopIndex - token.StartIndex + 1);
                if (token.Column <= column && tokenStop >= column)
                {
                    return terminal;
                }

                return null;
            }
            else if (root is Antlr4.Runtime.ParserRuleContext context)
            {
                if (context.Start == null || context.Stop == null)
                { // Invalid tree?
                    return null;
                }

                if (context.Start.Line > row || (context.Start.Line == row && column < context.Start.Column))
                {
                    return null;
                }

                var tokenStop = context.Stop.Column + (context.Stop.StopIndex - context.Stop.StartIndex + 1);
                if (context.Stop.Line < row || (context.Stop.Line == row && tokenStop < column))
                {
                    return null;
                }

                if (context.ChildCount > 0)
                {
                    foreach (var child in context.children)
                    {
                        var result = ParseTreeFromPosition(child, column, row);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }

                return context;
            }
            else
            {
                return null;
            }
        }
    }

    internal static class ContextExtensions
    {
        public static bool IsChildOfContext<T>(this Antlr4.Runtime.Tree.IParseTree tree)
            where T : Antlr4.Runtime.ParserRuleContext
        {
            return IsChildOfContext<T>(tree, out _);
        }

        public static bool IsChildOfContext<T>(this Antlr4.Runtime.Tree.IParseTree tree, out T? result)
            where T : Antlr4.Runtime.ParserRuleContext
        {
            var type = typeof(T);

            while (tree != null)
            {
                if (type.IsAssignableFrom(tree.Payload.GetType()))
                {
                    result = (T)tree;
                    return true;
                }

                tree = tree.Parent;
            }

            result = default;

            return false;
        }
    }
}
