using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

#nullable enable

namespace YarnLanguageServer
{
    /// <summary>
    /// An Action is a function or a command that can be invoked from Yarn
    /// scripts.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("Action ({Type}): {YarnName}")]
    public class Action
    {
        public ActionType Type { get; set; }

        public string YarnName { get; set; } = string.Empty;

        public Uri? SourceFileUri { get; set; }

        public Range? SourceRange { get; set; }

        public string? Documentation { get; set; }

        public bool IsBuiltIn { get; set; }

        public string ImplementationName => MethodDeclarationSyntax?.Identifier.ToString() ?? "(unknown)";

        /// <summary>
        /// Gets a value indicating whether this action's method is known. If it
        /// is not, then parameter and type information is not available.
        /// </summary>
        public bool HasMethod => MethodDeclarationSyntax != null;

        public MethodDeclarationSyntax? MethodDeclarationSyntax { get; set; }

        public IList<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

        /// <summary>
        /// Gets or sets the return type for the action.
        /// </summary>
        /// <remarks>
        /// This method is only never non-null for functions.
        /// </remarks>
        public Yarn.IType? ReturnType { get; set; }

        /// <summary>
        /// Gets a value indicating whether the implementing method is static.
        /// </summary>
        public bool IsStatic => MethodDeclarationSyntax?.Modifiers.Any(m => m.ToString() == "static") ?? true;

        /// <summary>
        /// The language that the action was defined in.
        /// </summary>
        /// <remarks>
        /// For example, if the action is defined in a C# source file, then this property is <c>csharp</c>.
        /// </remarks>
        public string? Language { get; internal set; }

        /// <summary>
        /// The signature of the action, as originally defined in the source file.
        /// </summary>
        public string? Signature { get; internal set; }

        public struct ParameterInfo
        {
            public string Name;
            public string? Description;

            /// <summary>
            /// The string to display in the editor for the default value of
            /// this parameter.
            /// </summary>
            public string? DisplayDefaultValue;

            /// <summary>
            /// The name of this parameter's type as it appears in the game's
            /// source code, for display in the editor.
            /// </summary>
            /// <remarks>
            /// This may be different to the Yarn type; for example, a parameter
            /// of type <c>UnityEngine.GameObject</c> is mapped to a Yarn
            /// string. However, it's useful to show the 'actual' type, so that
            /// writers of Yarn scripts know what kind of value will be received
            /// by the implementing method.
            /// </remarks>
            public string DisplayTypeName;

            public Yarn.IType Type;

            public bool IsParamsArray;

            public bool IsOptional => IsParamsArray || DisplayDefaultValue != null;
        }

        /// <summary>
        /// Gets a <see cref="Yarn.Compiler.Declaration"/> for this action, for
        /// use in compilation.
        /// </summary>
        /// <remarks>
        /// If this action is not a Function, the value of this property is <see
        /// langword="null"/>.
        /// </remarks>
        public Yarn.Compiler.Declaration? Declaration {
            get {
                if (this.Type != ActionType.Function) {
                    return null;
                }

                var typeBuilder = new Yarn.Compiler.FunctionTypeBuilder()
                    .WithReturnType(this.ReturnType);

                foreach (var param in this.Parameters) {
                    typeBuilder = typeBuilder.WithParameter(param.Type);
                }

                var decl = new Yarn.Compiler.DeclarationBuilder()
                    .WithName(this.YarnName)
                    .WithDescription(this.Documentation)
                    .WithImplicit(false)
                    .WithType(typeBuilder.FunctionType)
                    .Declaration;

                return decl;
            }
        }
    }
}
