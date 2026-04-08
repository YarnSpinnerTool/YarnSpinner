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
    public abstract partial class DiagnosticDescriptor
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

        /// <summary>
        /// The number of parameters this descriptor has in its messages.
        /// </summary>
        protected abstract int MessageParameterCount { get; }

        private DiagnosticDescriptor(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
        {
            Code = code;
            MessageTemplate = messageTemplate;
            DefaultSeverity = defaultSeverity;
            Description = description;

#if DEBUG
            // In debug builds, we'll do additional checking to make sure that
            // our descriptors are doing what they should be. (This isn't
            // included in release builds, because this information is
            // irrelevant to end users.)
            for (int i = 0; i < this.MessageParameterCount; i++)
            {
                if (MessageTemplate.Contains("{" + i + "}") == false)
                {
                    // This descriptor is invalid - its type indicates it needs
                    // N parameters, but not all of them are used in the message
                    throw new ArgumentException($"Message template for error {code} does not make use of message parameter {i}", nameof(messageTemplate));
                }
            }

            try
            {

                var matches = System.Text.RegularExpressions.Regex.Matches(MessageTemplate, @"\{(\d+)\}");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var number = int.Parse(match.Groups[1].Value);

                    if (number >= MessageParameterCount)
                    {
                        // This descriptor is invalid - its type indicates it
                        // needs N parameters, but its message references more
                        // than N unique values
                        throw new ArgumentException($"Message template for error {code} refers to message parameter {number}, but this descriptor only uses {MessageParameterCount}", nameof(messageTemplate));
                    }
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("Failed to parse message for error code " + code, nameof(messageTemplate), e);
            }

#endif
        }


        /// <inheritdoc cref="DiagnosticDescriptor"/>
        public sealed class DiagnosticDescriptor0 : DiagnosticDescriptor
        {
            internal DiagnosticDescriptor0(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
                : base(code, messageTemplate, defaultSeverity, description) { }

            /// <inheritdoc/>
            protected override int MessageParameterCount => 0;

            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, string[])"/>
            public Diagnostic Create(string sourceFile)
                => base.Create(sourceFile);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string,Antlr4.Runtime.ParserRuleContext, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context)
                => base.Create(sourceFile, context);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Antlr4.Runtime.IToken, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token)
                => base.Create(sourceFile, token);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Range, string[])"/>
            public Diagnostic Create(string sourceFile, Range range)
                => base.Create(sourceFile, range);
        }

#pragma warning disable CS1573 // parameter has no documentation but others do; suppressed because we inherit docs from elsewhere
        /// <inheritdoc cref="DiagnosticDescriptor"/>
        public sealed class DiagnosticDescriptor1 : DiagnosticDescriptor
        {
            internal DiagnosticDescriptor1(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
                : base(code, messageTemplate, defaultSeverity, description) { }

            /// <inheritdoc/>
            protected override int MessageParameterCount => 1;

            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, string[])"/>
            /// <param name="message">The value to use for the first parameter in this diagnostic's message.</param>
            public Diagnostic Create(string sourceFile, string message)
                => base.Create(sourceFile, message);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string,Antlr4.Runtime.ParserRuleContext, string[])"/>
            /// <param name="message">The value to use for the first parameter in this diagnostic's message.</param>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, string message)
                => base.Create(sourceFile, context, message);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Antlr4.Runtime.IToken, string[])"/>
            /// <param name="message">The value to use for the first parameter in this diagnostic's message.</param>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, string message)
                => base.Create(sourceFile, token, message);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Range, string[])"/>
            /// <param name="message">The value to use for the first parameter in this diagnostic's message.</param>
            public Diagnostic Create(string sourceFile, Range range, string message)
                => base.Create(sourceFile, range, message);
        }

        /// <inheritdoc cref="DiagnosticDescriptor"/>
        public sealed class DiagnosticDescriptor2 : DiagnosticDescriptor
        {
            internal DiagnosticDescriptor2(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
                : base(code, messageTemplate, defaultSeverity, description) { }

            /// <inheritdoc/>
            protected override int MessageParameterCount => 2;

            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, string[])"/>
            public Diagnostic Create(string sourceFile, string message1, string message2)
                => base.Create(sourceFile, message1, message2);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string,Antlr4.Runtime.ParserRuleContext, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, string message1, string message2)
                => base.Create(sourceFile, context, message1, message2);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Antlr4.Runtime.IToken, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, string message1, string message2)
                => base.Create(sourceFile, token, message1, message2);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Range, string[])"/>
            public Diagnostic Create(string sourceFile, Range range, string message1, string message2)
                => base.Create(sourceFile, range, message1, message2);
        }

        /// <inheritdoc cref="DiagnosticDescriptor"/>
        public sealed class DiagnosticDescriptor3 : DiagnosticDescriptor
        {
            internal DiagnosticDescriptor3(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
                : base(code, messageTemplate, defaultSeverity, description) { }

            /// <inheritdoc/>
            protected override int MessageParameterCount => 3;

            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, string[])"/>
            public Diagnostic Create(string sourceFile, string message1, string message2, string message3)
                => base.Create(sourceFile, message1, message2, message3);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string,Antlr4.Runtime.ParserRuleContext, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, string message1, string message2, string message3)
                => base.Create(sourceFile, context, message1, message2, message3);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Antlr4.Runtime.IToken, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, string message1, string message2, string message3)
                => base.Create(sourceFile, token, message1, message2, message3);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Range, string[])"/>
            public Diagnostic Create(string sourceFile, Range range, string message1, string message2, string message3)
                => base.Create(sourceFile, range, message1, message2, message3);
        }

        /// <inheritdoc cref="DiagnosticDescriptor"/>
        public sealed class DiagnosticDescriptor4 : DiagnosticDescriptor
        {
            internal DiagnosticDescriptor4(string code, string messageTemplate, Diagnostic.DiagnosticSeverity defaultSeverity, string description)
                : base(code, messageTemplate, defaultSeverity, description) { }

            /// <inheritdoc/>
            protected override int MessageParameterCount => 4;

            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, string[])"/>
            public Diagnostic Create(string sourceFile, string message1, string message2, string message3, string message4)
                => base.Create(sourceFile, message1, message2, message3, message4);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string,Antlr4.Runtime.ParserRuleContext, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, string message1, string message2, string message3, string message4)
                => base.Create(sourceFile, context, message1, message2, message3, message4);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Antlr4.Runtime.IToken, string[])"/>
            public Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, string message1, string message2, string message3, string message4)
                => base.Create(sourceFile, token, message1, message2, message3, message4);
            /// <inheritdoc cref="DiagnosticDescriptor.Create(string, Range, string[])"/>
            public Diagnostic Create(string sourceFile, Range range, string message1, string message2, string message3, string message4)
                => base.Create(sourceFile, range, message1, message2, message3, message4);
        }
#pragma warning restore

        /// <summary>
        /// Creates a new Diagnostic using this descriptor.
        /// </summary>
        /// <param name="sourceFile">The name of the file in which this error
        /// occurred.</param>
        /// <param name="args">The arguments to use when composing the
        /// diagnostic's message.</param>
        /// <returns>The diagnostic.</returns>
        private Diagnostic Create(string sourceFile, params string[] args)
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
        private Diagnostic Create(string sourceFile, Antlr4.Runtime.ParserRuleContext context, params string[] args)
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
        private Diagnostic Create(string sourceFile, Antlr4.Runtime.IToken token, params string[] args)
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
        private Diagnostic Create(string sourceFile, Range range, params string[] args)
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

        // Registry for lookup by code
        private static readonly Dictionary<string, DiagnosticDescriptor> descriptorsByCode;

        static DiagnosticDescriptor()
        {
            descriptorsByCode = GetDescriptorDictionary();
        }

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
