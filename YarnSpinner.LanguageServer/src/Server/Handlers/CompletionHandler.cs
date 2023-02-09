using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn.Compiler;

namespace YarnLanguageServer.Handlers
{
    internal class CompletionHandler : ICompletionHandler
    {
        private Workspace workspace;
        private List<CompletionItem> specialCommands;

        public CompletionHandler(Workspace workspace)
        {
            this.workspace = workspace;

            this.specialCommands = new List<CompletionItem>()
            {
                new CompletionItem
                {
                    Label = "if command",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "If statements selects a block of statements to present based on the value of an expression.",
                    InsertText = "if ${1:expression}",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "jump command",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Jump to another node",
                    InsertText = "jump ${1:node}",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "else if command",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Else if statements are used with if statements to present content based on a different condition.",
                    InsertText = "elseif ${1:expression}",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "else command",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Else statements are used with if statements to present an alternate path",
                    InsertText = "else",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "endif command",
                    Kind = CompletionItemKind.Keyword,
                    Documentation = "Endif ends an if, else, or else if statement",
                    InsertText = "endif",
                    InsertTextFormat = InsertTextFormat.PlainText,
                },
                new CompletionItem
                {
                    Label = "declare command",
                    Kind = CompletionItemKind.Keyword,
                    InsertText = "declare ${1:\\$variable} = ${2:value} as ${3:type}",
                    Documentation = "Declares a variable with a name, an initial value, and optionally a type.\nIf you don't provide a type it will instead be inferred.",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "set command",
                    Kind = CompletionItemKind.Keyword,
                    InsertText = "set ${1:\\$variable} to ${2:value}",
                    Documentation = "Set assigns the value of the expression to a variable",
                    InsertTextFormat = InsertTextFormat.Snippet,
                },
                new CompletionItem
                {
                    Label = "stop command",
                    Kind = CompletionItemKind.Keyword,
                    InsertText = "stop",
                    Documentation = "Stop ends the current dialogue.",
                    InsertTextFormat = InsertTextFormat.PlainText,
                }
            };
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            if (workspace.YarnFiles.TryGetValue(request.TextDocument.Uri.ToUri(), out var yarnFile))
            {
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
                        // giving every special command the requisite text edit range
                        foreach (var cmd in specialCommands)
                        {   
                            var copy = cmd with
                            {
                                TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = cmd.InsertText, Range = indexTokenRange.CollapseToEnd() }),
                            };
                            results.Add(copy);
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

                            results.Add(new CompletionItem
                            {
                                Label = cmd.DefinitionName,
                                Kind = CompletionItemKind.Function,
                                Documentation = cmd.Documentation,
                                Detail = cmd.DefinitionFile == null || cmd.IsBuiltIn ? null : System.IO.Path.GetFileName(cmd.DefinitionFile.AbsolutePath),
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