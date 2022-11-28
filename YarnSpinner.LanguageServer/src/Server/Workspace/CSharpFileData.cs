using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static MoreLinq.Extensions.PartitionExtension;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    internal class CSharpFileData : IDefinitionsProvider
    {
        public Dictionary<string, RegisteredDefinition> Definitions { get; set; }
        public IEnumerable<(string yarnName, Range definitionRange, bool isCommand)> UnmatchableBridges { get; protected set; }
        public ImmutableArray<int> LineStarts { get; protected set; }
        private Uri Uri { get; set; }
        private Workspace Workspace { get; set; }

        private CompilationUnitSyntax root;
        public CSharpFileData(string text, Uri uri, Workspace workspace, bool onePass = false)
        {
            Definitions = new Dictionary<string, RegisteredDefinition>();
            this.Uri = uri;
            this.Workspace = workspace;

            LineStarts = TextCoordinateConverter.GetLineStarts(text);

            Definitions.Clear(); // definitely more incremental ways to update but we probably aren't updating super often

            var tree = CSharpSyntaxTree.ParseText(text);

            root = tree.GetCompilationUnitRoot();
            
            RegisterCommandAndFunctionBridges(); // Technically this doesn't register them until going through unmatched commands

            // TODO: Making these come later to remove any corresponding entries in Workspace.UnmatchedDefinitions is definitly some code smell.
            // Might be cleaner to build the functionDefinitionCache as we go instead of storing things up in c# file datas.
            RegisterCommandAttributeMatches();
            RegisterFunctionAttributeMatches();
            RegisterCommentTaggedCommandsAndFunctions();

            // Let's check off any functions we can while we have everything open
            LookForUnmatchedCommands(onePass);
        }

        public void LookForUnmatchedCommands(bool isLastTime) {

            // TODO: This is definitly some late night code. Shuffle around to avoid the double lookup through UnmatchedCommandNames.
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => Workspace.UnmatchedDefinitions.Select(b => b.DefinitionName)
                    .Contains(m.Identifier.ToString()));

            var matches = methods.Select(m =>
            {
                var matchedUcn = Workspace.UnmatchedDefinitions.Find(ucn => ucn.DefinitionName == m.Identifier.ToString());
                return (matchedUcn, m);
            });

            foreach ((var matchedUcn, var command) in matches)
            {
                try
                {
                    // We don't want to override a function that has already matched in this file
                    if (!Definitions.ContainsKey(matchedUcn.YarnName))
                    {
                        Definitions[matchedUcn.YarnName] = CreateFunctionObject(Uri, matchedUcn.YarnName, command, matchedUcn.IsCommand, 3, false);
                    }

                    Workspace.UnmatchedDefinitions.RemoveAll(ucn => ucn.YarnName == matchedUcn.YarnName);
                }
                catch (Exception e)
                {
                }
            }

            if (isLastTime)
            {
                root = null; // we don't need this anymore, so don't want it hogging up memory
            }
        }

        private void RegisterCommandAttributeMatches()
        {
            var commandAttributeMatches = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.AttributeLists.Any(a =>
                a.Attributes.Any(a2 =>
                a2.Name.ToString().Contains("YarnCommand"))));

            foreach (var command in commandAttributeMatches)
            {
                var yarnCommandAttribute = command.AttributeLists.Where(z => z.Attributes.Any(y => y.Name.ToString().Contains("YarnCommand"))).First().Attributes.First();

                // Attempt to get the command name from the first parameter, if
                // it has one. Otherwise, use the name of the method itself, and if _that_ fails, fall back to an error string.
                string yarnCommandAttributeName = yarnCommandAttribute
                                    .ArgumentList?.Arguments.FirstOrDefault()?.ToString()
                                    .Trim('\"');

                var yarnName = yarnCommandAttributeName ?? command.Identifier.ToString() ?? "<unknown method>";

                Definitions[yarnName] = CreateFunctionObject(Uri, yarnName, command, true, 2, true);
                Workspace.UnmatchedDefinitions.RemoveAll(ucn => ucn.YarnName == yarnName); // Matched some comands, can mark them off the list!
            }
        }

        private void RegisterFunctionAttributeMatches()
        {
            var functionAttributeMatches = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.AttributeLists.Any(a =>
                a.Attributes.Any(a2 =>
                a2.Name.ToString().Contains("YarnFunction"))));

            foreach (var command in functionAttributeMatches)
            {
                var yarnFunctionAttribute = command.AttributeLists.Where(z => z.Attributes.Any(y => y.Name.ToString().Contains("YarnFunction"))).First().Attributes.First();

                // // Attempt to get the function name from the first parameter, if
                // // it has one. Otherwise, use the name of the method itself, and if _that_ fails, fall back to an error string.
                string yarnFunctionAttributeName = yarnFunctionAttribute.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('\"');

                var yarnName = yarnFunctionAttributeName ?? command.Identifier.ToString() ?? "<unknown method>";

                Definitions[yarnName] = CreateFunctionObject(Uri, yarnName, command, true, 2, true);
                Workspace.UnmatchedDefinitions.RemoveAll(ucn => ucn.YarnName == yarnName); // Matched some functions, can mark them off the list!
            }
        }

        private void RegisterCommentTaggedCommandsAndFunctions()
        {
            var commentMatches = root.DescendantNodes()
               .OfType<MethodDeclarationSyntax>()
               .Where(m => m.HasLeadingTrivia && m.GetLeadingTrivia().Any(t => t.HasStructure));
            foreach (var match in commentMatches)
            {
                var triviaStructure = match.GetLeadingTrivia().LastOrDefault(t => t.HasStructure).GetStructure();

                var functionName = ExtractStructuredTrivia("yarnfunction", triviaStructure);
                var commandName = ExtractStructuredTrivia("yarncommand", triviaStructure);
                var yarnName = functionName ?? commandName;
                var isCommand = string.IsNullOrWhiteSpace(functionName);
                if (!string.IsNullOrWhiteSpace(yarnName))
                {
                    Definitions[yarnName] = CreateFunctionObject(Uri, yarnName, match, isCommand, 1, false);
                    Workspace.UnmatchedDefinitions.RemoveAll(ucn => ucn.YarnName == yarnName);
                }
            }
        }

        private void RegisterCommandAndFunctionBridges()
        {
            var commandRegMatches = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(e =>
               e.Expression.ToString().Contains("AddCommandHandler")).Select(m => (m, true));
            var functionRegMatches = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(e =>
                e.Expression.ToString().Contains("AddFunction")).Select(m => (m, false));

            var regMatches = commandRegMatches.Concat(functionRegMatches);

            (var commandBridges, var unmatchableBridges) = regMatches
                .Where(e => e.m.ArgumentList.Arguments.Count >= 2)
                .Select(e =>
                    (YarnName: e.m.ArgumentList.Arguments[0].ToString().Trim('\"'), Expression: e.m.ArgumentList.Arguments[1].Expression, IsCommand: e.Item2))
                .Partition(b =>
                    b.Expression.Kind() == SyntaxKind.IdentifierName || b.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression);

            UnmatchableBridges = unmatchableBridges
                .Select(b => (
                    b.YarnName, PositionHelper.GetRange(LineStarts, b.Expression.GetLocation().SourceSpan.Start, b.Expression.GetLocation().SourceSpan.End),
                    b.IsCommand));

            foreach (var cb in commandBridges)
            {
                // we don't know if these will be defined in this file or not,
                // so let's mark them as unmatched for now, and then mark off what we can in this first pass
                Workspace.UnmatchedDefinitions.Add((cb.YarnName, cb.Expression.ToString().Split('.').LastOrDefault().Trim(), cb.IsCommand, null));
            }
        }

        private RegisteredDefinition CreateFunctionObject(Uri uri, string yarnName, MethodDeclarationSyntax methodDeclaration, bool isCommand, int priority, bool isAttributeMatch = false)
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
                    var summary = ExtractStructuredTrivia("summary", triviaStructure);

                    documentation = summary ?? triviaStructure.ToString();

                    var paramsXml = triviaStructure.ChildNodes().OfType<XmlElementSyntax>().Where(x => x.StartTag.Name.ToString() == "param");
                    foreach (var paramXml in paramsXml)
                    {
                        if (paramXml != null && paramXml.Kind() != SyntaxKind.None && paramXml.Content.Any())
                        {
                            var v = paramXml.Content[0].ChildTokens()
                                .Where(ct => ct.Kind() != SyntaxKind.XmlTextLiteralNewLineToken)
                                .Select(ct => ct.ValueText.Trim())
                                ;
                            var docstring = string.Join(" ", v).Trim();

                            var paraname = paramXml.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault().ToString();

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

            // so it looks like the range stuff is correct, needs deeper investigation
            // in the meantime get the addcommand stuff working
            var drange = PositionHelper.GetRange(LineStarts, methodDeclaration.GetLocation().SourceSpan.Start, methodDeclaration.GetLocation().SourceSpan.End);
            return new RegisteredDefinition
            {
                YarnName = yarnName,
                DefinitionFile = uri,
                DefinitionName = methodDeclaration.Identifier.Text,
                IsBuiltIn = false,
                IsCommand = isCommand,
                Parameters = parameters,
                Priority = priority,
                MinParameterCount = parameters.Count(p => p.DefaultValue == null && !p.IsParamsArray),
                MaxParameterCount = parameters.Any(p => p.IsParamsArray) ? null : parameters.Count(),
                DefinitionRange = drange,//PositionHelper.GetRange(LineStarts, methodDeclaration.GetLocation().SourceSpan.Start, methodDeclaration.GetLocation().SourceSpan.End),
                Documentation = documentation,
                Language = Utils.CSharpLanguageID,
                Signature = $"{methodDeclaration.Identifier.Text}{methodDeclaration.ParameterList}",
            };
        }

        private string ExtractStructuredTrivia(string key, Microsoft.CodeAnalysis.SyntaxNode triviaStructure)
        {
            var triviaMatch = triviaStructure.ChildNodes().OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.ToString() == key);
            if (triviaMatch != null && triviaMatch.Kind() != SyntaxKind.None && triviaMatch.Content.Any())
            {
                var v = triviaMatch.Content[0].ChildTokens().Where(ct => ct.Kind() != SyntaxKind.XmlTextLiteralNewLineToken)
                    .Select(ct => ct.ValueText.Trim())
                    ;
                return string.Join(" ", v).Trim();
            }

            return null;
        }
    }
}