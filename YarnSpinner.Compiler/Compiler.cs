// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using static Yarn.Instruction.Types;

    /// <summary>
    /// Compiles Yarn code.
    /// </summary>
    public static class Compiler
    {
       

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

            if (compilationJob.VariableDeclarations != null)
            {
                declarations.AddRange(compilationJob.VariableDeclarations);
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

            foreach (var file in compilationJob.Files)
            {
                var parseResult = ParseSyntaxTree(file, ref diagnostics);
                parsedFiles.Add(parseResult);

                // ok now we will add in our lastline tags
                // we do this BEFORE we build our strings table otherwise the tags will get missed
                // this should probably be a flag instead of every time though
                var lastLineTagger = new LastLineBeforeOptionsVisitor();
                lastLineTagger.Visit(parseResult.Tree);

                RegisterStrings(file.FileName, stringTableManager, parseResult.Tree, ref diagnostics);
            }

            // Ensure that all nodes names in this compilation are unique. Node
            // name uniqueness is important for several processes, so we do this
            // check here.
            AddErrorsForInvalidNodeNames(parsedFiles, ref diagnostics);

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
                };
            }

            // Go through all files and generate any necessary variables for
            // their #once hashtags, and rewrite any once() functions to include
            // the unique parameter.
            foreach (var parsedFile in parsedFiles) {
                var onceTagger = new ViewOnceVisitor(parsedFile.Name, declarations, diagnostics);
                onceTagger.Visit(parsedFile.Tree);
            }

            // Run the type checker on the files, and produce type variables and constraints.

            List<TypeChecker.TypeConstraint> typeConstraints = new List<TypeChecker.TypeConstraint>();

            var fileTags = new Dictionary<string, IEnumerable<string>>();

            var walker = new Antlr4.Runtime.Tree.ParseTreeWalker();
            foreach (var parsedFile in parsedFiles)
            {
                var typeCheckerListener = new TypeCheckerListener(parsedFile.Name, parsedFile.Tokens, ref declarations, ref knownTypes);

                walker.Walk(typeCheckerListener, parsedFile.Tree);

                diagnostics.AddRange(typeCheckerListener.Diagnostics);
                typeConstraints.AddRange(typeCheckerListener.TypeEquations);
                fileTags.Add(parsedFile.Name, typeCheckerListener.FileTags);
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

            // We now have declarations for variables in the program, which are
            // all type variables. We also have a number of type equations that
            // constrain those variables. 

            // Solve all type equations, producing a Substitution.

            var substitution = TypeChecker.Solver.Solve(typeConstraints, knownTypes.OfType<TypeBase>(), ref diagnostics);

            // Apply this substitution to all declarations.
            foreach (var decl in declarations)
            {
                decl.Type = TypeChecker.ITypeExtensions.Substitute(decl.Type, substitution);

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
                
                if (!(parsedFile.Tree.Payload is IParseTree parseTree)) {
                    throw new InvalidOperationException($"Internal error: expected {nameof(parsedFile.Tree.Payload)} to be {nameof(IParseTree)}");
                }

                stack.Push(parseTree);

                while (stack.Count > 0)
                {
                    var tree = stack.Pop();
                    if (tree.Payload is ITypedContext typedContext)
                    {
                        typedContext.Type = TypeChecker.ITypeExtensions.Substitute(typedContext.Type, substitution);

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
                        }
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
            foreach (var file in parsedFiles) {
                var previewFeatureChecker = new PreviewFeatureVisitor(file, !compilationJob.AllowPreviewFeatures, diagnostics);
                previewFeatureChecker.Visit(file.Tree);

                var fileDecls = declarations.Where(d => d.SourceFileName == file.Name);
                previewFeatureChecker.AddDiagnosticsForDeclarations(fileDecls);
            }

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
                    FileCompilationResult compilationResult = GenerateCode(parsedFile, declarations, compilationJob, stringTableManager, trackingNodes);

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
                    smartVarCompiler.Compile(decl, out var node, out var debugInfo);

                    compiledNodes.Add(node);
                    nodeDebugInfos.Add(debugInfo);
                }
            }

            
            var initialValues = new Dictionary<string, Operand>();

            // Last step: take every variable declaration we found in all
            // of the inputs, and create an initial value registration for
            // it.
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
                    value = new Operand(Convert.ToSingle(declaration.DefaultValue));
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
            };
            
            return finalResult;
        }

        private static void AddErrorsForSettingReadonlyVariables(List<FileParseResult> parsedFiles, IEnumerable<Declaration> declarations, List<Diagnostic> diagnostics)
        {
            var smartVariables = declarations.Where(d => d.IsInlineExpansion).ToDictionary(d => d.Name, d => d);

            foreach (var file in parsedFiles)
            {
                var dialogueContext = file.Tree.Payload as YarnSpinnerParser.DialogueContext;

                // Visit every 'set' statement in the parse tree.
                ParseTreeWalker.WalkTree<YarnSpinnerParser.Set_statementContext>(dialogueContext, (setStatement) =>
                {
                    if (setStatement.variable() != null && setStatement.variable().VAR_ID() != null)
                    {
                        var variableName = setStatement.variable().VAR_ID().GetText();
                        if (smartVariables.ContainsKey(variableName))
                        {
                            // This set statement is attempting to set a value to a
                            // smart variable. That's not allowed, because smart
                            // variables are read-only.
                            diagnostics.Add(new Diagnostic(file.Name, setStatement.variable(), $"{variableName} cannot be modified (it's a smart variable and is always equal to {smartVariables[variableName]?.InitialValueParserContext.GetTextWithWhitespace()})"));
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
            /// A regular expression used to detect illegal characters
            /// in node titles.
            Regex invalidTitleCharacters = new System.Text.RegularExpressions.Regex(@"[\[<>\]{}\|:\s#\$]");

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
                var titleHeader = GetHeadersWithKey(n.Node, "title").FirstOrDefault();
                if (titleHeader == null)
                {
                    return (
                        Name: null,
                        Header: null,
                        Node: n.Node,
                        File: n.File);
                }
                else
                {
                    return (
                        Name: titleHeader.header_value.Text,
                        Header: titleHeader,
                        Node: n.Node,
                        File: n.File);
                }
            }).Where(kv => kv.Name != null);

            // Find nodes whose titles have invalid characters, and generate
            // diagnostics for them
            var nodesWithIllegalTitleCharacters = nodesWithNames.Where(n => invalidTitleCharacters.IsMatch(n.Name));

            foreach (var node in nodesWithIllegalTitleCharacters)
            {
                diagnostics.Add(new Diagnostic(node.File.Name, node.Header, $"The node '{node.Name}' contains illegal characters."));
            }

            var nodesByName = nodesWithNames.GroupBy(n => n.Name);

            // Find groups of nodes with the same name and generate diagnostics
            // for each
            foreach (var group in nodesByName)
            {
                if (group.Count() == 1)
                {
                    continue;
                }

                // More than one node has this name! Report an error on both.
                foreach (var entry in group)
                {
                    var d = new Diagnostic(entry.File.Name, entry.Header, $"More than one node is named {entry.Name}");
                    diagnostics.Add(d);
                }
            }
        }

        private static void RegisterStrings(string fileName, StringTableManager stringTableManager, IParseTree tree, ref List<Diagnostic> diagnostics)
        {
            var visitor = new StringTableGeneratorVisitor(fileName, stringTableManager);
            visitor.Visit(tree);
            diagnostics.AddRange(visitor.Diagnostics);
        }

        private static FileCompilationResult GenerateCode(FileParseResult fileParseResult, IEnumerable<Declaration> variableDeclarations, CompilationJob job, StringTableManager stringTableManager, HashSet<string> trackingNodes)
        {

            FileCompiler compiler = new FileCompiler(new FileCompiler.CompilationContext
            {
                FileParseResult = fileParseResult,
                TrackingNodes = trackingNodes,
                Library = job.Library,
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

                foreach (var paramInfo in method.GetParameters())
                {
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

                    if (Types.TypeMappings.TryGetValue(paramInfo.ParameterType, out var yarnParameterType) == false)
                    {
                        diagnostics.Add(new Diagnostic($"Function {function.Key} cannot be used in Yarn Spinner scripts: parameter {paramInfo.Name}'s type ({paramInfo.ParameterType}) cannot be used in Yarn functions"));
                        continue;
                    }

                    functionType.AddParameter(yarnParameterType);
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
            ICharStream input = CharStreams.fromstring(source);

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

            IParseTree tree;

            tree = parser.dialogue();

            var newDiagnostics = lexerErrorListener.Diagnostics.Concat(parserErrorListener.Diagnostics);

            diagnostics.AddRange(newDiagnostics);

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
            ICharStream input = CharStreams.fromstring(text);

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
        /// <param name="sourceLine">The zero-indexed line in the source input
        /// corresponding to this instruction.</param>
        /// <param name="sourceCharacter">The zero-indexed character in the
        /// source input corresponding to this instruction.</param>
        /// <param name="code">The opcode of the instruction.</param>
        /// <param name="operands">The operands to associate with the
        /// instruction.</param>
        internal static void Emit(Node node, NodeDebugInfo debugInfo, int sourceLine, int sourceCharacter, OpCode code, params Operand[] operands)
        {
            var instruction = new Instruction
            {
                Opcode = code,
            };

            instruction.Operands.Add(operands);

            debugInfo.LinePositions.Add(node.Instructions.Count, new Position {
                Line = sourceLine,
                Character = sourceCharacter,
            });

            node.Instructions.Add(instruction);
        }

        /// <summary>
        /// Extracts a line ID from a collection of <see
        /// cref="YarnSpinnerParser.HashtagContext"/>s, if one exists.
        /// </summary>
        /// <param name="hashtagContexts">The hashtag parsing
        /// contexts.</param>
        /// <returns>The line ID if one is present in the hashtag contexts,
        /// otherwise <c>null</c>.</returns>
        internal static YarnSpinnerParser.HashtagContext? GetLineIDTag(YarnSpinnerParser.HashtagContext[] hashtagContexts)
        {
            // if there are any hashtags
            if (hashtagContexts != null)
            {
                foreach (var hashtagContext in hashtagContexts)
                {
                    string tagText = hashtagContext.text.Text;
                    if (tagText.StartsWith("line:", StringComparison.InvariantCulture))
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
        /// <param name="line">The line statement to extract the line ID
        /// from.</param>
        /// <returns>The line ID found in this line.</returns>
        /// <exception cref="ArgumentException">Thrown when the line does not
        /// have a line ID.</exception>
        internal static string GetLineID(YarnSpinnerParser.Line_statementContext line)
        {
            var lineIDHashTag = GetLineIDTag(line.hashtag());

            if (lineIDHashTag == null)
            {
                throw new ArgumentException("Internal error: line does not have a line ID");
            }

            var lineID = lineIDHashTag.text.Text;
            return lineID;
        }

        internal static bool TryGetOnceHashtag(IEnumerable<YarnSpinnerParser.HashtagContext>? hashtags, out YarnSpinnerParser.HashtagContext result)
        {
            if (hashtags != null)
            {
                foreach (var hashtagContext in hashtags)
                {
                    string tagText = hashtagContext.text.Text;
                    if (tagText.Equals("once", StringComparison.InvariantCulture))
                    {
                        result = hashtagContext;
                        return true;
                    }
                }
            }
            result = null;
            return false;
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
        public static string GetDocumentComments(CommonTokenStream tokens, ParserRuleContext context, bool allowCommentsAfter = true)
        {
            string description = null;

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

    public partial class YarnSpinnerParser
    {
        public partial class NodeContext
        {
            /// <summary>
            /// Gets the title of this node, as specified in its '<c>title</c>'
            /// header. If it is not present, returns <see langword="null"/>.
            /// </summary>
            public string NodeTitle
            {
                get
                {
                    var headers = this.header();

                    if (headers == null)
                    {
                        return null;
                    }

                    foreach (var header in headers)
                    {
                        var headerType = header.header_key;
                        var headerValue = header.header_value;
                        if (headerType == null || headerValue == null)
                        {
                            continue;
                        }

                        if (headerType.Text.Equals("title", StringComparison.CurrentCulture) == false)
                        {
                            continue;
                        }

                        return headerValue.Text;
                    }

                    return null;
                }
            }
        }
    }
}
