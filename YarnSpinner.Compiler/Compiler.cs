using System;
using System.Text;
using System.Collections.Generic;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using static Yarn.Instruction.Types;


namespace Yarn.Compiler
{
    
    public enum Status {
        /// The compilation succeeded with no errors
        Succeeded, 

        /// The compilation succeeded, but some strings do not have string tags. 
        SucceededUntaggedStrings,

    }
    
    public class Compiler : YarnSpinnerParserBaseListener
    {

        
        // (compilation failures will result in an exception, so they don't get a Status)

        private int labelCount = 0;

        internal Program program { get; private set; }

        internal Node currentNode = null;

        // this is used to determine if we are to do any parsing
        // or just dump the entire node body as raw text
        internal bool rawTextNode = false;

        internal string fileName;

        private bool containsImplicitStringTags;

        // A regular expression used to detect illegal characters in node titles
        private Regex invalidNodeTitleNameRegex = new System.Text.RegularExpressions.Regex(@"[\[<>\]{}\|:\s#\$]");

        internal Compiler(string fileName)
        {
            program = new Program();
            this.fileName = fileName;            
        }

        
        public static Status CompileFile(string path, out Program program, out IDictionary<string,StringInfo> stringTable) {
            var source = File.ReadAllText(path);

            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            return CompileString(source, fileName, out program, out stringTable);

        }

        #if DEBUG
        internal string parseTree;
        internal List<string> tokens;
        #endif

        // Given a bunch of raw text, load all nodes that were inside it.
        public static Status CompileString(string text, string fileName, out Program program, out IDictionary<string,StringInfo> stringTable)
        {

            ICharStream input = CharStreams.fromstring(text);

            YarnSpinnerLexer lexer = new YarnSpinnerLexer(input);
            CommonTokenStream tokens = new CommonTokenStream(lexer);

            YarnSpinnerParser parser = new YarnSpinnerParser(tokens);

            // turning off the normal error listener and using ours
            parser.RemoveErrorListeners();
            parser.AddErrorListener(ErrorListener.Instance);

            IParseTree tree;
            try {
                tree = parser.dialogue();
            } catch (ParseException e) {
                var tokenStringList = new List<string>();
                tokens.Reset();
                foreach (var token in tokens.GetTokens()) {
                    tokenStringList.Add($"{token.Line}:{token.Column} {YarnSpinnerLexer.DefaultVocabulary.GetDisplayName(token.Type)} \"{token.Text}\"");
                }
                throw new ParseException($"{e.Message}\n\nTokens:\n{string.Join("\n", tokenStringList)}");
            }

            Compiler compiler = new Compiler(fileName);
            
            compiler.Compile(tree);

            program = compiler.program;
            stringTable = compiler.StringTable;

            if (compiler.containsImplicitStringTags) {
                return Status.SucceededUntaggedStrings;
            } else {
                return Status.Succeeded;
            }
        }

        static public List<string> GetTokensFromFile(string path) {
            var text = File.ReadAllText(path);
            return GetTokensFromString(text);
        }

        static public List<string> GetTokensFromString(string text) {
            ICharStream input = CharStreams.fromstring(text);

            YarnSpinnerLexer lexer = new YarnSpinnerLexer(input);
            
            var tokenStringList = new List<string>();

            var tokens = lexer.GetAllTokens();
            foreach (var token in tokens) {
                tokenStringList.Add($"{token.Line}:{token.Column} {YarnSpinnerLexer.DefaultVocabulary.GetDisplayName(token.Type)} \"{token.Text}\"");
            }

            return tokenStringList;
                
        } 

        public Dictionary<string, StringInfo> StringTable = new Dictionary<string, StringInfo>();

        int stringCount = 0;

        public string RegisterString(string text, string nodeName, string lineID, int lineNumber, string[] tags)
		{

			string key;

            bool isImplicit;

			if (lineID == null) {
				key = $"{fileName}-{nodeName}-{stringCount}";
                
                stringCount += 1;

                // Note that we had to make up a tag for this string, which
                // may not be the same on future compilations
                containsImplicitStringTags = true;

                isImplicit = true;
            }
			else {
				key = lineID;

                isImplicit = false;
            }

            var theString = new StringInfo(text, fileName, nodeName, lineNumber, isImplicit, tags);
            
			// It's not in the list; append it
			StringTable.Add(key, theString);

			return key;
		}



        // Generates a unique label name to use
        internal string RegisterLabel(string commentary = null)
        {
            return "L" + labelCount++ + commentary;
        }
        // creates the relevant instruction and adds it to the stack
        void Emit(Node node, OpCode code, params Operand[] operands)
        {
            var instruction = new Instruction();
            instruction.Opcode = code;

            instruction.Operands.Add(operands);

            node.Instructions.Add(instruction);
            
        }
        // exactly same as above but defaults to using currentNode
        // creates the relevant instruction and adds it to the stack
        internal void Emit(OpCode code, params Operand[] operands)
        {
            this.Emit(this.currentNode, code, operands);
        }
        // returns the lineID for this statement if it has one
        // otherwise returns null
        // takes in a hashtag block which it handles here
        // may need to be changed as future hashtags get support
        internal string GetLineID(YarnSpinnerParser.HashtagContext[] context)
        {
            // if there are any hashtags
            if (context != null)
            {
                foreach (var hashtag in context)
                {
                    string tagText = hashtag.text.Text;
                    if (tagText.StartsWith("line:", StringComparison.InvariantCulture))
                    {
                        return tagText;
                    }
                }
            }
            return null;
        }

        // this replaces the CompileNode from the old compiler
        // will start walking the parse tree
        // emitting byte code as it goes along
        // this will all get stored into our program var
        // needs a tree to walk, this comes from the ANTLR Parser/Lexer steps
        internal void Compile(IParseTree tree)
        {
            ParseTreeWalker walker = new ParseTreeWalker();
            walker.Walk(this, tree);
        }

        // we have found a new node
        // set up the currentNode var ready to hold it and otherwise continue
        public override void EnterNode(YarnSpinnerParser.NodeContext context)
        {
            currentNode = new Node();
            rawTextNode = false;
        }
        // have left the current node
        // store it into the program
        // wipe the var and make it ready to go again
        public override void ExitNode(YarnSpinnerParser.NodeContext context)
        {
            program.Nodes[currentNode.Name] = currentNode;
            currentNode = null;
            rawTextNode = false;
        }

        
        // have finished with the header
        // so about to enter the node body and all its statements
        // do the initial setup required before compiling that body statements
        // eg emit a new startlabel
        public override void ExitHeader(YarnSpinnerParser.HeaderContext context)
        {
            var headerKey = context.header_key.Text;

            // Use the header value if provided, else fall back to the
            // empty string. This means that a header like "foo: \n" will
            // be stored as 'foo', '', consistent with how it was typed.
            // That is, it's not null, because a header was provided, but
            // it was written as an empty line.
            var headerValue = context.header_value?.Text ?? "";

            if (headerKey.Equals("title", StringComparison.InvariantCulture)) {
                // Set the name of the node
                currentNode.Name = headerValue;

                // Throw an exception if this node name contains illegal
                // characters
                if (invalidNodeTitleNameRegex.IsMatch(currentNode.Name)) {
                    throw new ParseException($"The node '{currentNode.Name}' contains illegal characters in its title.");
                }
            }

            if (headerKey.Equals("tags", StringComparison.InvariantCulture)) {
                // Split the list of tags by spaces, and use that

                var tags = headerValue.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);

                currentNode.Tags.Add(tags);

                if (currentNode.Tags.Contains("rawText")) {
                    // This is a raw text node. Flag it as such for future compilation.
                    rawTextNode = true;
                }
                
            }
            
        }

        // have entered the body
        // the header should have finished being parsed and currentNode ready
        // all we do is set up a body visitor and tell it to run through all the statements
        // it handles everything from that point onwards
        public override void EnterBody(YarnSpinnerParser.BodyContext context)
        {
            // if it is a regular node
            if (!rawTextNode)
            {
                // This is the start of a node that we can jump to. Add a label at this point.
                currentNode.Labels.Add(RegisterLabel(), currentNode.Instructions.Count);                

                BodyVisitor visitor = new BodyVisitor(this);

                foreach (var statement in context.statement())
                {
                    visitor.Visit(statement);
                }
            }
            // we are a rawText node
            // turn the body into text
            // save that into the node
            // perform no compilation
            // TODO: oh glob! there has to be a better way
            else
            {
                currentNode.SourceTextStringID = RegisterString(context.GetText(), currentNode.Name, "line:" + currentNode.Name, context.Start.Line, null);
			}
        }

        // exiting the body of the node, time for last minute work
        // before moving onto the next node
        // Does this node end after emitting AddOptions codes
        // without calling ShowOptions?
        public override void ExitBody(YarnSpinnerParser.BodyContext context)
        {
            // if it is a regular node
            if (!rawTextNode)
            {
                // Note: this only works when we know that we don't have
                // AddOptions and then Jump up back into the code to run them.
                // TODO: A better solution would be for the parser to flag
                // whether a node has Options at the end.
                var hasRemainingOptions = false;
                foreach (var instruction in currentNode.Instructions)
                {
                    if (instruction.Opcode == OpCode.AddOption)
                    {
                        hasRemainingOptions = true;
                    }
                    if (instruction.Opcode == OpCode.ShowOptions)
                    {
                        hasRemainingOptions = false;
                    }
                }

                // If this compiled node has no lingering options to show at the end of the node, then stop at the end
                if (hasRemainingOptions == false)
                {
                    Emit(currentNode, OpCode.Stop);
                }
                else
                {
                    // Otherwise, show the accumulated nodes and then jump to the selected node
                    Emit(currentNode, OpCode.ShowOptions);

                    // Showing options will make the execution stop; the
                    // user will have invoked code that pushes the name of
                    // a node onto the stack, which RunNode handles
                    Emit(currentNode, OpCode.RunNode);
                }
            }
        }
    }

    // the visitor for the body of the node
    // does not really return ints, just has to return something
    // might be worth later investigating returning Instructions
    public class BodyVisitor : YarnSpinnerParserBaseVisitor<int>
    {
        internal Compiler compiler;

        public BodyVisitor(Compiler compiler)
        {
            this.compiler = compiler;
            this.loadOperators();
        }

        private void GenerateFormattedText(IList<IParseTree> nodes, out string outputString, out int expressionCount) {
            expressionCount = 0;
            StringBuilder composedString = new StringBuilder();

            // First, visit all of the nodes, which are either terminal
            // text nodes or expressions. if they're expressions, we
            // evaluate them, and inject a positional reference into the
            // final string.
            foreach (var child in nodes) {
                if (child is ITerminalNode) {
                    composedString.Append(child.GetText());
                } else if (child is ParserRuleContext) {
                    // assume that this is an expression (the parser only
                    // permits them to be expressions, but we can't specify
                    // that here) - visit it, and we will emit code that
                    // pushes the final value of this expression onto the
                    // stack. running the line will pop these expressions
                    // off the stack.
                    Visit(child);
                    composedString.Append("{" + expressionCount + "}");
                    expressionCount += 1;
                }
            }        

            outputString = composedString.ToString().Trim();
            
        } 

        private string[] GetHashtagTexts (YarnSpinnerParser.HashtagContext[] hashtags) {
            // Add hashtag
            var hashtagText = new List<string>();
            foreach (var tag in hashtags) {
                hashtagText.Add(tag.HASHTAG_TEXT().GetText());
            }
            return hashtagText.ToArray();
        }

        // a regular ol' line of text
        public override int VisitLine_statement(YarnSpinnerParser.Line_statementContext context)
        {
            // TODO: add support for line conditions:
            //
            // Mae: here's a line <<if true>>
            //
            // is identical to
            //
            // <<if true>>
            // Mae: here's a line
            // <<endif>>

            // Convert the formatted string into a string with
            // placeholders, and evaluate the inline expressions and push
            // the results onto the stack.
            GenerateFormattedText(context.line_formatted_text().children, out var composedString, out var expressionCount);    
            
            // Get the lineID for this string from the hashtags if it has one; otherwise, a new one will be created
            string lineID = compiler.GetLineID(context.hashtag());

            var hashtagText = GetHashtagTexts(context.hashtag());

            int lineNumber = context.Start.Line;

            string stringID = compiler.RegisterString(
                composedString.ToString(), 
                compiler.currentNode.Name, 
                lineID, 
                lineNumber, 
                hashtagText
            );

            compiler.Emit(OpCode.RunLine, new Operand(stringID), new Operand(expressionCount));
            
            return 0;
        }

        // a jump statement
        // [[ NodeName ]]
        public override int VisitOptionJump(YarnSpinnerParser.OptionJumpContext context)
        {
            string destination = context.NodeName.Text.Trim();
            compiler.Emit(OpCode.RunNode, new Operand(destination));
            return 0;
        }

        public override int VisitOptionLink(YarnSpinnerParser.OptionLinkContext context)
        {

            // Create the formatted string and evaluate any inline
            // expressions            
            GenerateFormattedText(context.option_formatted_text().children, out var composedString, out var expressionCount);

            string destination = context.NodeName.Text.Trim();
            string label = composedString;

            int lineNumber = context.Start.Line;

            // getting the lineID from the hashtags if it has one
            string lineID = compiler.GetLineID(context.hashtag());
            
            var hashtagText = GetHashtagTexts(context.hashtag());

            string stringID = compiler.RegisterString(label, compiler.currentNode.Name, lineID, lineNumber, hashtagText);

            compiler.Emit(OpCode.AddOption, new Operand(stringID), new Operand(destination), new Operand(expressionCount));

            return 0;
        }

        // A set command: explicitly setting a value to an expression
        // <<set $foo to 1>>
        public override int VisitSetVariableToValue(YarnSpinnerParser.SetVariableToValueContext context)
        {
            // add the expression (whatever it resolves to)
            Visit(context.expression());

            // now store the variable and clean up the stack
            string variableName = context.VAR_ID().GetText();
            compiler.Emit(OpCode.StoreVariable, new Operand(variableName));
            compiler.Emit(OpCode.Pop);
            return 0;
        }

        // A set command: evaluating an expression where the operator is an assignment-type
        public override int VisitSetExpression(YarnSpinnerParser.SetExpressionContext context)
        {
            // checking the expression is of the correct form
            var expression = context.expression();
            // TODO: is there really no more elegant way of doing this?!
            if (expression is YarnSpinnerParser.ExpMultDivModEqualsContext ||
                expression is YarnSpinnerParser.ExpPlusMinusEqualsContext)
            {
                // run the expression, it handles it from here
                Visit(expression);
            }
            else
            {
                // throw an error
                throw ParseException.Make(context,"Invalid expression inside assignment statement");
            }
            return 0;
        }  

        public override int VisitCall_statement(YarnSpinnerParser.Call_statementContext context)
        {
            // Visit our function call, which will invoke the function
            Visit(context.function());

            // TODO: if this function returns a value, it will be pushed
            // onto the stack, but there's no way for the compiler to know
            // that, so the stack will not be tidied up. is there a way for
            // that to work?
            return 0;
        }      

        // semi-free form text that gets passed along to the game
        // for things like <<turn fred left>> or <<unlockAchievement FacePlant>>
        public override int VisitCommand_statement(YarnSpinnerParser.Command_statementContext context)
        {
            var action = context.COMMAND_TEXT()[0].GetText().Trim();

            // TODO: support formatted text here

            // TODO: look into replacing this as it seems a bit odd
            switch (action)
            {
                case "stop":
                    // "stop" is a special command that immediately stops
                    // execution
                    compiler.Emit(OpCode.Stop);
                    break;                
                default:
                    compiler.Emit(OpCode.RunCommand, new Operand(action));
                    break;
            }

			return 0;
        }

        // emits the required bytecode for the function call
        private void HandleFunction(string functionName, YarnSpinnerParser.ExpressionContext[] parameters)
        {
			// generate the instructions for all of the parameters
			foreach (var parameter in parameters)
			{
				Visit(parameter);
			}

			// push the number of parameters onto the stack
			compiler.Emit(OpCode.PushFloat, new Operand(parameters.Length));
			
			// then call the function itself
			compiler.Emit(OpCode.CallFunc, new Operand(functionName));            
        }
        // handles emiting the correct instructions for the function
        public override int VisitFunction(YarnSpinnerParser.FunctionContext context)
        {
            string functionName = context.FUNC_ID().GetText();

            this.HandleFunction(functionName, context.expression());

            return 0;
        }

        // if statement
        // ifclause (elseifclause)* (elseclause)? <<endif>>
        public override int VisitIf_statement(YarnSpinnerParser.If_statementContext context)
        {
            // label to give us a jump point for when the if finishes
            string endOfIfStatementLabel = compiler.RegisterLabel("endif");

            // handle the if
            var ifClause = context.if_clause();
            generateClause(endOfIfStatementLabel, ifClause.statement(), ifClause.expression());

            // all elseifs
            foreach (var elseIfClause in context.else_if_clause())
            {
                generateClause(endOfIfStatementLabel, elseIfClause.statement(), elseIfClause.expression());
            }

            // the else, if there is one
            var elseClause = context.else_clause();
            if (elseClause != null)
            {
                generateClause(endOfIfStatementLabel, elseClause.statement(), null);
            }

            compiler.currentNode.Labels.Add(endOfIfStatementLabel, compiler.currentNode.Instructions.Count);                

            return 0;
        }
        internal void generateClause(string jumpLabel, YarnSpinnerParser.StatementContext[] children, YarnSpinnerParser.ExpressionContext expression)
        {
            string endOfClauseLabel = compiler.RegisterLabel("skipclause");

            // handling the expression (if it has one)
            // will only be called on ifs and elseifs
            if (expression != null)
            {
                Visit(expression);
                compiler.Emit(OpCode.JumpIfFalse, new Operand(endOfClauseLabel));
            }

            // running through all of the children statements
            foreach (var child in children)
            {
                Visit(child);
            }

            compiler.Emit(OpCode.JumpTo, new Operand(jumpLabel));

            if (expression != null)
            {
                compiler.currentNode.Labels.Add(endOfClauseLabel, compiler.currentNode.Instructions.Count);                
                compiler.Emit(OpCode.Pop);
            }
        }

        // for the shortcut options
        // (-> line of text <<if expression>> indent statements dedent)+
        public override int VisitShortcut_option_statement (YarnSpinnerParser.Shortcut_option_statementContext context) {
            
            string endOfGroupLabel = compiler.RegisterLabel("group_end");

            var labels = new List<string>();

            int optionCount = 0;

            // For each option, create an internal destination label that,
            // if the user selects the option, control flow jumps to. Then,
            // evaluate its associated line_statement, and use that as the
            // option text. Finally, add this option to the list of
            // upcoming options.
            foreach (var shortcut in context.shortcut_option())
            {
                // Generate the name of internal label that we'll jump to
                // if this option is selected. We'll emit the label itself
                // later.
                string optionDestinationLabel = compiler.RegisterLabel("option_" + (optionCount + 1));
                labels.Add(optionDestinationLabel);

                // This line statement may have a condition on it. If it
                // does, emit code that evaluates the condition, and skips
                // over the code that prepares and adds the option.
                string endOfClauseLabel = null;
                if (shortcut.line_statement().line_condition()?.Length > 0)
                {
                    // Register the label we'll jump to if the condition
                    // fails. We'll add it later.
                    endOfClauseLabel = compiler.RegisterLabel("conditional_" + optionCount);

                    // Ensure that we only have a single condition here
                    var conditions = shortcut.line_statement().line_condition();
                    if (conditions.Length > 1) {
                        throw new ParseException("More than one line condition is not allowed.");
                    }

                    // Evaluate the condition, and jump to the end of
                    // clause if it evaluates to false.
                    var firstCondition = conditions[0];

                    Visit(firstCondition.expression());

                    compiler.Emit(OpCode.JumpIfFalse, new Operand(endOfClauseLabel));
                }

                // We can now prepare and add the option.

                // Start by figuring out the text that we want to add. This
                // will involve evaluating any inline expressions.
                GenerateFormattedText(shortcut.line_statement().line_formatted_text().children, out var composedString, out var expressionCount);

                // Get the line ID from the hashtags if it has one
                string lineID = compiler.GetLineID(shortcut.line_statement().hashtag());

                // Get the hashtags for the line
                var hashtags = GetHashtagTexts(shortcut.line_statement().hashtag());

                // Register this string
                string labelStringID = compiler.RegisterString(composedString, compiler.currentNode.Name, lineID, shortcut.Start.Line, hashtags);

                // And add this option to the list.
                compiler.Emit(OpCode.AddOption, new Operand(labelStringID), new Operand(optionDestinationLabel), new Operand(expressionCount));

                // If we had a line condition, now's the time to generate
                // the label that we'd jump to if its condition is false.
                if (shortcut.line_statement().line_condition()?.Length > 0)
                {
                    compiler.currentNode.Labels.Add(endOfClauseLabel, compiler.currentNode.Instructions.Count);    

                    // JumpIfFalse doesn't change the stack, so we need to
                    // tidy up            
                    compiler.Emit(OpCode.Pop);
                }

                optionCount++;
            }

            // All of the options that we intend to show are now ready to
            // go.
            compiler.Emit(OpCode.ShowOptions);
            
            // The top of the stack now contains the name of the label we
            // want to jump to. Jump to it now.
            compiler.Emit(OpCode.Jump);

            // We'll now emit the labels and code associated with each
            // option.
            optionCount = 0;
            foreach (var shortcut in context.shortcut_option())
            {
                // Emit the label for this option's code
                compiler.currentNode.Labels.Add(labels[optionCount], compiler.currentNode.Instructions.Count);                
                
                // Run through all the children statements of the shortcut
                // option.
                foreach (var child in shortcut.statement())
                {
                    Visit(child);
                }

                // Jump to the end of this shortcut option group.
                compiler.Emit(OpCode.JumpTo, new Operand(endOfGroupLabel));

                optionCount++;
            }

            // We made it to the end! Mark the end of the group, so we can jump to it.
            compiler.currentNode.Labels.Add(endOfGroupLabel, compiler.currentNode.Instructions.Count);                
            compiler.Emit(OpCode.Pop);

            return 0;
        }

        // the calls for the various operations and expressions
        // first the special cases (), unary -, !, and if it is just a value by itself
        #region specialCaseCalls
        // (expression)
        public override int VisitExpParens(YarnSpinnerParser.ExpParensContext context)
        {
            return Visit(context.expression());
        }
        // -expression
        public override int VisitExpNegative(YarnSpinnerParser.ExpNegativeContext context)
        {
            Visit(context.expression());

            // TODO: temp operator call

            // Indicate that we are pushing one parameter
            compiler.Emit(OpCode.PushFloat, new Operand(1));
            
            compiler.Emit(OpCode.CallFunc, new Operand(TokenType.UnaryMinus.ToString()));

            return 0;
        }
        // (not NOT !)expression
        public override int VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            Visit(context.expression());

            // TODO: temp operator call

            // Indicate that we are pushing one parameter
            compiler.Emit(OpCode.PushFloat, new Operand(1));

            compiler.Emit(OpCode.CallFunc, new Operand(TokenType.Not.ToString()));

            return 0;
        }
        // variable
        public override int VisitExpValue(YarnSpinnerParser.ExpValueContext context)
        {
            return Visit(context.value());
        }
        #endregion

        // left OPERATOR right style expressions
        // the most common form of expressions
        // for things like 1 + 3
        #region lValueOperatorrValueCalls
        internal void genericExpVisitor(YarnSpinnerParser.ExpressionContext left, YarnSpinnerParser.ExpressionContext right, int op)
        {
            Visit(left);
            Visit(right);

            // TODO: temp operator call

            // Indicate that we are pushing two items for comparison
            compiler.Emit(OpCode.PushFloat, new Operand(2));

            compiler.Emit(OpCode.CallFunc, new Operand(tokens[op].ToString()));
        }
        // * / %
        public override int VisitExpMultDivMod(YarnSpinnerParser.ExpMultDivModContext context)
        {
            genericExpVisitor(context.expression(0), context.expression(1), context.op.Type);

            return 0;
        }
        // + -
        public override int VisitExpAddSub(YarnSpinnerParser.ExpAddSubContext context)
        {
            genericExpVisitor(context.expression(0), context.expression(1), context.op.Type);

            return 0;
        }
        // < <= > >=
        public override int VisitExpComparison(YarnSpinnerParser.ExpComparisonContext context)
        {
            genericExpVisitor(context.expression(0), context.expression(1), context.op.Type);

            return 0;
        }
        // == !=
        public override int VisitExpEquality(YarnSpinnerParser.ExpEqualityContext context)
        {
            genericExpVisitor(context.expression(0), context.expression(1), context.op.Type);

            return 0;
        }
        // and && or || xor ^
        public override int VisitExpAndOrXor(YarnSpinnerParser.ExpAndOrXorContext context)
        {
            genericExpVisitor(context.expression(0), context.expression(1), context.op.Type);

            return 0;
        }
        #endregion

        // operatorEquals style operators, eg +=
        // these two should only be called during a SET operation
        // eg << set $var += 1 >>
        // the left expression has to be a variable
        // the right value can be anything
        #region operatorEqualsCalls
        // generic helper for these types of expressions
        internal void opEquals(string varName, YarnSpinnerParser.ExpressionContext expression, int op)
        {
            // Get the current value of the variable
            compiler.Emit(OpCode.PushVariable, new Operand(varName));

            // run the expression
            Visit(expression);

            // Stack now contains [currentValue, expressionValue]

            // Indicate that we are pushing two items for comparison
            compiler.Emit(OpCode.PushFloat, new Operand(2));

            // now we evaluate the operator
            // op will match to one of + - / * %
            compiler.Emit(OpCode.CallFunc, new Operand(tokens[op].ToString()));

            // Stack now has the destination value
            // now store the variable and clean up the stack
            compiler.Emit(OpCode.StoreVariable, new Operand(varName));
            compiler.Emit(OpCode.Pop);
        }
        // *= /= %=
        public override int VisitExpMultDivModEquals(YarnSpinnerParser.ExpMultDivModEqualsContext context)
        {
            // call the helper to deal with this
            opEquals(context.variable().GetText(), context.expression(), context.op.Type);
            return 0;
        }
        // += -=
        public override int VisitExpPlusMinusEquals(YarnSpinnerParser.ExpPlusMinusEqualsContext context)
        {
            // call the helper to deal with this
            opEquals(context.variable().GetText(), context.expression(), context.op.Type);

            return 0;
        }
        #endregion

        // the calls for the various value types
        // this is a wee bit messy but is easy to extend, easy to read
        // and requires minimal checking as ANTLR has already done all that
        // does have code duplication though
        #region valueCalls
        public override int VisitValueVar(YarnSpinnerParser.ValueVarContext context)
        {
            return Visit(context.variable());
        }
        public override int VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            float number = float.Parse(context.NUMBER().GetText(), CultureInfo.InvariantCulture);
            compiler.Emit(OpCode.PushFloat, new Operand(number));

            return 0;
        }
        public override int VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            compiler.Emit(OpCode.PushBool, new Operand(true));

            return 0;
        }
        public override int VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            compiler.Emit(OpCode.PushBool, new Operand(false));
            return 0;
        }
        public override int VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            string variableName = context.VAR_ID().GetText();
            compiler.Emit(OpCode.PushVariable, new Operand(variableName));

            return 0;
        }
        public override int VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            // stripping the " off the front and back
            // actually is this what we want?
            string stringVal = context.STRING().GetText().Trim('"');

            compiler.Emit(OpCode.PushString, new Operand(stringVal));

            return 0;
        }
        // all we need do is visit the function itself, it will handle everything
        public override int VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            Visit(context.function());

            return 0;
        }
        // null value
        public override int VisitValueNull(YarnSpinnerParser.ValueNullContext context)
        {
            compiler.Emit(OpCode.PushNull);
            return 0;
        }
		#endregion

		// TODO: figure out a better way to do operators
        Dictionary<int, TokenType> tokens = new Dictionary<int, TokenType>();
        private void loadOperators()
        {
            // operators for the standard expressions
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_LESS_THAN_EQUALS] = TokenType.LessThanOrEqualTo;
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER_THAN_EQUALS] = TokenType.GreaterThanOrEqualTo;
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_LESS] = TokenType.LessThan;
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER] = TokenType.GreaterThan;

            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_EQUALS] = TokenType.EqualTo;
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_NOT_EQUALS] = TokenType.NotEqualTo;

            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_AND] = TokenType.And;
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_OR] = TokenType.Or;
            tokens[YarnSpinnerLexer.OPERATOR_LOGICAL_XOR] = TokenType.Xor;

            tokens[YarnSpinnerLexer.OPERATOR_MATHS_ADDITION] = TokenType.Add;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION] = TokenType.Minus;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION] = TokenType.Multiply;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_DIVISION] = TokenType.Divide;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_MODULUS] = TokenType.Modulo;
            // operators for the set expressions
            // these map directly to the operator if they didn't have the =
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_ADDITION_EQUALS] = TokenType.Add;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION_EQUALS] = TokenType.Minus;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION_EQUALS] = TokenType.Multiply;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_DIVISION_EQUALS] = TokenType.Divide;
            tokens[YarnSpinnerLexer.OPERATOR_MATHS_MODULUS_EQUALS] = TokenType.Modulo;
        }
	}

	public class Graph
	{
		public ArrayList<String> nodes = new ArrayList<String>();
		public MultiMap<String, String> edges = new MultiMap<String, String>();
        public string graphName = "G";

		public void edge(String source, String target)
		{
			edges.Map(source, target);
		}
		public String toDot()
		{
			StringBuilder buf = new StringBuilder();
            buf.AppendFormat("digraph {0} ",graphName);
            buf.Append("{\n");
			buf.Append("  ");
			foreach (String node in nodes)
			{ // print all nodes first
				buf.Append(node);
				buf.Append("; ");
			}
			buf.Append("\n");
			foreach (String src in edges.Keys)
			{
                IList<string> output;
				if (edges.TryGetValue(src, out output))
				{
					foreach (String trg in output)
					{
						buf.Append("  ");
						buf.Append(src);
						buf.Append(" -> ");
						buf.Append(trg);
						buf.Append(";\n");
					}
				}
			}
			buf.Append("}\n");
			return buf.ToString();
		}
	}
    public class GraphListener:YarnSpinnerParserBaseListener
    {
        String currentNode = null;
        public Graph graph = new Graph();

        public override void EnterHeader(YarnSpinnerParser.HeaderContext context)
        {
            if (context.header_key.Text == "title") {
                currentNode = context.header_value.Text;
            }
        }

        public override void ExitNode(YarnSpinnerParser.NodeContext context)
        {
            // Add this node to the graph
            graph.nodes.Add(currentNode);
        }
        public override void ExitOptionJump(YarnSpinnerParser.OptionJumpContext context) {
            graph.edge(currentNode, context.NodeName.Text);
        }

        public override void ExitOptionLink(YarnSpinnerParser.OptionLinkContext context) {
            graph.edge(currentNode, context.NodeName.Text);
        }

    }
}
