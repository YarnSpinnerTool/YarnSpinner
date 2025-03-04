using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Compiler
{
    // TODO: when-condition for links referencing completion state for nodes
    // (i.e. <<q1:a -- q1:b when q2:c>> means 'q2:c' is completed

    public struct QuestGraphEdge
    {
        public QuestGraphEdgeDescriptor EdgeDescriptor { get; private set; }
        public enum VariableType
        {
            Implicit,
            External,
            None,
        }

        public QuestGraphEdge(QuestGraphEdgeDescriptor descriptor)
        {
            this.EdgeDescriptor = descriptor;
            this.Diagnostics = Array.Empty<Diagnostic>();
            this.VariableContext = null;
            this.Range = Range.InvalidRange;
            this.File = null;
        }

        public readonly QuestGraphNodeDescriptor FromNode => EdgeDescriptor.FromNode;

        public readonly QuestGraphNodeDescriptor ToNode => EdgeDescriptor.ToNode;

        public readonly string? Description => EdgeDescriptor.Description;

        public readonly string? Requirement => EdgeDescriptor.Requirement;

        public YarnSpinnerParser.VariableContext? VariableContext { get; private set; }

        public readonly bool HasErrors => this.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

        public IEnumerable<Diagnostic> Diagnostics { get; private set; }

        public static bool CanParse(string input) => QuestGraphEdgeDescriptor.CanParse(input);

        public static QuestGraphEdge Parse(string input, string? description = null)
        {
            var descriptor = QuestGraphEdgeDescriptor.Parse(input, description);

            List<Diagnostic>? diagnostics = null;
            YarnSpinnerParser.VariableContext? variableContext = null;

            if (descriptor.Requirement != null)
            {
                if (TryParseCondition(descriptor.Requirement, out variableContext, out diagnostics))
                {
                    var variableName = variableContext!.VAR_ID()?.GetText();
                    if (variableName != null && description == null)
                    {
                        description = Utility.SplitCamelCase(variableName.TrimStart('$').Replace('_', ' '));

                        descriptor = new QuestGraphEdgeDescriptor(
                            descriptor.FromNode,
                            descriptor.ToNode,
                            descriptor.Requirement,
                            description
                        );
                    }
                }
            }

            return new QuestGraphEdge(descriptor)
            {
                VariableContext = variableContext,
                Diagnostics = diagnostics ?? Enumerable.Empty<Diagnostic>(),
            };
        }


        public VariableType VariableCreation
        {
            get
            {
                // If an edge explicitly has a condition, its condition is
                // external
                if (!string.IsNullOrEmpty(this.Requirement))
                {
                    return VariableType.External;
                }

                // By default, we don't add a variable condition on links from a
                // step to a task, so its variable type is None
                if (this.FromNode.Type == QuestGraphNodeDescriptor.NodeType.Step
                    && this.ToNode.Type == QuestGraphNodeDescriptor.NodeType.Task
                    && this.Requirement == null)
                {
                    return VariableType.None;
                }

                // Otherwise, it implicitly creates a variable
                return VariableType.Implicit;
            }
        }

        public string? VariableName
        {
            get
            {
                switch (this.VariableCreation)
                {
                    case VariableType.Implicit:
                        if (string.IsNullOrEmpty(this.Description) == false)
                        {
                            return "$" + this.Description!.Replace(" ", "_");
                        }
                        else
                        {
                            return $"${this.FromNode.Quest}{this.FromNode.Name}_{this.ToNode.Quest}{this.ToNode.Name}";
                        }

                    case VariableType.External:
                        return this.VariableContext?.VAR_ID().GetText() ?? throw new System.InvalidOperationException("Variable type is external but variable context is null");

                    case VariableType.None:
                    default:
                        return null;

                }

            }
        }

        public string? File { get; internal set; }

        public Range Range { get; internal set; }

        public static bool TryParseCondition(string source, out YarnSpinnerParser.VariableContext? variable, out List<Diagnostic> diagnostics)
        {
            ICharStream input = CharStreams.fromString(source);

            YarnSpinnerLexer lexer = new YarnSpinnerLexer(input);
            CommonTokenStream tokens = new CommonTokenStream(lexer);

            YarnSpinnerParser parser = new YarnSpinnerParser(tokens);

            // turning off the normal error listener and using ours
            var parserErrorListener = new ParserErrorListener("condition");
            var lexerErrorListener = new LexerErrorListener("condition");

            parser.ErrorHandler = new ErrorStrategy();

            parser.RemoveErrorListeners();
            parser.AddErrorListener(parserErrorListener);

            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexerErrorListener);
            lexer.PushMode(YarnSpinnerLexer.ExpressionMode);

            diagnostics = Enumerable.Concat(lexerErrorListener.Diagnostics, parserErrorListener.Diagnostics).ToList();

            variable = parser.variable();

            if (diagnostics.Any(e => e.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                variable = null;
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return this.EdgeDescriptor.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is QuestGraphEdge other))
            {
                return false;
            }
            return this.EdgeDescriptor.Equals(other.EdgeDescriptor);
        }
    }

    internal class QuestGraphVisitor : YarnSpinnerParserBaseVisitor<int>
    {

        public List<Diagnostic> Diagnostics { get; }
        public List<Declaration> Declarations { get; }
        public FileParseResult File { get; }
        public IEnumerable<QuestGraphEdge> Edges => edges;
        private List<QuestGraphEdge> edges = new List<QuestGraphEdge>();

        internal QuestGraphVisitor(FileParseResult file, int languageVersion, List<Diagnostic> diagnostics, List<Declaration> declarations)
        {
            this.File = file;
            this.Diagnostics = diagnostics;
            this.Declarations = declarations;
        }

        public override int VisitCommand_statement([NotNull] YarnSpinnerParser.Command_statementContext context)
        {
            string text = context.command_formatted_text().GetTextWithWhitespace().Trim();

            if (!QuestGraphEdge.CanParse(text))
            {
                // Not a quest graph link
                return base.VisitCommand_statement(context);
            }

            string? description = Compiler.GetDocumentComments(this.File.Tokens, context);
            QuestGraphEdge edge = QuestGraphEdge.Parse(text, description);
            this.edges.Add(edge);

            this.Diagnostics.AddRange(edge.Diagnostics);
            edge.Range = Utility.GetRange(context);
            edge.File = this.File.Name;

            void DeclareBoolVariableIfNecessary(string variableName, string description)
            {
                if (Declarations.Any(d => d.Name == variableName))
                {
                    return;
                }

                var decl = new DeclarationBuilder()
                    .WithName(variableName)
                    .WithType(Types.Boolean)
                    .WithDefaultValue(false)
                    .WithImplicit(true)
                    .WithSourceFileName(this.File.Name)
                    .WithRange(Utility.GetRange(context))
                    .WithDescription(description)
                    .Declaration;

                Declarations.Add(decl);
            }

            void DeclareNodeVariables(QuestGraphNodeDescriptor node)
            {
                DeclareBoolVariableIfNecessary($"$Quest_{node.Quest}_{node.Name}_Reachable", $"{node.Name} is reachable");
                DeclareBoolVariableIfNecessary($"$Quest_{node.Quest}_{node.Name}_Active", $"{node.Name} is active");

                if (node.Type == QuestGraphNodeDescriptor.NodeType.Task)
                {
                    DeclareBoolVariableIfNecessary($"$Quest_{node.Quest}_{node.Name}_Complete", $"{node.Name} is Complete");
                    DeclareBoolVariableIfNecessary($"$Quest_{node.Quest}_{node.Name}_NoLongerNeeded", $"{node.Name} is NoLongerNeeded");
                }
            }

            DeclareNodeVariables(edge.FromNode);
            DeclareNodeVariables(edge.ToNode);

            var diagnostics = this.Diagnostics;

            var variableName = edge.VariableName;
            if (variableName != null)
            {
                if (edge.VariableCreation == QuestGraphEdge.VariableType.Implicit)
                {
                    // Record that executing this command should set the
                    // connection's variable to true

                    context.commandEffect = new Statements.SetBoolVariableCommandEffect(variableName, true);
                }
                else
                {
                    context.commandEffect = new Statements.NoOpCommandEffect();
                }

                if (!this.Declarations.Any(d => d.Name == variableName))
                {
                    var decl = new DeclarationBuilder()
                        .WithName(variableName)
                        .WithType(Types.Boolean)
                        .WithDefaultValue(false)
                        .WithSourceFileName(this.File.Name)
                        .WithRange(Utility.GetRange(context))
                        .WithImplicit(true)
                        .WithDescription($"{edge.FromNode.Name} complete, {edge.ToNode.Name} now active")
                        .Declaration;

                    Declarations.Add(decl);
                }
            }
            else
            {
                // No variable needed for this connection; mark that it doesn't
                // need to run a command
                context.commandEffect = new Statements.NoOpCommandEffect();
            }

            return base.VisitCommand_statement(context);
        }


    }
}
