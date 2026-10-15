// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Yarn.Shared;

using System.Collections.Generic;

#nullable enable

namespace Yarn.Analyser
{
    /// <summary>
    /// Responsible for reading the documentation trivia attached to C# methods and returning it in a form that can then be more easily managed by other parts of the analyser
    /// </summary>
    public static class DocstringExtractor
    {
        /// <summary>
        /// Attempts to read the docstrings from the target method and provide it as documentation payload for use in the YSLS generation.
        /// </summary>
        /// <param name="targetSymbol">The method whos docstrings we covet</param>
        /// <param name="logger">Logger only used during development, will most likely be null</param>
        /// <param name="documentation">the finalised form of the docstring</param>
        /// <returns>true if the docstring was able to be found and recorded</returns>
        public static bool TryCreateDocumentation(IMethodSymbol targetSymbol, ILogger? logger, out DocumentationPayload documentation)
        {
            if (TryGetDocumentation(targetSymbol, logger, out var xml, out var summary))
            {
                // ok need to get the return description here
                // and also the parameters
                // one step at a time though

                string? retvrn = null;
                var returnNode = xml?.Element("returns");
                if (returnNode != null)
                {
                    retvrn = string.Join("", returnNode.DescendantNodes().OfType<XText>().Select(n => n.ToString())).Trim();
                    logger?.WriteLine($"\tFound a return: {retvrn}");
                }

                Dictionary<string, string>? parameterDocumentation = null;
                if (xml != null)
                {
                    var parameterNodes = xml.Elements("param");
                    if (parameterNodes != null && parameterNodes.Count() > 0)
                    {
                        parameterDocumentation = new();
                        foreach (var parameterNode in parameterNodes)
                        {
                            var name = parameterNode.Attribute("name");
                            if (name == null) { continue; }
                            var text = string.Join("",parameterNode.DescendantNodes().OfType<XText>().Select(v => v.Value)).Trim();

                            if (!parameterDocumentation.ContainsKey(name.Value))
                            {
                                parameterDocumentation.Add(name.Value, text);
                            }
                        }
                    }
                }

                documentation = new(summary, retvrn, parameterDocumentation);
                return true;
            }
            documentation = new();
            return false;
        }

        /// <summary>
        /// Attempts to find and extract the XML docs from a method symbol
        /// </summary>
        /// <param name="targetSymbol">the method we want the docstrings from</param>
        /// <param name="logger">logger to be used during debugging, will almost always be null</param>
        /// <param name="documentationXML">the xml docstring</param>
        /// <param name="summary">the value from the summary tag, as this most of the time is all you actually care about.</param>
        /// <returns>True if it found the xml docstring</returns>
        public static bool TryGetDocumentation(IMethodSymbol targetSymbol, ILogger? logger, out XElement? documentationXML, out string? summary)
        {
            documentationXML = null;
            summary = null;

            var documentationComments = targetSymbol.GetDocumentationCommentXml();
            if (string.IsNullOrEmpty(documentationComments))
            {
                documentationComments = null;
                logger?.WriteLine($"Unable to find any xml documentation for {targetSymbol.Name}, attempting to load it syntactically instead.");

                foreach (var reference in targetSymbol.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is MethodDeclarationSyntax method)
                    {
                        var comment = GetActionTrivia(method, logger);
                        if (!string.IsNullOrEmpty(comment))
                        {
                            documentationComments = comment;
                            break;
                        }
                    }
                }
            }

            // at this point we still don't have a doc string
            // going to have to just give up
            if (string.IsNullOrWhiteSpace(documentationComments) || documentationComments == null)
            {
                logger?.WriteLine($"Unable to find any xml documentation for {targetSymbol.Name}, syntactically either.");
                return false;
            }
            logger?.WriteLine($"Found a potential documentation candidate:\"{documentationComments}\"");

            // there are three different situations:
            // 1. This is a correctly structured docs string that has come from GetDocumentationCommentXml
            if (TryGetXMLFromDocumentString(documentationComments, out documentationXML, logger))
            {
                var summaryNode = documentationXML?.Element("summary");
                if (summaryNode != null)
                {
                    summary = string.Join("", summaryNode.DescendantNodes().OfType<XText>().Select(n => n.ToString())).Trim();
                    logger?.WriteLine("Found the GetDocumentationCommentXml comments and parsed it successfully");

                    return true;
                }
            }

            // 2. This is a syntactically determined string that happens to also be XML, but it will be missing the synthesised member root
            // so we add the missing root node on and try again
            if (TryGetXMLFromDocumentString($"<member name=\"M:{targetSymbol.ToString()}\">{documentationComments}</member>", out documentationXML, logger))
            {
                // so we wrap this node and try again
                var summaryNode = documentationXML?.Element("summary");
                if (summaryNode != null)
                {
                    summary = string.Join("", summaryNode.DescendantNodes().OfType<XText>().Select(n => n.ToString())).Trim();
                    logger?.WriteLine("Found the unrooted XML comments and parsed it successfully");

                    return true;
                }
            }

            // 3. This is not doc XML and just happens to be a comment above a command/function
            summary = documentationComments;
            documentationXML = null;
            logger?.WriteLine("Unable to determine XML, returning the comment as is");
            return true;
        }

        private static bool TryGetXMLFromDocumentString(string comment, out XElement? element, ILogger? logger)
        {
            try
            {
                element = XElement.Parse(comment);
                return true;
            }
            catch (System.Xml.XmlException ex)
            {
                logger?.WriteLine("Failed to parse comments as XML");
                logger?.WriteException(ex);
                element = null;
                return false;
            }
        }

        private static string? GetActionTrivia(MethodDeclarationSyntax method, ILogger? logger)
        {
            // The main string to use as the function's documentation.
            if (method.HasLeadingTrivia)
            {
                var trivias = method.GetLeadingTrivia();
                var structuredTrivia = trivias.LastOrDefault(t => t.HasStructure);
                if (!structuredTrivia.IsKind(SyntaxKind.None))
                {
                    // The method contains structured trivia. Extract the
                    // documentation for it.
                    logger?.WriteLine($"trivia for {method.Identifier} is structured");
                    return GetDocumentationFromStructuredTrivia(structuredTrivia);
                }
                else
                {
                    // There isn't any structured trivia, but perhaps there's a
                    // comment above the method, which we can use as our
                    // documentation.
                    logger?.WriteLine($"trivia for {method.Identifier} is unstructured");
                    return GetDocumentationFromUnstructuredTrivia(trivias, logger);
                }
            }
            else
            {
                logger?.WriteLine($"{method.Identifier} has no trivia");
                return null;
            }
        }
        private static string GetDocumentationFromUnstructuredTrivia(SyntaxTriviaList trivias, ILogger? logger)
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
                        if (emptyLineFlag == true)
                        {
                            logger?.WriteLine("have hit two empty lines in a row, done collecting unstructured trivia");
                            doneWithTrivia = true;
                        }
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

            documentation = string.Join(" ", documentationParts);
            return documentation;
        }
        private static string? GetDocumentationFromStructuredTrivia(SyntaxTrivia structuredTrivia)
        {
            string documentation;
            var triviaStructure = structuredTrivia.GetStructure();
            if (triviaStructure == null)
            {
                return null;
            }

            string? ExtractStructuredTrivia(string tagName)
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
                    var v = triviaMatch
                        .Content[0]
                        .ChildTokens()
                        .Where(ct => !ct.IsKind(SyntaxKind.XmlTextLiteralNewLineToken))
                        .Select(ct => ct.ValueText.Trim());

                    return string.Join(" ", v).Trim();
                }

                return null;
            }

            var summary = ExtractStructuredTrivia("summary");
            var remarks = ExtractStructuredTrivia("remarks");

            documentation = summary ?? triviaStructure.ToString();

            if (remarks != null)
            {
                documentation += "\n\n" + remarks;
            }

            return documentation;
        }
    }
}