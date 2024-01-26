using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#nullable enable

namespace YarnLanguageServer
{
    internal static class CSharpFileData
    {
        public static IEnumerable<Action> ParseActionsFromCode(string text, Uri uri)
        {
            var lineStarts = TextCoordinateConverter.GetLineStarts(text);

            var tree = CSharpSyntaxTree.ParseText(text, null, uri.AbsolutePath);

            var root = tree.GetCompilationUnitRoot();

            string[] actionAttributeNames = new string[] { "YarnCommand", "YarnFunction" };

            // Build the collection of method declarations that have a Yarn action attribute on them
            Dictionary<MethodDeclarationSyntax, AttributeSyntax> taggedMethods = new ();

            // Get all classes that do not have the GeneratedCode attribute
            var nonGeneratedClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(classDecl =>
            {
                return !classDecl.AttributeLists.Any(attrList => attrList.Attributes.Any(attr => (
                        attr.Name.ToString().EndsWith("GeneratedCode")
                        || attr.Name.ToString().EndsWith("GeneratedCodeAttribute")
                    )));
            });

            foreach (var method in nonGeneratedClasses
                .SelectMany(c => c.DescendantNodes())
                .OfType<MethodDeclarationSyntax>())
            {
                AttributeSyntax? actionAttribute = null;
                foreach (var list in method.AttributeLists)
                {
                    foreach (var attribute in list.Attributes)
                    {
                        var name = attribute.Name.ToString();
                        if (name.EndsWith("Attribute"))
                        {
                            name = name.Remove(name.LastIndexOf("Attribute"));
                        }

                        if (actionAttributeNames.Contains(name))
                        {
                            actionAttribute = attribute;
                            break;
                        }
                    }

                    if (actionAttribute != null)
                    {
                        break;
                    }
                }

                if (actionAttribute != null)
                {
                    taggedMethods.Add(method, actionAttribute);
                }
            }

            foreach (var taggedMethod in taggedMethods)
            {
                Action? action = GetActionFromTaggedMethod(taggedMethod.Key, taggedMethod.Value);
                if (action != null)
                {
                    action.SourceFileUri = uri;
                    action.SourceRange = TextCoordinateConverter.GetRange(taggedMethod.Key.Span, lineStarts);
                    yield return action;
                }
            }

            var addCommandInvocations = nonGeneratedClasses
                .SelectMany(c => c.DescendantNodes())
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString().Contains("AddCommandHandler"))
                .Where(i => i.ArgumentList.Arguments.Count == 2);

            var addFunctionInvocations = nonGeneratedClasses
                .SelectMany(c => c.DescendantNodes())
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString().Contains("AddFunction"))
                .Where(i => i.ArgumentList.Arguments.Count == 2);

            foreach (var invocation in addCommandInvocations)
            {
                Action action = GetActionFromRuntimeRegistration(invocation, ActionType.Command);
                action.SourceFileUri = uri;

                // Set the source range to the range of the method, if we know
                // it, otherwise the range of the registration
                action.SourceRange = TextCoordinateConverter.GetRange(action.MethodDeclarationSyntax?.Span ?? invocation.Span, lineStarts);

                yield return action;
            }

            foreach (var invocation in addFunctionInvocations)
            {
                Action action = GetActionFromRuntimeRegistration(invocation, ActionType.Function);
                action.SourceFileUri = uri;

                // Set the source range to the range of the method, if we know
                // it, otherwise the range of the registration
                action.SourceRange = TextCoordinateConverter.GetRange(action.MethodDeclarationSyntax?.Span ?? invocation.Span, lineStarts);

                yield return action;
            }
        }

        private static Action GetActionFromRuntimeRegistration(InvocationExpressionSyntax invocation, ActionType actionType)
        {
            var nameArgument = invocation.ArgumentList.Arguments.ElementAt(0);
            var implementationArgument = invocation.ArgumentList.Arguments.ElementAt(1);

            Action action;

            if (implementationArgument.Expression.Kind() == SyntaxKind.IdentifierName)
            {
                // This is an identifier name. Try to find a method in this
                // syntax tree with the same name.
                var root = implementationArgument.SyntaxTree.GetCompilationUnitRoot();
                var methodDeclaration = root
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m =>
                        m.Identifier.ToString() == implementationArgument.Expression.ToString()
                    );

                if (methodDeclaration != null)
                {
                    action = GetActionFromMethod(methodDeclaration);
                }
                else
                {
                    // We couldn't find the method. For now, leave this as a
                    // known action but without any knowledge of its
                    // implementation.
                    action = new Action();
                }
            }
            else
            {
                // This is a different kind of expression - it's potentially a
                // lambda function, a variable containing a delegate, or a more
                // complex reference to a member of another type. Without
                // actually doing full type checking, we can't be confident that
                // we'd find the right one. For now, leave this as a known
                // action but without any knowledge of its implementation.
                action = new Action();
            }

            action.Type = actionType;

            action.YarnName = nameArgument.Expression.ToString().Trim('"');

            return action;
        }

        private static Action? GetActionFromTaggedMethod(MethodDeclarationSyntax method, AttributeSyntax attribute)
        {
            Action action = GetActionFromMethod(method);

            // Attempt to get the command name from the first parameter, if it
            // has one. Otherwise, use the name of the method itself, and if
            // _that_ fails, fall back to an error string.
            string? yarnCommandAttributeName = attribute
                .ArgumentList?.Arguments.FirstOrDefault()?.ToString()
                .Trim('\"');

            action.YarnName = yarnCommandAttributeName ?? method.Identifier.ToString() ?? "<unknown method>";

            if (attribute.Name.ToString().Contains("YarnCommand"))
            {
                action.Type = ActionType.Command;

                if (action.IsStatic == false) {
                    // Instance command methods take an initial GameObject
                    // parameter, which indicates which game object should
                    // receive the command. Add this new parameter to the start
                    // of the list.
                    var targetParameter = new Action.ParameterInfo
                    {
                        Name = "target",
                        Description = "The game object that should receive the command",
                        Type = Yarn.BuiltinTypes.String,
                        DisplayTypeName = "GameObject",
                        IsParamsArray = false,
                    };
                    action.Parameters.Insert(0, targetParameter);
                }
            }
            else if (attribute.Name.ToString().Contains("YarnFunction"))
            {
                action.Type = ActionType.Function;
            }
            else
            {
                return null;
            }

            return action;
        }

        private static Action GetActionFromMethod(MethodDeclarationSyntax method)
        {
            var action = new Action
            {
                Documentation = GetDocumentation(method),
                ReturnType = GetYarnType(method.ReturnType),
                MethodDeclarationSyntax = method,
                Language = "csharp",
                Signature = $"{method.Identifier.Text}{method.ParameterList}",
            };

            foreach (var parameter in method.ParameterList.Parameters)
            {
                action.Parameters.Add(new Action.ParameterInfo
                {
                    Name = parameter.Identifier.ToString(),
                    Description = GetParameterDocumentation(method, parameter.Identifier.ToString()),
                    DisplayDefaultValue = parameter.Default?.Value?.ToString(),
                    Type = GetYarnType(parameter.Type),
                    DisplayTypeName = parameter.Type?.ToString() ?? "(unknown)",
                });
            }

            return action;
        }

        /// <summary>
        /// Returns the Yarn type that corresponds with the given <see
        /// cref="TypeSyntax"/>.
        /// </summary>
        /// <param name="typeSyntax">The type syntax to get a Yarn type for.</param>
        /// <returns>The Yarn type that corresponds to the given type syntax.</returns>
        private static Yarn.IType GetYarnType(TypeSyntax? typeSyntax)
        {
            // The type syntax is missing; fall back to treating this type as
            // 'Any'
            if (typeSyntax == null)
            {
                return Yarn.BuiltinTypes.Any;
            }

            switch (typeSyntax.ToString())
            {
                case "string":
                    return Yarn.BuiltinTypes.String;
                case "int":
                case "float":
                case "double":
                case "byte":
                case "uint":
                case "decimal":
                    return Yarn.BuiltinTypes.Number;
                case "bool":
                    return Yarn.BuiltinTypes.Boolean;
                default:
                    // We don't know the type. Mark it as 'any'.
                    return Yarn.BuiltinTypes.Any;
            }
        }

        private static string? GetDocumentation(MethodDeclarationSyntax methodDeclaration)
        {
            // The main string to use as the function's documentation.
            if (methodDeclaration.HasLeadingTrivia)
            {
                var trivias = methodDeclaration.GetLeadingTrivia();
                var structuredTrivia = trivias.LastOrDefault(t => t.HasStructure);
                if (structuredTrivia.Kind() != SyntaxKind.None)
                {
                    // The method contains structured trivia. Extract the
                    // documentation for it.
                    return GetDocumentationFromStructuredTrivia(structuredTrivia);
                }
                else
                {
                    // There isn't any structured trivia, but perhaps there's a
                    // comment above the method, which we can use as our
                    // documentation.
                    return GetDocumentationFromUnstructuredTrivia(trivias);
                }
            }
            else
            {
                return null;
            }
        }

        private static string? GetParameterDocumentation(MethodDeclarationSyntax method, string parameterName)
        {
            var trivias = method.GetLeadingTrivia();
            var structuredTrivia = trivias.LastOrDefault(t => t.HasStructure);
            if (structuredTrivia.Kind() == SyntaxKind.None)
            {
                return null;
            }

            var paramsXml = structuredTrivia
                .GetStructure()?
                .ChildNodes()
                .OfType<XmlElementSyntax>()
                .Where(x => x.StartTag.Name.ToString() == "param");

            var paramDoc = paramsXml?.FirstOrDefault(node =>
                node.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault()?.ToString() == parameterName
            );

            if (paramDoc == null)
            {
                return null;
            }

            var v = paramDoc.Content[0].ChildTokens()
                        .Where(ct => ct.Kind() != SyntaxKind.XmlTextLiteralNewLineToken)
                        .Select(ct => ct.ValueText.Trim())
                        ;
            var docstring = string.Join(" ", v).Trim();
            return docstring;
        }

        private static string? GetDocumentationFromStructuredTrivia(Microsoft.CodeAnalysis.SyntaxTrivia structuredTrivia)
        {
            string documentation;
            var triviaStructure = structuredTrivia.GetStructure();
            if (triviaStructure == null)
            {
                return null;
            }

            var summary = ExtractStructuredTrivia("summary", triviaStructure);
            var remarks = ExtractStructuredTrivia("remarks", triviaStructure);

            documentation = summary ?? triviaStructure.ToString();

            if (remarks != null) {
                documentation += "\n\n" + remarks;
            }

            return documentation;
        }

        private static string GetDocumentationFromUnstructuredTrivia(Microsoft.CodeAnalysis.SyntaxTriviaList trivias)
        {
            string documentation;
            bool emptyLineFlag = false;
            var documentationParts = Enumerable.Empty<string>();

            // loop in reverse order until hit something that doesn't look like it's related
            foreach (var trivia in trivias.Reverse())
            {
                var doneWithTrivia = false;
                switch (trivia.Kind())
                {
                    case SyntaxKind.EndOfLineTrivia:
                        // if we hit two lines in a row without a comment/attribute inbetween, we're done collecting trivia
                        if (emptyLineFlag == true) { doneWithTrivia = true; }
                        emptyLineFlag = true;
                        break;
                    case SyntaxKind.WhitespaceTrivia:
                        break;
                    case SyntaxKind.Attribute:
                        emptyLineFlag = false;
                        break;
                    case SyntaxKind.SingleLineCommentTrivia:
                    case SyntaxKind.MultiLineCommentTrivia:
                        documentationParts = documentationParts.Prepend(trivia.ToString().Trim('/', ' '));
                        emptyLineFlag = false;
                        break;
                    default:
                        doneWithTrivia = true;
                        break;
                }

                if (doneWithTrivia)
                {
                    break;
                }
            }

            documentation = string.Join(' ', documentationParts);
            return documentation;
        }

        private static string? ExtractStructuredTrivia(string tagName, Microsoft.CodeAnalysis.SyntaxNode triviaStructure)
        {
            // Find the tag that matches the requested name.
            var triviaMatch = triviaStructure
                .ChildNodes()
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(x =>
                    x.StartTag.Name.ToString() == tagName
                );

            if (triviaMatch != null
                && triviaMatch.Kind() != SyntaxKind.None
                && triviaMatch.Content.Any())
            {
                // Get all content from this element that isn't a newline, and
                // join it up into a single string.
                var nodes = triviaMatch
                    .Content.SelectMany((c) =>
                    {
                        return c
                        .DescendantNodesAndTokens()
                        .Where(ct => ct.Kind() != SyntaxKind.XmlTextLiteralNewLineToken)
                        .Select(ct => ct.AsToken().ValueText);
                    });

                var text = string.Join(string.Empty, nodes.Select(n => n.ToString()));

                return text.Trim();
            }

            return null;
        }
    }
}
