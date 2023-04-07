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
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
                var startOfLine = request.Position with
                {
                    Character = 0,
                };

                if (yarnFile.IsNullOrWhitespace(startOfLine, request.Position))
                {
                    // There are no tokens on this line. Offer to code-complete
                    // full statements, or character names.
                    var statementCompletions = new List<CompletionItem>();

                    var cursorLineIndex = request.Position.Line;

                    // Build a list of character names, sorted by distance from
                    // the cursor.
                    //
                    // To do this, get all (character name, line index) pairs
                    // from all nodes in the file, calculate the distance from
                    // the line they appear on from the current position, group
                    // them by name, pick the closest one of each name, and then
                    // finally sort the list by distance.
                    var charactersByDistance = yarnFile.NodeInfos
                        .SelectMany(ni => ni.CharacterNames)
                        .Select(c => (Name: c.Name, Distance: System.Math.Abs(cursorLineIndex - c.LineIndex)))
                        .GroupBy(c => c.Name)
                        .Select(group => group.MinBy(c => c.Distance))
                        .OrderBy(c => c.Distance);

                    foreach (var character in charactersByDistance) {
                        var newText = $"{character.Name}: ";
                        statementCompletions.Add(new CompletionItem
                        {
                            Label = character.Name,
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
                            TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = cmd.InsertText, Range = new Range(request.Position, request.Position) }),
                        });
                    }

                    return Task.FromResult(new CompletionList(statementCompletions));
                }

                var index = yarnFile.GetRawToken(request.Position);
                if (!index.HasValue)
                {
                    return Task.FromResult<CompletionList>(null);
                }

                var indexToken = yarnFile.Tokens[index.Value];

                var indexTokenRange = PositionHelper.GetRange(yarnFile.LineStarts, indexToken);
                if (indexToken.Type == YarnSpinnerLexer.COMMAND_END || indexToken.Type == YarnSpinnerLexer.RPAREN || indexToken.Type == YarnSpinnerLexer.EXPRESSION_END)
                {
                    indexTokenRange = indexTokenRange.CollapseToStart(); // don't replace closing braces
                }

                var results = new List<CompletionItem>();

                var vocabulary = yarnFile.Lexer.Vocabulary;
                var tokenName = vocabulary.GetSymbolicName(indexToken.Type);

                switch (indexToken.Type)
                {
                    case YarnSpinnerLexer.COMMAND_JUMP:
                        {
                            foreach (var node in workspace.GetNodeTitles())
                            {
                                results.Add(new CompletionItem
                                {
                                    Label = node.title,
                                    Kind = CompletionItemKind.Method,
                                    Detail = System.IO.Path.GetFileName(node.uri.AbsolutePath),
                                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = node.title, Range = indexTokenRange.CollapseToEnd() }),
                                });
                            }

                            break;
                        }

                    case YarnSpinnerLexer.COMMAND_START:
                        {
                            // Add keyword completions
                            foreach (var keyword in keywordCompletions) {
                                results.Add(keyword with
                                {
                                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = keyword.InsertText, Range = new Range(request.Position, request.Position) }),
                                });
                            }

                            // adding any known commands
                            System.Text.StringBuilder builder = new System.Text.StringBuilder();
                            foreach (var cmd in workspace.GetCommands())
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

                                string detailText = (cmd.DefinitionFile == null || cmd.IsBuiltIn)
                                    ? null
                                    : $"{cmd.DefinitionName} ({System.IO.Path.GetFileName(cmd.DefinitionFile.AbsolutePath)})";

                                results.Add(new CompletionItem
                                {
                                    Label = cmd.YarnName,
                                    Kind = CompletionItemKind.Function,
                                    Documentation = cmd.Documentation,
                                    Detail = detailText,
                                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = builder.ToString(), Range = indexTokenRange.CollapseToEnd() }),
                                    InsertTextFormat = InsertTextFormat.Snippet,
                                });
                                builder.Clear();
                            }

                            break;
                        }

                    // inline expressions, if, and elseif are the same thing
                    case YarnSpinnerLexer.EXPRESSION_START:
                    case YarnSpinnerLexer.COMMAND_IF:
                    case YarnSpinnerLexer.COMMAND_ELSEIF:
                        {
                            System.Text.StringBuilder builder = new System.Text.StringBuilder();
                            foreach (var cmd in workspace.GetFunctions())
                            {
                                builder.Append(cmd.YarnName);
                                builder.Append("(");

                                var parameters = new List<string>();
                                int i = 1;
                                foreach (var param in cmd.Parameters)
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

                                builder.Append(")");

                                results.Add(new CompletionItem
                                {
                                    Label = cmd.DefinitionName,
                                    Kind = CompletionItemKind.Function,
                                    Documentation = cmd.Documentation,

                                    // would be good in the future to also show the return type but we don't know that at this stage, something for the future
                                    Detail = cmd.DefinitionFile == null || cmd.IsBuiltIn ? null : System.IO.Path.GetFileName(cmd.DefinitionFile.AbsolutePath),
                                    TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = builder.ToString(), Range = indexTokenRange.CollapseToEnd() }),
                                    InsertTextFormat = InsertTextFormat.Snippet,
                                });
                                builder.Clear();
                            }

                            foreach (var variable in workspace.GetVariables())
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

                            break;
                        }
                }

                return Task.FromResult(new CompletionList(results));
            }

            return Task.FromResult<CompletionList>(null);
        }

        public static readonly HashSet<int> PreferedRules = new HashSet<int>
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

        public static readonly HashSet<int> IgnoredTokens = new HashSet<int>
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

        public static readonly Dictionary<string, string> UserFriendlyTokenText = new Dictionary<string, string>
        {
            { "COMMAND_IF", "if" }, { "COMMAND_ELSEIF", "elseif" }, { "COMMAND_ELSE", "else" }, { "COMMAND_SET", "set" },
            { "COMMAND_ENDIF", "endif" }, { "COMMAND_CALL", "call" }, { "COMMAND_DECLARE", "declare" }, { "COMMAND_JUMP", "jump " },
            { "KEYWORD_FALSE", "false" }, { "KEYWORD_TRUE", "true" }, { "KEYWORD_NULL", "null" },
        };

        public static readonly Dictionary<string, string> TokenSnippets = new Dictionary<string, string>
        {
            { "COMMAND_SET", "set \\$$1 to ${2:value}" }, { "COMMAND_DECLARE", "declare \\$$1 to ${2:value}" },
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
    }
}
