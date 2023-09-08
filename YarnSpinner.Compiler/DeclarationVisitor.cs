// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

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
    internal class DeclarationVisitor : YarnSpinnerParserBaseVisitor<Yarn.IType>
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
        /// Gets the collection of types known to this <see
        /// cref="DeclarationVisitor"/>.
        /// </summary>
        public IEnumerable<IType> Types { get; private set; }

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
        public IEnumerable<Declaration> Declarations => ExistingDeclarations.Concat(NewDeclarations);

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        private List<Diagnostic> diagnostics = new List<Diagnostic>();

        private static readonly IReadOnlyDictionary<string, IType> KeywordsToBuiltinTypes = new Dictionary<string, IType>
        {
            { "string", BuiltinTypes.String },
            { "number", BuiltinTypes.Number },
            { "bool", BuiltinTypes.Boolean },
        };

        /// <summary>A regular expression used to detect illegal characters
        /// in node titles.</summary>
        private System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"[\[<>\]{}\|:\s#\$]");

        public DeclarationVisitor(string sourceFileName, IEnumerable<Declaration> existingDeclarations, IEnumerable<IType> typeDeclarations, CommonTokenStream tokens)
        {
            this.ExistingDeclarations = existingDeclarations;
            this.NewDeclarations = new List<Declaration>();
            this.FileTags = new List<string>();
            this.sourceFileName = sourceFileName;
            this.Types = typeDeclarations;
            this.tokens = tokens;
        }

        public override Yarn.IType VisitFile_hashtag(YarnSpinnerParser.File_hashtagContext context)
        {
            this.FileTags.Add(context.text.Text);
            return null;
        }

        public override Yarn.IType VisitNode(YarnSpinnerParser.NodeContext context)
        {
            currentNodeContext = context;

            foreach (var header in context.header())
            {
                if (header.header_key.Text == "title")
                {
                    currentNodeName = header.header_value.Text;

                    if (regex.IsMatch(currentNodeName))
                    {
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, header, $"The node '{currentNodeName}' contains illegal characters."));
                    }
                }
            }

            var body = context.body();

            if (body != null)
            {
                base.Visit(body);
            }

            return null;
        }

        public override Yarn.IType VisitDeclare_statement(YarnSpinnerParser.Declare_statementContext context)
        {
            string description = Compiler.GetDocumentComments(tokens, context);

            // Get the name of the variable we're declaring
            var variableContext = context.variable();
            string variableName = variableContext.GetText();

            // Does this variable name already exist in our declarations?
            var existingExplicitDeclaration = Declarations.Where(d => d.IsImplicit == false).FirstOrDefault(d => d.Name == variableName);
            if (existingExplicitDeclaration != null)
            {
                // Then this is an error, because you can't have two explicit declarations for the same variable.
                string v = $"{existingExplicitDeclaration.Name} has already been declared in {existingExplicitDeclaration.SourceFileName}, line {existingExplicitDeclaration.SourceFileLine}";
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, v));
                return BuiltinTypes.Undefined;
            }

            // Figure out the value and its type
            var constantValueVisitor = new ConstantValueVisitor(context, sourceFileName, Types, ref this.diagnostics);
            if (context.value() == null)
            {
                // no value was provided, declare as undefined and continue
                return BuiltinTypes.Undefined;
            }
            var value = constantValueVisitor.Visit(context.value());

            // Did the source code name an explicit type?
            if (context.type != null)
            {
                Yarn.IType explicitType;

                if (KeywordsToBuiltinTypes.TryGetValue(context.type.Text, out explicitType) == false)
                {
                    // The type name provided didn't map to a built-in
                    // type. Look for the type in our Types collection.
                    explicitType = this.Types.FirstOrDefault(t => t.Name == context.type.Text);

                    if (explicitType == null)
                    {
                        // We didn't find a type by this name.
                        string v = $"Unknown type {context.type.Text}";
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, v));
                        return BuiltinTypes.Undefined;
                    }
                }

                // Check that the type we've found is compatible with the
                // type of the value that was provided - if it doesn't,
                // that's a type error
                if (TypeUtil.IsSubType(explicitType, value.Type) == false)
                {
                    string v = $"Type {context.type.Text} does not match value {context.value().GetText()} ({value.Type.Name})";
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, v));
                    return BuiltinTypes.Undefined;
                }
            }

            // We're done creating the declaration!
            int positionInFile = context.Start.Line;

            // The start line of the body is the line after the delimiter
            int nodePositionInFile = this.currentNodeContext.BODY_START().Symbol.Line + 1;

            var declaration = new Declaration
            {
                Name = variableName,
                Type = value.Type,
                DefaultValue = value.InternalValue,
                Description = description,
                SourceFileName = this.sourceFileName,
                SourceNodeName = this.currentNodeName,
                Range = new Range
                {
                    Start =
                    {
                        Line = variableContext.Start.Line - 1,
                        Character = variableContext.Start.Column,
                    },
                    End =
                    {
                        Line = variableContext.Stop.Line - 1,
                        Character = variableContext.Stop.Column + variableContext.GetText().Length,
                    },
                },
                IsImplicit = false,
            };

            this.NewDeclarations.Add(declaration);

            return value.Type;
        }
    }
}
