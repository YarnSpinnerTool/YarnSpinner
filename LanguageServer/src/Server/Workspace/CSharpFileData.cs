using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static MoreLinq.Extensions.PartitionExtension;

namespace YarnLanguageServer
{
    internal class CSharpFileData : IFunctionDefinitionsProvider
    {
        public Dictionary<string, RegisteredFunction> FunctionDefinitions { get; set; }
        public ImmutableArray<int> LineStarts { get; protected set; }

        public CSharpFileData(string text, Uri uri)
        {
            FunctionDefinitions = new Dictionary<string, RegisteredFunction>();
            Update(text, uri);
        }

        public void Update(string text, Uri uri)
        {
            LineStarts = TextCoordinateConverter.GetLineStarts(text);

            FunctionDefinitions.Clear(); // definitely more incremental ways to update but we probably aren't updating super often

            var tree = CSharpSyntaxTree.ParseText(text);

            var root = tree.GetCompilationUnitRoot();

            var commandAttributeMatches = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.AttributeLists.Any(a =>
                a.Attributes.Any(a2 =>
                a2.Name.ToString().Contains("YarnCommand"))));

            foreach (var command in commandAttributeMatches)
            {
                var yarnName =
                    command.AttributeLists.First().Attributes.First(a => a.Name.ToString().Contains("YarnCommand"))
                    .ArgumentList.Arguments.First().ToString()
                    .Trim('\"');
                FunctionDefinitions[yarnName] = CreateFunctionObject(uri, yarnName, command, true, true);
            }

            var commandRegMatches = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(e =>
                e.Expression.ToString().Contains("AddCommandHandler")).Select(m => (m, true));
            var functionRegMatches = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(e =>
                e.Expression.ToString().Contains("AddFunction")).Select(m => (m, false));

            var regMatches = commandRegMatches.Concat(functionRegMatches);

            // TODO: This doesn't match anonymous functions, fix that at somepoint
            (var commandBridges, var unmatchableCommandBridges) = regMatches
                .Select(e =>
                    (e.m.ArgumentList.Arguments[0].ToString().Trim('\"'), e.m.ArgumentList.Arguments[1].Expression, e.Item2))
                .Partition(b =>
                    b.Item2.Kind() == SyntaxKind.IdentifierName);




            var registeredmatched = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => commandBridges.Select(b => b.Item2.ToString()).Contains(m.Identifier.ToString()));
            try
            {
                foreach (var unmatchable in unmatchableCommandBridges)
                {
                    FunctionDefinitions[unmatchable.Item1] = new RegisteredFunction
                    {
                        DefinitionFile = uri,
                        DefinitionName = unmatchable.Item1,
                        IsBuiltIn = false,
                        IsCommand = unmatchable.Item3,
                        Documentation = string.Empty,
                        Language = "csharp",
                        Parameters = null,
                        Signature = $"{unmatchable.Item1}(?)",
                        YarnName = unmatchable.Item1,
                        DefinitionRange =
                            PositionHelper.GetRange(LineStarts, unmatchable.Item2.GetLocation().SourceSpan.Start, unmatchable.Item2.GetLocation().SourceSpan.End),
                    };
                }

                var matchedCommandbridges = commandBridges.Select(cb => (cb.Item1, registeredmatched.FirstOrDefault(rm => rm.Identifier.ToString() == cb.Item2.ToString()), cb.Item3))
                    .Where(cbm => cbm.Item2 != null);

                foreach ((var yarnName, var command, var isCommand) in matchedCommandbridges)
                {
                    try
                    {
                        FunctionDefinitions[yarnName] = CreateFunctionObject(uri, yarnName, command, isCommand, false);
                    }
                    catch (Exception e) { 
                    }
                }
            }
            catch (Exception e) { 
            }
        }

        private RegisteredFunction CreateFunctionObject(Uri uri, string yarnName, MethodDeclarationSyntax methodDeclaration, bool isCommand, bool isAttributeMatch = false)
        {
            string documentation = string.Empty;
            Dictionary<string, string> paramsDocumentation = new Dictionary<string, string>();

            if (methodDeclaration.HasLeadingTrivia)
            {
                var trivias = methodDeclaration.GetLeadingTrivia();
                var structuredTrivia = trivias.LastOrDefault(t => t.HasStructure);
                if (structuredTrivia.Kind() != SyntaxKind.None)
                {
                    var triviaStructure = structuredTrivia.GetStructure();
                    var summaryXml = triviaStructure.ChildNodes().OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.ToString() == "summary");
                    if (summaryXml != null && summaryXml.Kind() != SyntaxKind.None && summaryXml.Content.Any())
                    {
                        var v = summaryXml.Content[0].ChildTokens().Where(ct => ct.Kind() != SyntaxKind.XmlTextLiteralNewLineToken)
                            .Select(ct => ct.ValueText.Trim())
                            ;
                        documentation = string.Join(" ", v).Trim();
                    }
                    else
                    {
                        // Looks like xml, but doesn't have a summary element
                        // Will have extraneous // characters if multiline
                        documentation = triviaStructure.ToString();
                    }

                    var paramsXml = triviaStructure.ChildNodes().OfType<XmlElementSyntax>().Where(x => x.StartTag.Name.ToString() == "param");
                    foreach (var paramXml in paramsXml)
                    {
                        if (paramXml != null && paramXml.Kind() != SyntaxKind.None && paramXml.Content.Any())
                        {
                            var v = paramXml.Content[0].ChildTokens().Where(ct => ct.Kind() != SyntaxKind.XmlTextLiteralNewLineToken)
                                .Select(ct => ct.ValueText.Trim())
                                ;
                            var paraname = paramXml.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault().ToString();

                            var docstring = string.Join(" ", v).Trim();
                            if (!string.IsNullOrWhiteSpace(paraname) && !string.IsNullOrWhiteSpace(docstring))
                            {
                                paramsDocumentation[paraname] = docstring;
                            }
                        }
                    }
                }
                else
                {
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
                }
            }

            var parameters = methodDeclaration.ParameterList.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Identifier.Text,
                Type = p.Type.ToString(),
                Documentation = paramsDocumentation.ContainsKey(p.Identifier.Text) ? paramsDocumentation[p.Identifier.Text] : p.ToFullString(),
                DefaultValue = p.Default?.Value?.ToString(),
                IsParamsArray = p.Modifiers.Any(m => m.Text.Contains("params")),
            });
            if (isAttributeMatch)
            {
                parameters = parameters.Prepend(new ParameterInfo
                {
                    Name = "GameObjectName",
                    Type = "string",
                    Documentation = "Name of the game object to receive this command",
                    DefaultValue = null,
                    IsParamsArray = false,
                });
            }

            return new RegisteredFunction
            {
                YarnName = yarnName,
                DefinitionFile = uri,
                DefinitionName = methodDeclaration.Identifier.Text,
                IsBuiltIn = false,
                IsCommand = isCommand,
                Parameters = parameters,
                MinParameterCount = parameters.Count(p => p.DefaultValue == null && !p.IsParamsArray),
                MaxParameterCount = parameters.Any(p => p.IsParamsArray) ? null : parameters.Count(),
                DefinitionRange = PositionHelper.GetRange(LineStarts, methodDeclaration.GetLocation().SourceSpan.Start, methodDeclaration.GetLocation().SourceSpan.End),
                Documentation = documentation,
                Signature = $"{methodDeclaration.Identifier.Text}{methodDeclaration.ParameterList}",
            };
        }
    }
}