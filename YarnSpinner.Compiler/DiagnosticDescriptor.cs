// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes a diagnostic message with a unique code and template.
    /// </summary>
    /// <remarks>
    /// This class ensures that diagnostic codes and their descriptions
    /// are always paired correctly, preventing typos and mismatches.
    /// All diagnostics should be created using the predefined descriptors
    /// in this class.
    /// </remarks>
    public sealed class DiagnosticDescriptor
    {
        /// <summary>
        /// Gets the unique error code for this diagnostic (e.g., "YS0001").
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the message template for this diagnostic.
        /// </summary>
        /// <remarks>
        /// The template may contain placeholders like {0}, {1}, etc. for
        /// string formatting.
        /// </remarks>
        public string MessageTemplate { get; }

        /// <summary>
        /// Gets the default severity for this diagnostic.
        /// </summary>
        public Diagnostic.DiagnosticSeverity DefaultSeverity { get; }

        /// <summary>
        /// Gets a brief description of what this diagnostic means.
        /// </summary>
        public string Description { get; }

        private DiagnosticDescriptor(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
        {
            Code = code;
            MessageTemplate = messageTemplate;
            DefaultSeverity = defaultSeverity;
            Description = description;
        }

        /// <summary>
        /// Creates a new Diagnostic using this descriptor.
        /// </summary>
        /// <param name="sourceFile">The name of the file in which this error
        /// occurred.</param>
        /// <param name="args">The arguments to use when composing the
        /// diagnostic's message.</param>
        /// <returns>The diagnostic.</returns>
        public Diagnostic Create(string sourceFile, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, this, args);

        /// <summary>
        /// Creates a new Diagnostic using this descriptor.
        /// </summary>
        /// <param name="sourceFile">The name of the file in which this error
        /// occurred.</param>
        /// <param name="context">The parse context associated with this error.</param>
        /// <param name="args">The arguments to use when composing the
        /// diagnostic's message.</param>
        /// <returns>The diagnostic.</returns>
        public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, context, this, args);

        /// <summary>
        /// Creates a new Diagnostic using this descriptor.
        /// </summary>
        /// <param name="sourceFile">The name of the file in which this error
        /// occurred.</param>
        /// <param name="token">The token associated with this error.</param>
        /// <param name="args">The arguments to use when composing the
        /// diagnostic's message.</param>
        /// <returns>The diagnostic.</returns>
        public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, token, this, args);

        /// <summary>
        /// Creates a new Diagnostic using this descriptor.
        /// </summary>
        /// <param name="sourceFile">The name of the file in which this error
        /// occurred.</param>
        /// <param name="range">The range of the file associated with this error.</param>
        /// <param name="args">The arguments to use when composing the
        /// diagnostic's message.</param>
        /// <returns>The diagnostic.</returns>
        public Diagnostic Create(string sourceFile, Range range, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, range, this, args);

        /// <summary>
        /// Formats the message template with the provided arguments.
        /// </summary>
        /// <param name="args">Arguments to format the message template with.</param>
        /// <returns>The formatted message.</returns>
        public string FormatMessage(params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return MessageTemplate;
            }
            return string.Format(MessageTemplate, args);
        }

        #region Type Checking Errors

        /// <summary>
        /// YS0001: A variable has been implicitly declared with multiple conflicting types.
        /// </summary>
        /// <remarks>
        /// <para>This error occurs when a variable is used with different types across
        /// different files or contexts without an explicit declaration, and the
        /// compiler cannot determine which type is correct.</para>
        /// <para>Format placeholders: 0: variable name, 1: type names.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor ImplicitVariableTypeConflict = new DiagnosticDescriptor(
            code: "YS0001",
            messageTemplate: "Variable {0} has been implicitly declared with multiple types: {1}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Variable has conflicting implicit type declarations"
        );

        /// <summary>
        /// YS0002: A type mismatch occurred during type checking.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: expected type, 1: actual type.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor TypeMismatch = new DiagnosticDescriptor(
            code: "YS0002",
            messageTemplate: "Type mismatch: expected {0}, got {1}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Expression type does not match expected type"
        );

        /// <summary>
        /// YS0003: An undefined variable was referenced.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UndefinedVariable = new DiagnosticDescriptor(
            code: "YS0003",
            messageTemplate: "Variable '{0}' is used but not declared. Declare it with: <<declare {0} = value>>",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "Variable used without being declared"
        );
        #endregion

        #region Syntax Errors

        /// <summary>
        /// YS0004: Missing node delimiter (=== or ---).
        /// </summary>
        public static readonly DiagnosticDescriptor MissingDelimiter = new DiagnosticDescriptor(
            code: "YS0004",
            messageTemplate: "Missing node delimiter",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Node is missing required === delimiter"
        );

        /// <summary>
        /// YS0005: Malformed dialogue or syntax error.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: specific syntax error message.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor SyntaxError = new DiagnosticDescriptor(
            code: "YS0005",
            messageTemplate: "{0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Syntax error in Yarn script"
        );

        /// <summary>
        /// YS0006: Unclosed command (missing >>).
        /// </summary>
        public static readonly DiagnosticDescriptor UnclosedCommand = new DiagnosticDescriptor(
            code: "YS0006",
            messageTemplate: "Unclosed command: missing >>",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Command started with << but not closed with >>"
        );

        /// <summary>
        /// YS0007: Unclosed control flow scope (missing endif, endonce, etc.).
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: missing token.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UnclosedScope = new DiagnosticDescriptor(
            code: "YS0007",
            messageTemplate: "Unclosed scope: missing {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Control flow block not properly closed"
        );
        #endregion

        #region Semantic Warnings

        /// <summary>
        /// YS0008: Unreachable code detected.
        /// </summary>
        public static readonly DiagnosticDescriptor UnreachableCode = new DiagnosticDescriptor(
            code: "YS0008",
            messageTemplate: "Unreachable code detected",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "Code that will never be executed"
        );

        /// <summary>
        /// YS0009: Node is never referenced.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: node title.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UnusedNode = new DiagnosticDescriptor(
            code: "YS0009",
            messageTemplate: "Node '{0}' is never referenced",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Info,
            description: "Node exists but is never jumped to"
        );

        /// <summary>
        /// YS0010: Variable is declared but never used.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UnusedVariable = new DiagnosticDescriptor(
            code: "YS0010",
            messageTemplate: "Variable '{0}' is declared but never used",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Info,
            description: "Variable declaration that is not referenced"
        );

        #region Additional Codes

        /// <summary>
        /// YS0011: Duplicate node title.
        /// </summary>
        /// <remarks>
        /// <para>This diagnostic is not emitted for node groups where nodes
        /// share a title but have different `when:` clauses.</para>
        /// <para>Format placeholders: 0: node title.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor DuplicateNodeTitle = new DiagnosticDescriptor(
            code: "YS0011",
            messageTemplate: "Duplicate node title: '{0}'",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Multiple nodes have the same title without when: clauses"
        );

        /// <summary>
        /// YS0012: Jump to undefined node.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: node title.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UndefinedNode = new DiagnosticDescriptor(
            code: "YS0012",
            messageTemplate: "Jump to undefined node: '{0}'",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Jump target node does not exist"
        );

        /// <summary>
        /// YS0013: Invalid function call.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: function name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InvalidFunctionCall = new DiagnosticDescriptor(
            code: "YS0013",
            messageTemplate: "Invalid function call: {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Function called with incorrect parameters or does not exist"
        );

        /// <summary>
        /// YS0014: Invalid command.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: command name</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InvalidCommand = new DiagnosticDescriptor(
            code: "YS0014",
            messageTemplate: "Invalid command: {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Command is not recognized or has invalid syntax"
        );

        /// <summary>
        /// YS0015: Cyclic dependency detected.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: error message.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor CyclicDependency = new DiagnosticDescriptor(
            code: "YS0015",
            messageTemplate: "Cyclic dependency detected: {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "Circular reference between nodes or files"
        );

        /// <summary>
        /// YS0016: Unknown character name.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: character name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UnknownCharacter = new DiagnosticDescriptor(
            code: "YS0016",
            messageTemplate: "Unknown character: '{0}'",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "Character name not defined in project configuration"
        );

        #endregion

        /// <summary>
        /// YS0017: Lines cannot have both a '#line' tag and a '#shadow' tag.
        /// </summary>
        public static readonly DiagnosticDescriptor LinesCantHaveLineAndShadowTag = new DiagnosticDescriptor(
            code: "YS0017",
            messageTemplate: "Lines cannot have both a '#line' tag and a '#shadow' tag.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Shadow tags represent copies of another line elsewhere, and don't get their own line ID."
        );

        /// <summary>
        /// YS0018: Lines cannot have both a '#line' tag and a '#shadow' tag.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: line ID.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor DuplicateLineID = new DiagnosticDescriptor(
            code: "YS0018",
            messageTemplate: "Duplicate line ID '{0}'",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "All line IDs in a Yarn Spinner project must be unique."
        );

        /// <summary>
        /// YS0019: Line content after a non-flow-control command.
        /// </summary>
        public static readonly DiagnosticDescriptor LineContentAfterCommand = new DiagnosticDescriptor(
            code: "YS0019",
            messageTemplate: "Line content after '<<{0}>>' command. Commands should be on their own line.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "Non-flow-control commands should be on their own line"
        );

        /// <summary>
        /// YS0020: Line content before a non-flow-control command.
        /// </summary>
        public static readonly DiagnosticDescriptor LineContentBeforeCommand = new DiagnosticDescriptor(
            code: "YS0020",
            messageTemplate: "Line content before '<<{0}>>' command. Commands should start on a new line.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "Non-flow-control commands should start on their own line"
        );

        /// <summary>
        /// YS0021: Stray command end marker without matching start marker.
        /// </summary>
        public static readonly DiagnosticDescriptor StrayCommandEnd = new DiagnosticDescriptor(
            code: "YS0021",
            messageTemplate: "Stray '>>' without matching '<<'. Did you forget to open the command?",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Command end marker without corresponding start marker"
        );

        /// <summary>
        /// YS0022: Command keyword outside of command block.
        /// </summary>
        public static readonly DiagnosticDescriptor UnenclosedCommand = new DiagnosticDescriptor(
            code: "YS0022",
            messageTemplate: "'{0}' command must be enclosed in '<<' and '>>'. Did you mean '<<{0} ...'?",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Command keyword appearing outside of command markers"
        );

        /// <summary>
        /// YS0027: Node title or subtitle contains invalid characters.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: "title" or "subtitle", 1: the invalid name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InvalidNodeName = new DiagnosticDescriptor(
            code: "YS0027",
            messageTemplate: "The node {0} '{1}' contains invalid characters. Titles can only contain letters, numbers, and underscores.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Node titles and subtitles can only contain letters, numbers, and underscores."
        );

        #endregion

        /// <summary>
        /// YSXXX1: Redeclaration of existing variable
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor RedeclarationOfExistingVariable = new DiagnosticDescriptor(
            code: "YSXXX1",
            messageTemplate: "Redeclaration of existing variable {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Variables can only have a single declaration."
        );

        /// <summary>
        /// YSXXX2: Redeclaration of existing type
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: type name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor RedeclarationOfExistingType = new DiagnosticDescriptor(
            code: "YSXXX2",
            messageTemplate: "Redeclaration of existing type {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A type with this name already exists."
        );

        /// <summary>
        /// YSXXX3: Internal error.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: error description.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InternalError = new DiagnosticDescriptor(
            code: "YSXXX3",
            messageTemplate: "Internal compiler error: {0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "An internal error was detected by the compiler. Please file an issue."
        );

        /// <summary>
        /// YSXXX4: Unknown line ID {0} for shadow line.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: line ID.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor UnknownLineIDForShadowLine = new DiagnosticDescriptor(
            code: "YSXXX4",
            messageTemplate: "Unknown line ID {0} for shadow line",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Shadow lines must map to existing line IDs."
        );

        /// <summary>
        /// YSXXX5: Shadow lines must not have expressions
        /// </summary>
        public static readonly DiagnosticDescriptor ShadowLinesCantHaveExpressions = new DiagnosticDescriptor(
            code: "YSXXX5",
            messageTemplate: "Shadow lines must not have expressions",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Shadow lines must be text, and not contain any expressions."
        );

        /// <summary>
        /// YSXXX6: Shadow lines must have the same text as their source
        /// </summary>
        public static readonly DiagnosticDescriptor ShadowLinesMustHaveSameTextAsSource = new DiagnosticDescriptor(
            code: "YSXXX6",
            messageTemplate: "Shadow lines must have the same text as their source",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Shadow lines are copies of their source lines, and must have the exact same text as their source line."
        );

        /// <summary>
        /// YSXXX7: Smart variables cannot contain reference loops.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable reference, 1: smart variable name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor SmartVariableLoop = new DiagnosticDescriptor(
            code: "YSXXX7",
            messageTemplate: "Smart variables cannot contain reference loops (referencing {0} here creates a loop for the smart variable {1}).",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A smart variable's expression references itself through a chain of other smart variables."
        );

        /// <summary>
        /// YSXXX8: Variable declaration has a null default value.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable name, 1: type name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor NullDefaultValue = new DiagnosticDescriptor(
            code: "YSXXX8",
            messageTemplate: "Variable declaration {0} (type {1}) has a null default value. This is not allowed.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A variable declaration must have a non-null default value."
        );

        /// <summary>
        /// YSXXX9: Expression failed to resolve in a reasonable time.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: time limit in seconds.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor TypeSolverTimeout = new DiagnosticDescriptor(
            code: "YSXXX9",
            messageTemplate: "Expression failed to resolve in a reasonable time ({0}). Try simplifying this expression.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "The type solver exceeded its time limit while resolving this expression."
        );

        #region Additional Semantic Codes

        /// <summary>
        /// YS0028: Can't determine type of variable given its usage.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor TypeInferenceFailure = new DiagnosticDescriptor(
            code: "YS0028",
            messageTemplate: "Can't determine type of {0} given its usage. Manually specify its type with a declare statement.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "The compiler could not infer the type of this variable from how it is used."
        );

        /// <summary>
        /// YS0029: Can't determine the type of an expression.
        /// </summary>
        public static readonly DiagnosticDescriptor ExpressionTypeUndetermined = new DiagnosticDescriptor(
            code: "YS0029",
            messageTemplate: "Can't determine the type of this expression.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "The compiler could not resolve the type of this expression."
        );

        /// <summary>
        /// YS0030: Smart variable cannot be modified.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: variable name.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor SmartVariableReadOnly = new DiagnosticDescriptor(
            code: "YS0030",
            messageTemplate: "{0} cannot be modified (it's a smart variable).",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Smart variables are read-only computed values and cannot be assigned to."
        );

        /// <summary>
        /// YS0031: All nodes in a group must have a 'when' clause.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: node group title.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor NodeGroupMissingWhen = new DiagnosticDescriptor(
            code: "YS0031",
            messageTemplate: "All nodes in the group '{0}' must have a 'when' clause (use 'when: always' if you want this node to not have any conditions).",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "When some nodes in a group have 'when' clauses, all must have them."
        );

        /// <summary>
        /// YS0032: Duplicate subtitle in a node group.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: group name, 1: subtitle.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor DuplicateSubtitle = new DiagnosticDescriptor(
            code: "YS0032",
            messageTemplate: "More than one node in group {0} has subtitle {1}.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "Subtitles within a node group must be unique."
        );

        /// <summary>
        /// YS0033: Node is empty and will not be compiled.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: node title.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor EmptyNode = new DiagnosticDescriptor(
            code: "YS0033",
            messageTemplate: "Node \"{0}\" is empty and will not be included in the compiled output.",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Warning,
            description: "An empty node has no statements and will be excluded from compilation."
        );

        /// <summary>
        /// YS0034: Function cannot be used in Yarn Spinner scripts.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: function name, 1: reason.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InvalidLibraryFunction = new DiagnosticDescriptor(
            code: "YS0034",
            messageTemplate: "Function {0} cannot be used in Yarn Spinner scripts: {1}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A library function has an incompatible signature for use in Yarn scripts."
        );

        /// <summary>
        /// YS0035: Enum declaration error.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: error message.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor EnumDeclarationError = new DiagnosticDescriptor(
            code: "YS0035",
            messageTemplate: "{0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "An error occurred while validating an enum declaration."
        );

        /// <summary>
        /// YS0036: Language version too low for feature.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: error message.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor LanguageVersionTooLow = new DiagnosticDescriptor(
            code: "YS0036",
            messageTemplate: "{0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A language feature was used that requires a newer Yarn Spinner project version."
        );

        /// <summary>
        /// YS0037: Invalid literal value.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: error message.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InvalidLiteralValue = new DiagnosticDescriptor(
            code: "YS0037",
            messageTemplate: "{0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A constant literal value could not be parsed or is of an unexpected type."
        );

        /// <summary>
        /// YS0038: Invalid member access.
        /// </summary>
        /// <remarks>
        /// <para>Format placeholders: 0: error message.</para>
        /// </remarks>
        public static readonly DiagnosticDescriptor InvalidMemberAccess = new DiagnosticDescriptor(
            code: "YS0038",
            messageTemplate: "{0}",
            defaultSeverity: Diagnostic.DiagnosticSeverity.Error,
            description: "A type member access could not be resolved."
        );

        #endregion

        // Registry for lookup by code
        private static readonly Dictionary<string, DiagnosticDescriptor> descriptorsByCode = new Dictionary<string, DiagnosticDescriptor>
        {
            { ImplicitVariableTypeConflict.Code, ImplicitVariableTypeConflict },
            { TypeMismatch.Code, TypeMismatch },
            { UndefinedVariable.Code, UndefinedVariable },
            { MissingDelimiter.Code, MissingDelimiter },
            { SyntaxError.Code, SyntaxError },
            { UnclosedCommand.Code, UnclosedCommand },
            { UnclosedScope.Code, UnclosedScope },
            { UnreachableCode.Code, UnreachableCode },
            { UnusedNode.Code, UnusedNode },
            { UnusedVariable.Code, UnusedVariable },
            { DuplicateNodeTitle.Code, DuplicateNodeTitle },
            { UndefinedNode.Code, UndefinedNode },
            { InvalidFunctionCall.Code, InvalidFunctionCall },
            { InvalidCommand.Code, InvalidCommand },
            { CyclicDependency.Code, CyclicDependency },
            { UnknownCharacter.Code, UnknownCharacter },
            { LinesCantHaveLineAndShadowTag.Code, LinesCantHaveLineAndShadowTag },
            { DuplicateLineID.Code, DuplicateLineID },
            { LineContentAfterCommand.Code, LineContentAfterCommand },
            { LineContentBeforeCommand.Code, LineContentBeforeCommand },
            { StrayCommandEnd.Code, StrayCommandEnd },
            { UnenclosedCommand.Code, UnenclosedCommand },
            { InvalidNodeName.Code, InvalidNodeName },
            { RedeclarationOfExistingVariable.Code, RedeclarationOfExistingVariable },
            { RedeclarationOfExistingType.Code, RedeclarationOfExistingType },
            { InternalError.Code, InternalError },
            { UnknownLineIDForShadowLine.Code, UnknownLineIDForShadowLine },
            { ShadowLinesCantHaveExpressions.Code, ShadowLinesCantHaveExpressions },
            { ShadowLinesMustHaveSameTextAsSource.Code, ShadowLinesMustHaveSameTextAsSource },
            { SmartVariableLoop.Code, SmartVariableLoop },
            { NullDefaultValue.Code, NullDefaultValue },
            { TypeSolverTimeout.Code, TypeSolverTimeout },
            { TypeInferenceFailure.Code, TypeInferenceFailure },
            { ExpressionTypeUndetermined.Code, ExpressionTypeUndetermined },
            { SmartVariableReadOnly.Code, SmartVariableReadOnly },
            { NodeGroupMissingWhen.Code, NodeGroupMissingWhen },
            { DuplicateSubtitle.Code, DuplicateSubtitle },
            { EmptyNode.Code, EmptyNode },
            { InvalidLibraryFunction.Code, InvalidLibraryFunction },
            { EnumDeclarationError.Code, EnumDeclarationError },
            { LanguageVersionTooLow.Code, LanguageVersionTooLow },
            { InvalidLiteralValue.Code, InvalidLiteralValue },
            { InvalidMemberAccess.Code, InvalidMemberAccess },
        };

        /// <summary>
        /// Gets a diagnostic descriptor by its code.
        /// </summary>
        /// <param name="code">The diagnostic code to look up.</param>
        /// <returns>The descriptor, or null if not found.</returns>
        public static DiagnosticDescriptor? GetDescriptor(string code)
        {
            descriptorsByCode.TryGetValue(code, out var descriptor);
            return descriptor;
        }
    }
}
