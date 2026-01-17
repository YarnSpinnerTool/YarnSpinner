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

        public Diagnostic Create(string sourceFile, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, this, args);
        public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, context, this, args);

        public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, params string[] args)
            => Diagnostic.CreateDiagnostic(sourceFile, token, this, args);
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
            messageTemplate: "Undefined variable: {0}",
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
            { RedeclarationOfExistingVariable.Code, RedeclarationOfExistingVariable },
            { RedeclarationOfExistingType.Code, RedeclarationOfExistingType },
            { InternalError.Code, InternalError },
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
