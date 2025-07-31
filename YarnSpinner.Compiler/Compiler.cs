﻿// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using TypeChecker;

    internal enum ContentIdentifierType
    {
        /// <summary>
        /// The content identifier is a <c>#line:</c> tag.
        /// </summary>
        Line,
        /// <summary>
        /// The content identifier is a <c>#shadow:</c> tag.
        /// </summary>
        Shadow,
    }

    /// <summary>
    /// Compiles Yarn code.
    /// </summary>
    public static class Compiler
    {
        /// <summary>
        /// The maximum amount of time the type solver will spend attempting to
        /// complete the type solution, in seconds.
        /// </summary>
        const int TypeSolverTimeLimit = 10;

        /// <summary>
        /// Compiles Yarn code, as specified by a compilation job.
        /// </summary>
        /// <param name="compilationJob">The compilation job to perform.</param>
        /// <returns>The results of the compilation.</returns>
        /// <seealso cref="CompilationJob"/>
        /// <seealso cref="CompilationResult"/>
        public static CompilationResult Compile(CompilationJob compilationJob)
        {
            // All variable declarations that we've encountered
            var declarations = new List<Declaration>();

            // All type definitions that we've encountered while parsing. We'll add to this list when we encounter user-defined types.
            var knownTypes = Types.AllBuiltinTypes.Cast<TypeBase>().ToList();

            // Add any imported types:
            if (compilationJob.TypeDeclarations != null)
            {
                knownTypes.AddRange(compilationJob.TypeDeclarations.OfType<TypeBase>());
            }

            if (compilationJob.Declarations != null)
            {
                declarations.AddRange(compilationJob.Declarations);
            }

            var diagnostics = new List<Diagnostic>();
            {
                // Get function declarations from the Standard Library
                (IEnumerable<Declaration> newDeclarations, IEnumerable<Diagnostic> declarationDiagnostics) = GetDeclarationsFromLibrary(new Dialogue.StandardLibrary());

                diagnostics.AddRange(declarationDiagnostics);

                // Add declarations, as long as they don't conflict with existing entries
                declarations.AddRange(
                    newDeclarations.Where(d => declarations.Any(other => other.Name == d.Name) == false)
                );
            }

            // Get function declarations from the library, if provided
            if (compilationJob.Library != null)
            {
                (IEnumerable<Declaration> newDeclarations, IEnumerable<Diagnostic> declarationDiagnostics) = GetDeclarationsFromLibrary(compilationJob.Library);

                // Add declarations, as long as they don't conflict with existing entries
                declarations.AddRange(
                    newDeclarations.Where(d => declarations.Any(other => other.Name == d.Name) == false)
                );

                diagnostics.AddRange(declarationDiagnostics);
            }

            var parsedFiles = new List<FileParseResult>();

            // First pass: parse all files, generate their syntax trees,
            // and figure out what variables they've declared
            var stringTableManager = new StringTableManager();

            foreach (var file in compilationJob.Inputs.OfType<CompilationJob.File>())
            {
                var parseResult = ParseSyntaxTree(file, ref diagnostics);
                parsedFiles.Add(parseResult);

                // ok now we will add in our lastline tags
                // we do this BEFORE we build our strings table otherwise the tags will get missed
                // this should probably be a flag instead of every time though
            }

            foreach (var parseTree in compilationJob.Inputs.OfType<FileParseResult>())
            {
                parsedFiles.Add(parseTree);
            }

            // Assign every node its title
            var allNodes = parsedFiles.SelectMany(r =>
            {
                var dialogue = r.Tree.Payload as YarnSpinnerParser.DialogueContext;
                if (dialogue == null)
                {
                    return Enumerable.Empty<(YarnSpinnerParser.NodeContext Node, FileParseResult File)>();
                }

                return dialogue.node().Select(n => (Node: n, File: r));
            });
            foreach (var node in allNodes)
            {
                var titleHeader = node.Node.title_header()?.FirstOrDefault();
                if (titleHeader != null)
                {
                    node.Node.NodeTitle = titleHeader.title?.Text;
                }
            }

            foreach (var parsedInput in parsedFiles)
            {
                var lastLineTagger = new LastLineBeforeOptionsVisitor();
                lastLineTagger.Visit(parsedInput.Tree);
                RegisterStrings(parsedInput.FileName, stringTableManager, parsedInput.Tree, ref diagnostics);
            }

            // Check to see if any lines that shadow another have a valid source
            // line, the same text as their source line
            foreach (var shadowLineContext in stringTableManager.LineContexts.Values.Where(v => v.ShadowLineID != null))
            {
                var shadowLineID = shadowLineContext.ShadowLineID!;

                if (shadowLineContext.LineID == null)
                {
                    // All lines have a unique line ID, including shadow lines -
                    // it's an error if we have a shadow ID but no line ID
                    throw new InvalidOperationException($"Internal error: line with shadow id {shadowLineID} did not have a line ID of its own");
                }

                var sourceFile = stringTableManager.StringTable[shadowLineContext.LineID].fileName;

                if (stringTableManager.LineContexts.TryGetValue(shadowLineID, out var sourceLineContext) == false)
                {
                    // No source line found
                    diagnostics.Add(new Diagnostic(
                        sourceFile, shadowLineContext, $"\"{shadowLineID}\" is not a known line ID."
                    ));
                    continue;
                }

                var sourceText = stringTableManager.StringTable[shadowLineID].text;
                var shadowText = stringTableManager.StringTable[shadowLineContext.LineID].text;

                if (sourceText == null)
                {
                    throw new InvalidOperationException($"Internal error: line with shadow id {shadowLineID} was referencing line {shadowLineID}, but that line's text is null");
                }

                var sourceContext = stringTableManager.LineContexts[shadowLineID];

                if (sourceContext.line_formatted_text().expression().Length > 0)
                {
                    // Lines must not have inline expressions
                    diagnostics.Add(new Diagnostic(
                        sourceFile, shadowLineContext, $"Shadow lines must not have expressions"
                    ));
                }

                if (sourceText.Equals(shadowText, StringComparison.CurrentCulture) == false)
                {
                    // Lines must be identical
                    diagnostics.Add(new Diagnostic(
                        sourceFile, shadowLineContext, $"Shadow lines must have the same text as their source lines"
                    ));
                }

                // The shadow line is valid. Strip the text from its StringInfo,
                // to reinforce to clients that the content should come from the
                // source line.
                StringInfo shadowLineTableEntry = stringTableManager.StringTable[shadowLineContext.LineID];
                shadowLineTableEntry.text = null;
                stringTableManager.StringTable[shadowLineContext.LineID] = shadowLineTableEntry;


            }

            // Ensure that all nodes names in this compilation are unique. Node
            // name uniqueness is important for several processes, so we do this
            // check here.
            AddErrorsForInvalidNodeNames(parsedFiles, ref diagnostics);

            // For nodes that have a 'when' clause (that is, they're in a node
            // group), make their node names unique and store which group
            // they're in.
            foreach (var file in parsedFiles)
            {
                var nodeGroupVisitor = new NodeGroupVisitor(file.Name);
                nodeGroupVisitor.Visit(file.Tree);
            }

            if (compilationJob.CompilationType == CompilationJob.Type.StringsOnly)
            {
                // Stop at this point
                return new CompilationResult
                {
                    Declarations = Array.Empty<Declaration>(),
                    ContainsImplicitStringTags = stringTableManager.ContainsImplicitStringTags,
                    Program = null,
                    StringTable = stringTableManager.StringTable,
                    Diagnostics = diagnostics,
                    ParseResults = parsedFiles,
                };
            }

            // Run the type checker on the files, and produce type variables and constraints.
            List<TypeChecker.TypeConstraint> typeConstraints = new List<TypeChecker.TypeConstraint>();

            var fileTags = new Dictionary<string, IEnumerable<string>>();

            // The mapping of type variables to concrete types (or to other type
            // variables)
            var typeSolution = new Substitution();

            // The collection of type constraints that we weren't able to solve.
            // (We'll resolve as many of them as we can after finishing type
            // checking, and turn the rest into error messages.)
            var failingConstraints = new HashSet<TypeConstraint>();

            var walker = new Antlr4.Runtime.Tree.ParseTreeWalker();
            foreach (var parsedFile in parsedFiles)
            {
                compilationJob.CancellationToken.ThrowIfCancellationRequested();

                var typeCheckerListener = new TypeCheckerListener(parsedFile.Name, parsedFile.Tokens, declarations, knownTypes, typeSolution, failingConstraints, compilationJob.CancellationToken);

                walker.Walk(typeCheckerListener, parsedFile.Tree);

                diagnostics.AddRange(typeCheckerListener.Diagnostics);
                typeConstraints.AddRange(typeCheckerListener.TypeEquations);
                fileTags.Add(parsedFile.Name, typeCheckerListener.FileTags);

                typeSolution = typeCheckerListener.TypeSolution;

                failingConstraints = new HashSet<TypeConstraint>(TypeCheckerListener.ApplySolution(typeSolution, failingConstraints));
            }

            if (failingConstraints.Count > 0)
            {
                // We have a number of constraints that we were unable to
                // resolve - either they failed to unify during solving, or they
                // were left unresolved at the end of type-checking all files.
                //
                // We'll make a list-ditch effort to resolve these failing
                // constraints by attempting to solve them one at a time - it's
                // possible that they're only 'failing' because they were in a
                // set of type constraints that couldn't be solved all at once,
                // and some may be resolvable on their own. (We may still have
                // an unrecoverable type error, but if we can eliminate
                // constraints from the set of possible failure causes, we'll
                // reduce the chance of a spurious error message.)

                var watchdog = System.Diagnostics.Stopwatch.StartNew();

                bool anySucceeded;
                do
                {
                    compilationJob.CancellationToken.ThrowIfCancellationRequested();
#if !DEBUG
                    if (watchdog.ElapsedMilliseconds > TypeSolverTimeLimit * 1000)
                    {
                        // We've taken too long to solve. Create error
                        // diagnostics for the affected expressions.
                        foreach (var constraint in failingConstraints)
                        {
                            diagnostics.Add(new Yarn.Compiler.Diagnostic(constraint.SourceFileName, constraint.SourceContext, $"Expression failed to resolve in a reasonable time ({TypeSolverTimeLimit}). Try simplifying this expression."));
                        }
                        break;
                    }
#endif

                    // Repeatedly attempt to solve each constraint individually.
                    // After each attempt, attempt to eliminate whatever
                    // constraints we can. Stop when we either have no more
                    // constraints to fix, or no constraints ended up resolving.
                    anySucceeded = false;

                    foreach (var constraint in failingConstraints)
                    {
                        anySucceeded |= Solver.TrySolve(new[] { constraint }, knownTypes, diagnostics, ref typeSolution);
                    }
                    failingConstraints = new HashSet<TypeConstraint>(TypeCheckerListener.ApplySolution(typeSolution, failingConstraints));
                } while (anySucceeded);

                // If we have any left, then we well and truly failed to resolve
                // the constraint, and we should produce diagnostics for them.
                foreach (var constraint in failingConstraints)
                {
                    foreach (var failureMessage in constraint.GetFailureMessages(typeSolution))
                    {
                        diagnostics.Add(new Yarn.Compiler.Diagnostic(constraint.SourceFileName, constraint.SourceRange, failureMessage));
                    }
                }
                watchdog.Stop();
            }

            // determining the nodes we need to track visits on
            // this needs to be done before we finish up with declarations
            // so that any tracking variables are included in the compiled declarations
            HashSet<string> trackingNodes = new HashSet<string>();
            HashSet<string> ignoringNodes = new HashSet<string>();
            foreach (var parsedFile in parsedFiles)
            {
                var thingy = new NodeTrackingVisitor(trackingNodes, ignoringNodes);
                thingy.Visit(parsedFile.Tree);
            }

            // removing all nodes we are told explicitly to not track
            trackingNodes.ExceptWith(ignoringNodes);

            var trackingDeclarations = new List<Declaration>();
            foreach (var node in trackingNodes)
            {
                trackingDeclarations.Add(Declaration.CreateVariable(Yarn.Library.GenerateUniqueVisitedVariableForNode(node), Types.Number, 0, $"The generated variable for tracking visits of node {node}"));
            }

            // adding the generated tracking variables into the declaration list
            // this way any future variable storage system will know about them
            // if we didn't do this later stages wouldn't be able to interface with them
            declarations.AddRange(trackingDeclarations);

            // Apply the type solution to all declarations.
            foreach (var decl in declarations)
            {
                compilationJob.CancellationToken.ThrowIfCancellationRequested();

                decl.Type = TypeChecker.ITypeExtensions.Substitute(decl.Type, typeSolution);

                // If this value is of an error type, don't attempt to create a
                // value for it.
                if (decl.Type == Types.Error)
                {
                    continue;
                }

                // If this was an implicit declaration, then we didn't have an
                // initial value to use. Instead, set its default value to one
                // provided by the type.
                if (decl.IsImplicit && decl.Type is TypeBase typeLiteral)
                {
                    decl.DefaultValue = typeLiteral.DefaultValue;
                }

                // If, after substituting for the solver's result, the type is
                // STILL a variable, then we weren't able to determine its type,
                // and its type becomes the error type.
                if (decl.Type is TypeChecker.TypeVariable)
                {
                    var suggestion = decl.Name.StartsWith("$") ? $" For example: <<declare {decl.Name} = (initial value) >>" : string.Empty;

                    diagnostics.Add(new Diagnostic(decl.SourceFileName, decl.Range, $"Can't determine type of {decl.Name} given its usage. Manually specify its type with a declare statement.{suggestion}"));

                    decl.Type = Types.Error;
                }
            }

            // We've solved for our variable declarations; we also need to apply
            // these solutions to the parse tree, so that we know the type of
            // each expression.
            foreach (var parsedFile in parsedFiles)
            {
                Stack<IParseTree> stack = new Stack<IParseTree>();

                if (!(parsedFile.Tree.Payload is IParseTree parseTree))
                {
                    throw new InvalidOperationException($"Internal error: expected {nameof(parsedFile.Tree.Payload)} to be {nameof(IParseTree)}");
                }

                stack.Push(parseTree);

                while (stack.Count > 0)
                {
                    var tree = stack.Pop();

                    bool nodeHasTypeError = false;
                    if (tree.Payload is ITypedContext typedContext)
                    {
                        typedContext.Type = TypeChecker.ITypeExtensions.Substitute(typedContext.Type, typeSolution);

                        if (typedContext.Type is TypeChecker.TypeVariable variable)
                        {
                            // This context's type failed to be resolved to a
                            // literal; we don't know the type of this context.
                            // Compile error!
                            if (typedContext is ParserRuleContext parserRuleContext)
                            {
                                diagnostics.Add(new Diagnostic(parsedFile.Name, parserRuleContext, $"Can't determine the type of this expression."));
                            }
                            else
                            {
                                // It's a typed context, but for some reason not
                                // a parser rule context? That's an internal
                                // error.
                                throw new InvalidOperationException($"Internal error: Expected parse tree node {typedContext} to be a {nameof(ParserRuleContext)}, but it was a {typedContext.GetType().FullName}");
                            }
                            nodeHasTypeError = true;
                        }
                    }

                    if (nodeHasTypeError)
                    {
                        // This node has a type error. It's likely that its
                        // children will, too, so don't bother walking further
                        // into the tree (to do so wouldn't create any helpful
                        // error messages.)
                        continue;
                    }

                    for (int i = 0; i < tree.ChildCount; i++)
                    {
                        // Push its children onto our stack to check those
                        stack.Push(tree.GetChild(i));
                    }
                }
            }

            // Now that we know for sure the types of every node in all parse trees, we can correctly determine the initial values for every <<declare>> statement.
            TypeCheckerListener.ResolveInitialValues(ref declarations, ref diagnostics);

            // Check to see if there are any set statements that attempt to
            // modify smart variables (which are readonly)
            AddErrorsForSettingReadonlyVariables(parsedFiles, declarations, diagnostics);

            // Check to see if we're permitted to use preview features. If not,
            // and preview features are used, then emit errors.
            foreach (var file in parsedFiles)
            {
                var previewFeatureChecker = new PreviewFeatureVisitor(file, compilationJob.LanguageVersion, diagnostics);
                previewFeatureChecker.Visit(file.Tree);

                var fileDecls = declarations.Where(d => d.SourceFileName == file.Name);
                previewFeatureChecker.AddDiagnosticsForDeclarations(fileDecls);
            }

            // Filter out any duplicate diagnostics
            diagnostics = diagnostics.Distinct().ToList();

            AddDiagnosticsForStyleIssues(parsedFiles, ref diagnostics);

            // adding in the warnings about empty nodes
            var empties = AddDiagnosticsForEmptyNodes(parsedFiles, ref diagnostics);

            // The user-defined types are all types that we know about, minus
            // all types that were pre-defined.
            var userDefinedTypes = knownTypes.Except(Types.AllBuiltinTypes).ToList();

            // All declarations must now have a concrete type. If they don't,
            // then we couldn't solve for their type, and can't continue.
            if (compilationJob.CompilationType == CompilationJob.Type.TypeCheck)
            {
                // Stop at this point
                return new CompilationResult
                {
                    Declarations = declarations,
                    ContainsImplicitStringTags = false,
                    Program = null,
                    StringTable = null,
                    FileTags = fileTags,
                    Diagnostics = diagnostics,
                    UserDefinedTypes = userDefinedTypes,
                    ParseResults = parsedFiles,
                };
            }

            var fileCompilationResults = new List<FileCompilationResult>();

            List<Node> compiledNodes = new List<Node>();
            List<NodeDebugInfo> nodeDebugInfos = new List<NodeDebugInfo>();

            if (diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                // We have errors, so we can't safely generate code.
            }
            else
            {
                // No errors! Go ahead and generate the code for all parsed
                // files.
                foreach (var parsedFile in parsedFiles)
                {
                    compilationJob.CancellationToken.ThrowIfCancellationRequested();
                    FileCompilationResult compilationResult = GenerateCode(parsedFile, declarations, compilationJob, trackingNodes, empties);

                    fileCompilationResults.Add(compilationResult);
                }

                compiledNodes.AddRange(fileCompilationResults.SelectMany(r => r.Nodes));
                nodeDebugInfos.AddRange(fileCompilationResults.SelectMany(r => r.DebugInfos));
                diagnostics.AddRange(fileCompilationResults.SelectMany(r => r.Diagnostics));

                // For each smart variable, generate a node that contains code that
                // evaluates the variable's expression.
                var smartVarCompiler = new SmartVariableCompiler(declarations.ToDictionary(d => d.Name, d => d));

                foreach (var decl in declarations.Where(d => d.IsInlineExpansion))
                {
                    // Compile the smart variable declaration and get back a
                    // node alongside its debugger info
                    var nodeCompilationResult = smartVarCompiler.Compile(decl);

                    compiledNodes.Add(nodeCompilationResult.Node);
                    nodeDebugInfos.Add(nodeCompilationResult.NodeDebugInfo);
                }

                // Now that we've code-generated every node, we'll find every
                // node that's in a node group, and create the 'hub' node for
                // that group. The 'hub' node adds a saliency candidate for each
                // node in the group, asks the VM to select the best one via the
                // saliency strategy, and jumps to the appropriate node.
                var nodeGroups = parsedFiles
                    // Get all nodes
                    .SelectMany(n => (n.Tree as YarnSpinnerParser.DialogueContext)?.node())
                    // Filter nodes that are in groups
                    .Where(n => n.NodeGroup != null)
                    // Group them by group name
                    .GroupBy(n => n.NodeGroup!);

                foreach (var group in nodeGroups)
                {

                    var codegen = new NodeGroupCompiler(
                        nodeGroupName: group.Key,
                        variableDeclarations: declarations.ToDictionary(d => d.Name, d => d),
                        nodeContexts: group,
                        compiledNodes: compiledNodes,
                        trackingNodes: trackingNodes
                        );

                    var generatedNodes = codegen.CompileNodeGroup();

                    foreach (var generatedNode in generatedNodes)
                    {
                        compiledNodes.Add(generatedNode.Node);
                        nodeDebugInfos.Add(generatedNode.NodeDebugInfo);
                    }
                }
            }


            var initialValues = new Dictionary<string, Operand>();

            // Last step: take every variable declaration we found in all of the
            // inputs, and create an initial value registration for it.
            foreach (var declaration in declarations)
            {
                // We only care about variable declarations here
                if (declaration.Type is FunctionType)
                {
                    continue;
                }

                // Inline-expanded declarations ('smart variables') don't have
                // an initial value stored, because they are computed at
                // runtime.
                if (declaration.IsInlineExpansion)
                {
                    continue;
                }

                if (declaration.Type == Types.Error)
                {
                    // This declaration has an error type; we will
                    // already have created an error message for this, so
                    // skip this one.
                    continue;
                }

                Operand value;

                if (declaration.DefaultValue == null)
                {
                    diagnostics.Add(new Diagnostic($"Variable declaration {declaration.Name} (type {declaration.Type?.Name ?? "undefined"}) has a null default value. This is not allowed."));
                    continue;
                }

                if (declaration.Type == Types.String)
                {
                    value = new Operand(Convert.ToString(declaration.DefaultValue));
                }
                else if (declaration.Type == Types.Number)
                {
                    value = new Operand(Convert.ToSingle(declaration.DefaultValue));
                }
                else if (declaration.Type == Types.Boolean)
                {
                    value = new Operand(Convert.ToBoolean(declaration.DefaultValue));
                }
                else if (declaration.Type is EnumType enumType)
                {
                    if (enumType.RawType == Types.Number)
                    {
                        value = new Operand(Convert.ToSingle(declaration.DefaultValue));
                    }
                    else if (enumType.RawType == Types.String)
                    {
                        value = new Operand(Convert.ToString(declaration.DefaultValue));
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException($"Cannot create an initial value for enum type {declaration.Type.Name}: invalid raw type {enumType.RawType.Name}");
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Cannot create an initial value for type {declaration?.Type?.Name ?? "(unknown)"}");
                }

                initialValues.Add(declaration.Name, value);

            }

            var program = new Program();
            program.Nodes.Add(compiledNodes.Where(n => n.Name != null).ToDictionary(n => n.Name, n => n));
            program.InitialValues.Add(initialValues);

            var projectDebugInfo = new ProjectDebugInfo
            {
                Nodes = nodeDebugInfos,
            };

            var finalResult = new CompilationResult
            {
                Program = program,
                StringTable = stringTableManager.StringTable,
                Declarations = declarations,
                Diagnostics = diagnostics.Distinct(),
                FileTags = fileTags,
                ContainsImplicitStringTags = stringTableManager.ContainsImplicitStringTags,
                ProjectDebugInfo = projectDebugInfo,
                UserDefinedTypes = userDefinedTypes,
                ParseResults = parsedFiles,
            };

            return finalResult;
        }

        private static void AddDiagnosticsForStyleIssues(List<FileParseResult> parsedFiles, ref List<Diagnostic> diagnostics)
        {
            foreach (var file in parsedFiles)
            {
                var styleIssuesVisitor = new StyleWarningsVisitor(file.Name, file.Tokens);
                styleIssuesVisitor.Visit(file.Tree);
                diagnostics.AddRange(styleIssuesVisitor.Diagnostics);
            }
        }

        private static void AddErrorsForSettingReadonlyVariables(List<FileParseResult> parsedFiles, IEnumerable<Declaration> declarations, List<Diagnostic> diagnostics)
        {
            var smartVariables = declarations.Where(d => d.IsInlineExpansion).ToDictionary(d => d.Name, d => d);

            foreach (var file in parsedFiles)
            {
                if (!(file.Tree.Payload is YarnSpinnerParser.DialogueContext dialogueContext))
                {
                    // The tree isn't a valid 'dialogue' context, likely due to
                    // a parse error. Skip it for these purposes - diagnostics
                    // for the parse error will have been generated error.
                    continue;
                }

                // Visit every 'set' statement in the parse tree.
                ParseTreeWalker.WalkTree<YarnSpinnerParser.Set_statementContext>(dialogueContext, (setStatement) =>
                {
                    if (setStatement.variable() != null && setStatement.variable().VAR_ID() != null)
                    {
                        var variableName = setStatement.variable().VAR_ID().GetText();
                        if (smartVariables.ContainsKey(variableName))
                        {
                            // This set statement is attempting to set a value
                            // to a smart variable. That's not allowed, because
                            // smart variables are read-only.
                            diagnostics.Add(new Diagnostic(
                                file.Name,
                                setStatement.variable(),
                                $"{variableName} cannot be modified (it's a smart variable and is always equal to " +
                                $"{smartVariables[variableName]?.InitialValueParserContext?.GetTextWithWhitespace() ?? "(unknown)"})"));
                        }
                    }
                });
            }
        }


        /// <summary>
        /// Gets the name of the boolean variable that stores whether the
        /// content identified by lineID has been seen by the player before.
        /// </summary>
        /// <param name="lineID">The line ID to generate a variable name
        /// for.</param>
        /// <returns>A variable name.</returns>
        internal static string GetContentViewedVariableName(string lineID)
        {
            return $"$Yarn.Internal.Once.{lineID}";
        }

        /// <summary>
        /// Checks every node name in <paramref name="parseResults"/>, and
        /// ensure that they're all unique and valid. If there are duplicates or
        /// invalid node names, create diagnostics for them.
        /// </summary>
        /// <param name="parseResults">A collection of file parse results to
        /// check.</param>
        /// <param name="diagnostics">A collection of diagnostics to add
        /// to.</param>
        private static void AddErrorsForInvalidNodeNames(List<FileParseResult> parseResults, ref List<Diagnostic> diagnostics)
        {
            // A regular expression used to detect illegal characters
            // in node titles.
            System.Text.RegularExpressions.Regex invalidTitleCharacters = new System.Text.RegularExpressions.Regex(@"[\[<>\]{}\|:\s#\$]");

            var allNodes = parseResults.SelectMany(r =>
            {
                var dialogue = r.Tree.Payload as YarnSpinnerParser.DialogueContext;
                if (dialogue == null)
                {
                    return Enumerable.Empty<(YarnSpinnerParser.NodeContext Node, FileParseResult File)>();
                }

                return dialogue.node().Select(n => (Node: n, File: r));
            });

            // Pair up every node with its name, and filter out any that don't
            // have a name
            var nodesWithNames = allNodes.Select(n =>
            {
                var titleHeader = n.Node.title_header()?.FirstOrDefault();
                string? title = n.Node.NodeTitle;
                if (titleHeader == null)
                {
                    return (
                        Name: null,
                        TitleHeader: null,
                        Node: n.Node,
                        File: n.File);
                }
                else
                {
                    return (
                        Name: title,
                        TitleHeader: titleHeader ?? null,
                        Node: n.Node,
                        File: n.File);
                }
            }).Where(kv => kv.Name != null);

            // Find nodes whose titles have invalid characters, and generate
            // diagnostics for them

            var nodesWithIllegalTitleCharacters = nodesWithNames.Where(n => invalidTitleCharacters.IsMatch(n.Name));

            foreach (var node in nodesWithIllegalTitleCharacters)
            {
                diagnostics.Add(new Diagnostic(node.File.Name, node.TitleHeader, $"The node '{node.Name}' contains illegal characters."));
            }

            var nodesByTitle = nodesWithNames.GroupBy(n => n.Name);

            // Find groups of nodes with the same name and generate diagnostics
            // for each
            foreach (var group in nodesByTitle)
            {
                if (group.Count() == 1)
                {
                    continue;
                }

                bool HasWhenHeader(YarnSpinnerParser.NodeContext nodeContext)
                {
                    return nodeContext.GetWhenHeaders().Any();
                }

                // If any of these nodes have 'when' clauses, then all nodes
                // must have them for the group to be valid. In this situation,
                // it's not an error for the nodes to share the same name,
                // because after this check is done, they will be renamed.

                if (group.Any(n => HasWhenHeader(n.Node)))
                {
                    if (!group.All(n => HasWhenHeader(n.Node)))
                    {
                        // Error - some nodes have a 'when' header, but others
                        // don't. Create errors for these others.
                        foreach (var entry in group.Where(n => n.Node.GetWhenHeaders().Any() == false))
                        {
                            var d = new Diagnostic(entry.File.Name, entry.TitleHeader, $"All nodes in the group '{entry.Node.NodeTitle}' must have a 'when' clause (use 'when: always' if you want this node to not have any conditions)");
                            diagnostics.Add(d);
                        }
                    }

                    // Else no error - all nodes that have this name have at least
                    // one 'when' header

                    // If subtitles will be used to disambiguate node names then check
                    // if those are unique
                    var nodesBySubtitle = group.GroupBy(n => n.Node.GetHeader("subtitle")?.header_value?.Text ?? null);

                    foreach (var subgroup in nodesBySubtitle)
                    {
                        if (subgroup.Key == null)
                        {
                            continue;
                        }

                        var subtitle = subgroup.Key;

                        // multiple nulls are allowed but multiple of a value are not
                        if (subgroup.Count() > 1)
                        {
                            foreach (var entry in group)
                            {
                                var d = new Diagnostic(entry.File.Name, entry.Node.GetHeader("subtitle"), $"More than one node in group {entry.Name} has subtitle {subtitle}");
                                diagnostics.Add(d);
                            }
                        }
                        if (invalidTitleCharacters.IsMatch(subtitle))
                        {
                            foreach (var entry in group)
                            {
                                diagnostics.Add(new Diagnostic(entry.File.Name, entry.Node.GetHeader("subtitle"), $"The node subtitle '{subtitle}' contains illegal characters."));
                            }
                        }
                    }

                    continue;
                }

                // More than one node has this name! Report an error on both.
                foreach (var entry in group)
                {
                    var d = new Diagnostic(entry.File.Name, entry.TitleHeader, $"More than one node is named {entry.Name}");
                    diagnostics.Add(d);
                }
            }

            // Find nodes that have either zero title headers, or more than one title header
            foreach (var node in allNodes)
            {
                if (node.Node.NodeTitle == null)
                {
                    diagnostics.Add(new Diagnostic(node.File.Name, node.Node.body(), $"Nodes must have a title"));
                }
                if (node.Node.title_header().Length > 1)
                {
                    diagnostics.Add(new Diagnostic(node.File.Name, node.Node.title_header()[1], $"Nodes must have a single title node"));
                }
            }
        }

        private static HashSet<string> AddDiagnosticsForEmptyNodes(List<FileParseResult> parseResults, ref List<Diagnostic> diagnostics)
        {
            HashSet<string> emptyNodes = new HashSet<string>();
            var empties = parseResults.SelectMany(r =>
            {
                var dialogue = r.Tree.Payload as YarnSpinnerParser.DialogueContext;
                if (dialogue == null)
                {
                    return Enumerable.Empty<(YarnSpinnerParser.NodeContext Node, FileParseResult File)>();
                }

                return dialogue.node().Select(n => (Node: n, File: r));
            }).Where(n => n.Node.body() != null && n.Node.body().statement().Length == 0);

            foreach (var entry in empties)
            {
                var title = entry.Node.NodeTitle;
                var d = new Diagnostic(entry.File.Name, entry.Node, $"Node \"{title ?? "(missing title)"}\" is empty and will not be included in the compiled output.", Diagnostic.DiagnosticSeverity.Warning);
                diagnostics.Add(d);
                if (title != null)
                {
                    emptyNodes.Add(title);
                }
            }
            return emptyNodes;
        }

        private static void RegisterStrings(string fileName, StringTableManager stringTableManager, IParseTree tree, ref List<Diagnostic> diagnostics)
        {
            var visitor = new StringTableGeneratorVisitor(fileName, stringTableManager);
            visitor.Visit(tree);
            diagnostics.AddRange(visitor.Diagnostics);
        }

        private static FileCompilationResult GenerateCode(FileParseResult fileParseResult, IEnumerable<Declaration> variableDeclarations, CompilationJob job, HashSet<string> trackingNodes, HashSet<string> nodesToSkip)
        {

            FileCompiler compiler = new FileCompiler(new FileCompiler.CompilationContext
            {
                FileParseResult = fileParseResult,
                TrackingNodes = trackingNodes,
                Library = job.Library,
                NodesToSkip = nodesToSkip,
                VariableDeclarations = variableDeclarations
                .Where(d => d.Type is FunctionType == false)
                .ToDictionary(d => d.Name, d => d),
            });


            return compiler.Compile();
        }

        /// <summary>
        /// Returns a collection of <see cref="Declaration"/> structs that
        /// describe the functions present in <paramref name="library"/>.
        /// </summary>
        /// <param name="library">The <see cref="Library"/> to get declarations
        /// from.</param>
        /// <returns>The <see cref="Declaration"/> structs found.</returns>
        internal static (IEnumerable<Declaration>, IEnumerable<Diagnostic>) GetDeclarationsFromLibrary(Library library)
        {
            var declarations = new List<Declaration>();

            var diagnostics = new List<Diagnostic>();

            foreach (var function in library.Delegates)
            {
                var method = function.Value.Method;

                if (method.ReturnType == typeof(Value))
                {
                    // Functions that return the internal type Values are
                    // operators, and are type checked by
                    // ExpressionTypeVisitor. (Future work: define each
                    // polymorph of each operator as a separate function
                    // that returns a concrete type, rather than the
                    // current method of having a 'Value' wrapper type).
                    continue;
                }

                // Does the return type of this delegate map to a value
                // that Yarn Spinner can use?
                if (Types.TypeMappings.TryGetValue(method.ReturnType, out var yarnReturnType) == false)
                {
                    diagnostics.Add(new Diagnostic($"Function {function.Key} cannot be used in Yarn Spinner scripts: {method.ReturnType} is not a valid return type."));
                    continue;
                }

                // Define a new type for this function
                var functionType = new FunctionType(Types.Any);

                var includeMethod = true;

                System.Reflection.ParameterInfo[] array = method.GetParameters();
                for (int i = 0; i < array.Length; i++)
                {
                    var isLast = i == array.Length - 1;

                    System.Reflection.ParameterInfo? paramInfo = array[i];
                    if (paramInfo.ParameterType == typeof(Value))
                    {
                        // Don't type-check this method - it's an operator
                        includeMethod = false;
                        break;
                    }

                    if (paramInfo.IsOptional)
                    {
                        diagnostics.Add(new Diagnostic($"Function {function.Key} cannot be used in Yarn Spinner scripts: parameter {paramInfo.Name} is optional, which isn't supported."));
                        continue;
                    }

                    if (paramInfo.IsOut)
                    {
                        diagnostics.Add(new Diagnostic($"Function {function.Key} cannot be used in Yarn Spinner scripts: parameter {paramInfo.Name} is an out parameter, which isn't supported."));
                        continue;
                    }

                    var isLastParamArray = isLast
                        && paramInfo.ParameterType.IsArray;
                    // Normally, we'd check for the presence of a
                    // System.ParamArrayAttribute, but C# doesn't generate
                    // them for local functions. Instead, assume that if the
                    // last parameter is an array of a valid type, it's a
                    // params array.

                    if (isLastParamArray)
                    {
                        // This is a params array. Is the type of the array valid?
                        if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType.GetElementType(), out var yarnParamsElementType))
                        {
                            functionType.VariadicParameterType = yarnParamsElementType;
                        }
                        else
                        {
                            diagnostics.Add(new Diagnostic($"Function {function.Key} cannot be used in Yarn Spinner scripts: params array {paramInfo.Name}'s type ({paramInfo.ParameterType}) cannot be used in Yarn functions"));
                        }
                    }
                    else if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType, out var yarnParameterType) == false)
                    {
                        diagnostics.Add(new Diagnostic($"Function {function.Key} cannot be used in Yarn Spinner scripts: parameter {paramInfo.Name}'s type ({paramInfo.ParameterType}) cannot be used in Yarn functions"));
                    }
                    else
                    {
                        functionType.AddParameter(yarnParameterType);
                    }

                }

                if (includeMethod == false)
                {
                    continue;
                }

                functionType.ReturnType = yarnReturnType;

                var declaration = new Declaration
                {
                    Name = function.Key,
                    Type = functionType,
                    Range = { },
                    SourceFileName = Declaration.ExternalDeclaration,
                    SourceNodeName = null,
                };

                declarations.Add(declaration);
            }

            return (declarations, diagnostics);
        }

        private static FileParseResult ParseSyntaxTree(CompilationJob.File file, ref List<Diagnostic> diagnostics)
        {
            string source = file.Source;
            string fileName = file.FileName;

            return ParseSyntaxTree(fileName, source, ref diagnostics);
        }

        internal static FileParseResult ParseSyntaxTree(string fileName, string source, ref List<Diagnostic> diagnostics)
        {
            ICharStream input = CharStreams.fromString(source);

            YarnSpinnerLexer lexer = new YarnSpinnerLexer(input);
            CommonTokenStream tokens = new CommonTokenStream(lexer);

            YarnSpinnerParser parser = new YarnSpinnerParser(tokens);

            // turning off the normal error listener and using ours
            var parserErrorListener = new ParserErrorListener(fileName);
            var lexerErrorListener = new LexerErrorListener(fileName);

            parser.ErrorHandler = new ErrorStrategy();

            parser.RemoveErrorListeners();
            parser.AddErrorListener(parserErrorListener);

            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexerErrorListener);

            YarnSpinnerParser.DialogueContext tree;

            tree = parser.dialogue();

            var newDiagnostics = lexerErrorListener.Diagnostics.Concat(parserErrorListener.Diagnostics);

            diagnostics.AddRange(newDiagnostics);

            // Now that we've parsed the file, we'll go through all of the nodes
            // and record which file they came from.
            foreach (var node in tree.node())
            {
                node.SourceFileName = fileName;
            }

            return new FileParseResult(fileName, tree, tokens);
        }

        /// <summary>
        /// Reads the contents of a text file containing source code, and
        /// returns a list of tokens found in that source code.
        /// </summary>
        /// <param name="path">The path of the file to load the source code
        /// from.</param>
        /// <inheritdoc cref="GetTokensFromString(string)" path="/returns"/>
        internal static List<string> GetTokensFromFile(string path)
        {
            var text = File.ReadAllText(path);
            return GetTokensFromString(text);
        }

        /// <summary>
        /// Reads a string containing source code, and returns a list of
        /// tokens found in that source code.
        /// </summary>
        /// <param name="text">The source code to extract tokens
        /// from.</param>
        /// <returns>The list of tokens extracted from the source
        /// code.</returns>
        internal static List<string> GetTokensFromString(string text)
        {
            ICharStream input = CharStreams.fromString(text);

            YarnSpinnerLexer lexer = new YarnSpinnerLexer(input);

            var tokenStringList = new List<string>();

            var tokens = lexer.GetAllTokens();
            foreach (var token in tokens)
            {
                tokenStringList.Add($"{token.Line}:{token.Column} {YarnSpinnerLexer.DefaultVocabulary.GetDisplayName(token.Type)} \"{token.Text}\"");
            }

            return tokenStringList;
        }

        /// <summary>
        /// Finds all header parse contexts in the given node with the given key.
        /// </summary>
        /// <param name="nodeContext">The node context to search.</param>
        /// <param name="name">The key to search for</param>
        /// <returns>A collection of header contexts.</returns>
        internal static IEnumerable<YarnSpinnerParser.HeaderContext> GetHeadersWithKey(YarnSpinnerParser.NodeContext nodeContext, string name)
        {
            return nodeContext.header().Where(h => h.header_key?.Text == name);
        }


        /// <summary>
        /// Creates a new instruction, and appends it to a node in the <see
        /// cref="Program" />.
        /// </summary>
        /// <param name="node">The node to append instructions to.</param>
        /// <param name="debugInfo">The <see cref="NodeDebugInfo"/> object to add
        /// line debugging information to.</param>
        /// <param name="range">The range of source code corresponding to this instruction.</param>
        /// <param name="instruction">The instruction to add.</param>
        internal static void Emit(Node node, NodeDebugInfo debugInfo, Range range, Instruction instruction)
        {
            debugInfo.LineRanges.Add(node.Instructions.Count, range);

            node.Instructions.Add(instruction);
        }

        /// <summary>
        /// Extracts a line ID from a collection of <see
        /// cref="YarnSpinnerParser.HashtagContext"/>s, if one exists.
        /// </summary>
        /// <param name="type">The type of content identifier tag to
        /// retrieve.</param>
        /// <param name="hashtagContexts">The hashtag parsing contexts.</param>
        /// <returns>The line ID if one is present in the hashtag contexts,
        /// otherwise <c>null</c>.</returns>
        internal static YarnSpinnerParser.HashtagContext? GetContentIDTag(ContentIdentifierType type, YarnSpinnerParser.HashtagContext[] hashtagContexts)
        {
            // if there are any hashtags
            if (hashtagContexts != null)
            {
                foreach (var hashtagContext in hashtagContexts)
                {
                    string tagText = hashtagContext.text.Text;
                    if (type == ContentIdentifierType.Line && tagText.StartsWith("line:", StringComparison.InvariantCulture))
                    {
                        return hashtagContext;
                    }
                    if (type == ContentIdentifierType.Shadow && tagText.StartsWith("shadow:", StringComparison.InvariantCulture))
                    {
                        return hashtagContext;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a string containing the line ID from a line statement parser
        /// context.
        /// </summary>
        /// <remarks>
        /// The line ID is extracted from the <c>#line:</c> hashtag found on the
        /// line. If it isn't present, then this method throws an exception.
        /// </remarks>
        /// <param name="type">The type of content identifier to retrieve.</param>
        /// <param name="line">The line statement to extract the line ID
        /// from.</param>
        /// <returns>The line ID found in this line.</returns>
        /// <exception cref="ArgumentException">Thrown when the line does not
        /// have a line ID.</exception>
        internal static string GetContentID(ContentIdentifierType type, YarnSpinnerParser.Line_statementContext line)
        {
            return line.LineID ??
                throw new ArgumentException($"Internal error: line does not have a {type} ID");
        }

        /// <summary>
        /// Generates a line id for a raw text node
        /// </summary>
        /// <remarks>
        /// This should only be used when in raw text mode.
        /// </remarks>
        /// <param name="name">The name of the node</param>
        /// <returns>line id for the raw text node</returns>
        public static string GetLineIDForNodeName(string name)
        {
            return "line:" + name;
        }

        /// <summary>
        /// Flattens a tree of <see cref="IParseTree"/> objects by
        /// recursively visiting their children, and converting them into a
        /// flat <see cref="IEnumerable{IParseTree}"/>.
        /// </summary>
        /// <param name="node">The root node to begin work from.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> that contains a
        /// flattened version of the hierarchy rooted at <paramref
        /// name="node"/>.</returns>
        public static IEnumerable<IParseTree> FlattenParseTree(IParseTree node)
        {
            // Get the list of children in this node
            var children = Enumerable
                .Range(0, node.ChildCount)
                .Select(i => node.GetChild(i));

            // Recursively visit each child and append it to a sequence,
            // and then return that sequence
            return children
                .SelectMany(c => FlattenParseTree(c))
                .Concat(new[] { node });
        }

        /// <summary>
        /// Gets the text of the documentation comments that either immediately
        /// precede <paramref name="context"/>, or are on the same line as
        /// <paramref name="context"/>.
        /// </summary>
        /// <remarks>
        /// Documentation comments begin with a triple-slash (<c>///</c>), and
        /// are used to describe variable declarations. If documentation
        /// comments precede a declaration (that is, they're not on the same
        /// line as the declaration), then they may span multiple lines, as long
        /// as each line begins with a triple-slash.
        /// </remarks>
        /// <param name="tokens">The token stream to search.</param>
        /// <param name="context">The parser rule context to get documentation
        /// comments for.</param>
        /// <param name="allowCommentsAfter">If true, this method will search
        /// for documentation comments that come after <paramref
        /// name="context"/>'s last token and are on the same line.</param>
        /// <returns>The text of the documentation comments, or <see
        /// langword="null"/> if no documentation comments were
        /// present.</returns>
        public static string? GetDocumentComments(CommonTokenStream tokens, ParserRuleContext context, bool allowCommentsAfter = true)
        {
            string? description = null;

            var precedingComments = tokens.GetHiddenTokensToLeft(context.Start.TokenIndex, YarnSpinnerLexer.COMMENTS);

            if (precedingComments != null)
            {
                var precedingDocComments = precedingComments
                    // There are no tokens on the main channel with this
                    // one on the same line
                    .Where(t => tokens.GetTokens()
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

            if (allowCommentsAfter)
            {
                var subsequentComments = tokens.GetHiddenTokensToRight(context.Stop.TokenIndex, YarnSpinnerLexer.COMMENTS);
                if (subsequentComments != null)
                {
                    var subsequentDocComment = subsequentComments
                        // This comment is on the same line as the end of
                        // the declaration
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
            }

            return description;
        }

    }

    /// <summary>
    /// Adds additional functionality to the <see cref="YarnSpinnerParser"/>
    /// class.
    /// </summary>
    public partial class YarnSpinnerParser
    {
        /// <summary>
        /// Adds additional functionality to the <see cref="NodeContext"/>
        /// class.
        /// </summary>
        public partial class NodeContext
        {
            /// <summary>
            /// Gets the path to the file that this node was parsed from, if any.
            /// </summary>
            public string? SourceFileName { get; set; }

            /// <summary>
            /// Gets the title of this node, as specified in its '<c>title</c>'
            /// header. If it is not present, returns <see langword="null"/>. If
            /// more than one is present, the first is returned.
            /// </summary>
            public string? NodeTitle { get; set; }

            /// <summary>
            /// Gets the name of the node group that this node is part of, if
            /// any.
            /// </summary>
            public string? NodeGroup { get; set; }

            /// <summary>
            /// Gets the first header in this node named <paramref name="key"/>.
            /// </summary>
            /// <param name="key">The key of the header to find.</param>
            /// <returns>A header whose <see cref="HeaderContext.header_key"/>
            /// is <paramref name="key"/>. </returns>
            /// <remarks>
            /// This method returns only the first header with the specified
            /// key. To fetch all headers with this key, use <see
            /// cref="GetHeaders"/>.
            /// </remarks>
            public HeaderContext? GetHeader(string key)
            {
                return this.header()?
                    .FirstOrDefault(h =>
                        h.header_key?.Text.Equals(key, StringComparison.InvariantCultureIgnoreCase) ?? false
                        && h.header_value != null
                );
            }

            internal IEnumerable<When_headerContext> GetWhenHeaders()
            {
                return this.when_header();
            }

            /// <summary>
            /// Gets all headers in this node named <paramref name="key"/>.
            /// </summary>
            /// <param name="key">The key of the header to find.</param>
            /// <returns>A collection of headers whose <see cref="HeaderContext.header_key"/>
            /// is <paramref name="key"/>. </returns>
            internal IEnumerable<HeaderContext> GetHeaders(string? key = null)
            {
                if (this.header() == null)
                {
                    return Enumerable.Empty<HeaderContext>();
                }

                if (key == null)
                {
                    return this.header();
                }

                return this.header()
                    .Where(h =>
                        h.header_key?.Text.Equals(key, StringComparison.InvariantCultureIgnoreCase) ?? false
                        && h.header_value != null
                );
            }
        }

        /// <summary>
        /// Gets the total number of binary boolean operations - ands, ors, and
        /// xors - present in an expression and its sub-expressions.
        /// </summary>
        /// <param name="context">An expression.</param>
        /// <returns>The total number of binary boolean operations in the
        /// expression.</returns>
        private static int GetBooleanOperatorCountInExpression(ParserRuleContext context)
        {
            var subtreeCount = 0;

            if (context is ExpAndOrXorContext)
            {
                // This expression is a binary boolean expression.
                subtreeCount += 1;
            }

            foreach (var child in context.children)
            {
                if (child is ParserRuleContext childContext)
                {
                    subtreeCount += GetBooleanOperatorCountInExpression(childContext);
                }
            }

            return subtreeCount;
        }

        /// <inheritdoc/>
        public partial class Line_conditionContext
        {
            /// <summary>
            /// Gets the complexity of this line's condition.
            /// </summary>
            internal int ConditionCount
            {
                get
                {
                    var count = 0;
                    ExpressionContext? expression;
                    if (this is LineOnceConditionContext once)
                    {
                        count += 1;
                        expression = once.expression();
                    }
                    else if (this is LineConditionContext condition)
                    {
                        expression = condition.expression();
                    }
                    else
                    {
                        throw new InvalidOperationException("Internal error: line condition class was of an invalid type");
                    }
                    if (expression != null)
                    {
                        count += GetBooleanOperatorCountInExpression(expression) + 1;
                    }
                    return count;
                }
            }
        }

        public partial class NodeContext
        {
            /// <summary>
            /// Gets the computed total complexity of this node's 'when'
            /// conditions.
            /// </summary>
            /// <remarks>
            /// If this node has no such conditions, this value is -1.
            /// </remarks>
            public int ComplexityScore
            {
                get
                {
                    var headers = this.GetWhenHeaders();
                    if (headers == null || !headers.Any())
                    {
                        return -1;
                    }

                    return headers.Sum(h => h.ComplexityScore);
                }
            }
        }

        /// <inheritdoc/>
        public partial class When_headerContext
        {
            internal bool IsOnce => this.header_expression.once != null;
            internal bool IsAlways => this.header_expression.always != null;

            /// <summary>
            /// Gets the complexity of this line's condition.
            /// </summary>
            internal int ComplexityScore
            {
                get
                {
                    if (IsAlways)
                    {
                        // This header is a 'when: always' header - it has a complexity of 0
                        return 0;
                    }

                    var count = 0;

                    if (IsOnce)
                    {
                        count += 1;
                    }

                    if (this.header_expression.expression() != null)
                    {
                        count += GetBooleanOperatorCountInExpression(this.header_expression.expression()) + 1;
                    }

                    return count;
                }
            }
        }
    }
}
;
