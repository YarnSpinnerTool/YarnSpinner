namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// A visitor that extracts variable declarations from a parse tree.
    /// /// After visiting an entire parse tree for a file, the <see
    /// cref="NewDeclarations"/> property will contain all explicit
    /// variable declarations that were found.
    /// </summary>
    internal class DeclarationVisitor : YarnSpinnerParserBaseVisitor<Yarn.Type>
    {

        /// <summary>
        /// The CommonTokenStream derived from the file we're parsing. This
        /// is used to find documentation comments for declarations.
        /// </summary>
        private readonly CommonTokenStream tokens;

        // The collection of variable declarations we know about before
        // starting our work
        private IEnumerable<Declaration> ExistingDeclarations;

        // The name of the node that we're currently visiting.
        private string currentNodeName = null;

        /// <summary>
        /// The context of the node we're currently in.
        /// </summary>
        private YarnSpinnerParser.NodeContext currentNodeContext;

        /// <summary>
        /// The name of the file we're currently in.
        /// </summary>
        private string sourceFileName;

        /// <summary>
        /// Gets the collection of new variable declarations that were
        /// found as a result of using this <see
        /// cref="DeclarationVisitor"/> to visit a <see
        /// cref="ParserRuleContext"/>.
        /// </summary>
        public ICollection<Declaration> NewDeclarations { get; private set; }

        /// <summary>
        /// Gets the collection of file-level hashtags that were found as a
        /// result of using this <see cref="DeclarationVisitor"/> to visit
        /// a <see cref="ParserRuleContext"/>.
        /// </summary>
        public ICollection<string> FileTags { get; private set; }

        /// <summary>
        /// The collection of all declarations - both the ones we received
        /// at the start, and the new ones we've derived ourselves.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Declaration> Declarations => ExistingDeclarations.Concat(NewDeclarations);

        public DeclarationVisitor(string sourceFileName, IEnumerable<Declaration> existingDeclarations, CommonTokenStream tokens)
        {
            this.ExistingDeclarations = existingDeclarations;
            this.NewDeclarations = new List<Declaration>();
            this.FileTags = new List<string>();
            this.sourceFileName = sourceFileName;
            this.tokens = tokens;
        }

        public override Yarn.Type VisitFile_hashtag(YarnSpinnerParser.File_hashtagContext context) {
            this.FileTags.Add(context.text.Text);
            return Yarn.Type.Undefined;
        }

        public override Yarn.Type VisitNode(YarnSpinnerParser.NodeContext context) {
            currentNodeContext = context;

            foreach (var header in context.header()) {
                if (header.header_key.Text == "title") {
                    currentNodeName = header.header_value.Text;
                }
            }
            Visit(context.body());
            return Yarn.Type.Undefined;
        }

        public override Yarn.Type VisitDeclare_statement(YarnSpinnerParser.Declare_statementContext context)
        {

            string description = null;

            var precedingComments = tokens.GetHiddenTokensToLeft(context.Start.TokenIndex, YarnSpinnerLexer.COMMENTS);

            if (precedingComments != null) {
                var precedingDocComments = precedingComments
                    // There are no tokens on the main channel with this
                    // one on the same line
                    .Where(t=>tokens.GetTokens()
                        .Where(ot => ot.Line == t.Line)
                        .Where(ot => ot.Type != YarnSpinnerLexer.INDENT && ot.Type != YarnSpinnerLexer.DEDENT)
                        .Where(ot => ot.Channel == YarnSpinnerLexer.DefaultTokenChannel)
                        .Count() == 0)
                    // The comment starts with a triple-slash
                    .Where(t => t.Text.StartsWith("///"))
                    // Get its text
                    .Select(t => t.Text.Replace("///", string.Empty).Trim());
                
                if (precedingDocComments.Count() > 0)
                {
                    description = string.Join(" ", precedingDocComments);
                }
            }

            var subsequentComments = tokens.GetHiddenTokensToRight(context.Stop.TokenIndex, YarnSpinnerLexer.COMMENTS);

            if (subsequentComments != null) {
                var subsequentDocComment = subsequentComments
                    // This comment is on the same line as the end of the declaration
                    .Where(t => t.Line == context.Stop.Line)
                    // The comment starts with a triple-slash
                    .Where(t => t.Text.StartsWith("///"))
                    // Get its text
                    .Select(t => t.Text.Replace("///", string.Empty).Trim())
                    // Get the first one, or null
                    .FirstOrDefault();

                if (subsequentDocComment != null)
                {
                    description = subsequentDocComment;
                }
            }

            // Get the name of the variable we're declaring
            string variableName = context.variable().GetText();

            // Does this variable name already exist in our declarations?
            var existingExplicitDeclaration = Declarations.Where(d => d.IsImplicit == false).FirstOrDefault(d => d.Name == variableName);
            if (existingExplicitDeclaration != null) {
                // Then this is an error, because you can't have two explicit declarations for the same variable.
                throw new TypeException(context, $"{existingExplicitDeclaration.Name} has already been declared in {existingExplicitDeclaration.SourceFileName}, line {existingExplicitDeclaration.SourceFileLine}", sourceFileName);
            }
            
            // Figure out the value and its type
            var constantValueVisitor = new ConstantValueVisitor(sourceFileName);
            var value = constantValueVisitor.Visit(context.value());

            // Do we have an explicit type declaration?
            if (context.type() != null)
            {
                Yarn.Type explicitType;

                // Get its type
                switch (context.type().typename.Type)
                {
                    case YarnSpinnerLexer.TYPE_STRING:
                        explicitType = Yarn.Type.String;
                        break;
                    case YarnSpinnerLexer.TYPE_BOOL:
                        explicitType = Yarn.Type.Bool;
                        break;
                    case YarnSpinnerLexer.TYPE_NUMBER:
                        explicitType = Yarn.Type.Number;
                        break;
                    default:
                        throw new ParseException(context, $"Unknown type {context.type().GetText()}");
                }

                // Check that it matches - if it doesn't, that's a type
                // error
                if (explicitType != value.type)
                {
                    throw new TypeException(context, $"Type {context.type().GetText()} does not match value {context.value().GetText()} ({value.type})", sourceFileName);
                }
            }

            // We're done creating the declaration!

            int positionInFile = context.Start.Line;
            
            // The start line of the body is the line after the delimiter
            int nodePositionInFile = this.currentNodeContext.BODY_START().Symbol.Line + 1;

            var declaration = new Declaration
            {
                Name = variableName,
                ReturnType = value.type,
                DefaultValue = value.value,
                Description = description,
                DeclarationType = Declaration.Type.Variable,
                SourceFileName = sourceFileName,
                SourceFileLine = positionInFile,
                SourceNodeName = currentNodeName,
                SourceNodeLine = positionInFile - nodePositionInFile,
                IsImplicit = false,
            };

            this.NewDeclarations.Add(declaration);

            return value.type;
        }
    }

    }
