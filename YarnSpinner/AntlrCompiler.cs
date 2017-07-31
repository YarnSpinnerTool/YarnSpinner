using System;
using System.Text;
using System.Collections.Generic;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace Yarn
{
    public class AntlrCompiler : YarnSpinnerParserBaseListener
    {
        internal struct CompileFlags
        {
            // should we emit code that turns (VAR_SHUFFLE_OPTIONS) off
            // after the next RunOptions bytecode?
            public bool DisableShuffleOptionsAfterNextSet;
        }

        internal CompileFlags flags;

        private int labelCount = 0;

        internal Program program { get; private set; }

        internal Node currentNode = null;

        internal Library library;

        // this is used to determine if we are to do any parsing
        // or just dump the entire node body as raw text
        internal bool rawTextNode = false;

        internal AntlrCompiler(Library library)
        {
            program = new Program();
            this.library = library;
        }

        // Generates a unique label name to use
        internal string RegisterLabel(string commentary = null)
        {
            return "L" + labelCount++ + commentary;
        }
        // creates the relevant instruction and adds it to the stack
        void Emit(Node node, ByteCode code, object operandA = null, object operandB = null)
        {
            var instruction = new Instruction();
            instruction.operation = code;
            instruction.operandA = operandA;
            instruction.operandB = operandB;

            node.instructions.Add(instruction);

            if (code == ByteCode.Label)
            {
                // Add this label to the label table
                node.labels.Add((string)instruction.operandA, node.instructions.Count - 1);
            }
        }
        // exactly same as above but defaults to using currentNode
        // creates the relevant instruction and adds it to the stack
        internal void Emit(ByteCode code, object operandA = null, object operandB = null)
        {
            this.Emit(this.currentNode, code, operandA, operandB);
        }
        // returns the lineID for this statement if it has one
        // otherwise returns null
        // takes in a hashtag block which it handles here
        // may need to be changed as future hashtags get support
        internal string GetLineID(YarnSpinnerParser.Hashtag_blockContext context)
        {
            // if there are any hashtags
            if (context != null)
            {
                foreach (var hashtag in context.hashtag())
                {
                    string tagText = hashtag.GetText().Trim('#');
                    if (tagText.StartsWith("line:"))
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
            if (currentNode != null)
            {
                string newNode = context.header().header_title().TITLE_TEXT().GetText().Trim();
                string message = string.Format("Discovered a new node {0} while {1} is still being parsed", newNode, currentNode.name);
				throw new Yarn.ParseException(message);
            }
            currentNode = new Node();
            rawTextNode = false;
        }
        // have left the current node
        // store it into the program
        // wipe the var and make it ready to go again
        public override void ExitNode(YarnSpinnerParser.NodeContext context)
        {
            program.nodes[currentNode.name] = currentNode;
            currentNode = null;
            rawTextNode = false;
        }

        // quick check to make sure we have the required number of headers
        // basically only allowed one of each tags, position, colourID
        // don't need to check for title because without it'll be a parse error
        public override void EnterHeader(YarnSpinnerParser.HeaderContext context)
        {
            if (context.header_tag().Length > 1)
            {
                string message = string.Format("Too many header tags defined inside {0}", context.header_title().TITLE_TEXT().GetText().Trim());
                throw new Yarn.ParseException(message);
            }
        }
        // all we need to do is store the title as the name of the node
        public override void EnterHeader_title(YarnSpinnerParser.Header_titleContext context)
        {
            currentNode.name = context.TITLE_TEXT().GetText().Trim();
        }
        // parsing the header tags
        // will not enter if there aren't any
        public override void EnterHeader_tag(YarnSpinnerParser.Header_tagContext context)
        {
            var tags = new List<string>(context.TAG_TEXT().GetText().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            // if the node is to be parsed as raw text
            // ie just straight passthrough
            // this is for things like if you want to store books in YarnSpinner
            if (tags.Contains("rawText"))
            {
                rawTextNode = true;
            }
            // storing the tags in the node
            currentNode.tags = tags;
        }
        // have finished with the header
        // so about to enter the node body and all its statements
        // do the initial setup required before compiling that body statements
        // eg emit a new startlabel
        public override void ExitHeader(YarnSpinnerParser.HeaderContext context)
        {
            // if this is flagged as a regular node
            if (!rawTextNode)
            {
                Emit(currentNode, ByteCode.Label, RegisterLabel());
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
                // moving in by 4 from the end to cut off the ---/=== delimiters
                // and their associated /n's
				int start = context.Start.StartIndex + 4;
				int end = context.Stop.StopIndex - 4;
                string body = context.Start.InputStream.GetText(new Interval(start, end));

                currentNode.sourceTextStringID = program.RegisterString(body, currentNode.name, "line:" + currentNode.name, context.Start.Line, true);
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
                foreach (var instruction in currentNode.instructions)
                {
                    if (instruction.operation == ByteCode.AddOption)
                    {
                        hasRemainingOptions = true;
                    }
                    if (instruction.operation == ByteCode.ShowOptions)
                    {
                        hasRemainingOptions = false;
                    }
                }

                // If this compiled node has no lingering options to show at the end of the node, then stop at the end
                if (hasRemainingOptions == false)
                {
                    Emit(currentNode, ByteCode.Stop);
                }
                else
                {
                    // Otherwise, show the accumulated nodes and then jump to the selected node
                    Emit(currentNode, ByteCode.ShowOptions);

                    if (flags.DisableShuffleOptionsAfterNextSet == true)
                    {
                        Emit(currentNode, ByteCode.PushBool, false);
                        Emit(currentNode, ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
                        Emit(currentNode, ByteCode.Pop);
                        flags.DisableShuffleOptionsAfterNextSet = false;
                    }

                    Emit(currentNode, ByteCode.RunNode);
                }
            }
        }
    }

    // the visitor for the body of the node
    // does not really return ints, just has to return something
    // might be worth later investigating returning Instructions
    public class BodyVisitor : YarnSpinnerParserBaseVisitor<int>
    {
        internal AntlrCompiler compiler;

        public BodyVisitor(AntlrCompiler compiler)
        {
            this.compiler = compiler;
            this.loadOperators();
        }

        // a regular ol' line of text
        public override int VisitLine_statement(YarnSpinnerParser.Line_statementContext context)
        {
            // grabbing the line of text and stripping off any "'s if they had them
            string lineText = context.text().GetText().Trim('"');

            // getting the lineID from the hashtags if it has one
            string lineID = compiler.GetLineID(context.hashtag_block());

            // technically this only gets the line the statement started on
            int lineNumber = context.Start.Line;

            // TODO: why is this called num?
            string num = compiler.program.RegisterString(lineText, compiler.currentNode.name, lineID, lineNumber, true);

            compiler.Emit(ByteCode.RunLine, num);

            return 0;
        }

        // an option statement
        // [[ OPTION_TEXT | OPTION_LINK]] or [[OPTION_TEXT]]
        public override int VisitOption_statement(YarnSpinnerParser.Option_statementContext context)
        {
            // if it is a split style option
            if (context.OPTION_LINK() != null)
            {
                string destination = context.OPTION_LINK().GetText();
                string label = context.OPTION_TEXT().GetText();

                int lineNumber = context.Start.Line;

                // getting the lineID from the hashtags if it has one
                string lineID = compiler.GetLineID(context.hashtag_block());

                string stringID = compiler.program.RegisterString(label, compiler.currentNode.name, lineID, lineNumber, true);
                compiler.Emit(ByteCode.AddOption, stringID, destination);
            }
            else
            {
                string destination = context.OPTION_TEXT().GetText();
                compiler.Emit(ByteCode.RunNode, destination);
            }
            return 0;
        }

        // for setting variables, has two forms
        // << SET variable TO/= expression >>
        // << SET expression >>
        // the second form does need to match the structure:
        // variable (+= -= *= /= %=) expression
        public override int VisitSet_statement(YarnSpinnerParser.Set_statementContext context)
        {
            // if it is the first form
            // a regular << SET $varName TO expression >>
            if (context.variable() != null)
            {
                // add the expression (whatever it resolves to)
                Visit(context.expression());

                // now store the variable and clean up the stack
                string variableName = context.variable().GetText();
                compiler.Emit(ByteCode.StoreVariable, variableName);
                compiler.Emit(ByteCode.Pop);
            }
            // it is the second form
            else
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
                    throw Yarn.ParseException.Make(context,"Invalid expression inside assignment statement");
                }
            }

            return 0;
        }

        // semi-free form text that gets passed along to the game
        // for things like <<turn fred left>> or <<unlockAchievement FacePlant>>
        public override int VisitAction_statement(YarnSpinnerParser.Action_statementContext context)
        {
            char[] trimming = { '<', '>' };
            string action = context.GetText().Trim(trimming);

            // TODO: look into replacing this as it seems a bit odd
            switch (action)
            {
                case "stop":
                    compiler.Emit(ByteCode.Stop);
                    break;
                case "shuffleNextOptions":
                    compiler.Emit(ByteCode.PushBool, true);
                    compiler.Emit(ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
                    compiler.Emit(ByteCode.Pop);
                    compiler.flags.DisableShuffleOptionsAfterNextSet = true;
                    break;
                default:
                    compiler.Emit(ByteCode.RunCommand, action);
                    break;
            }

			return 0;
        }

        // solo function statements
        // this is such a weird thing...
        // COMMAND_FUNC expression, expression ) >>
        // COMMAND_FUNC = << ID (
        public override int VisitFunction_statement(YarnSpinnerParser.Function_statementContext context)
        {
            char[] lTrim = { '<' };
            char[] rTrim = { '(' };
            string functionName = context.GetChild(0).GetText().TrimStart(lTrim).TrimEnd(rTrim);

            var output = this.HandleFunction(functionName, context.expression());
            // failed to handle the function
            if (output == false)
            {
                Yarn.ParseException.Make(context, "Invalid number of parameters for " + functionName);
            }

            return 0;
        }
        // emits the required tokens for the function call
        // returns a bool indicating if the function was valid
        private bool HandleFunction(string functionName, YarnSpinnerParser.ExpressionContext[] parameters)
        {
			// this will throw an exception if it doesn't exist
			FunctionInfo functionInfo = compiler.library.GetFunction(functionName);

			// if the function is not variadic we need to check it has the right number of params
			if (functionInfo.paramCount != -1)
			{
				if (parameters.Length != functionInfo.paramCount)
				{
                    // invalid function, return false
                    return false;
				}
			}

			// generate the instructions for all of the parameters
			foreach (var parameter in parameters)
			{
				Visit(parameter);
			}

			// if the function is variadic we push the parameter number onto the stack
			// variadic functions are those with paramCount of -1
			if (functionInfo.paramCount == -1)
			{
				compiler.Emit(ByteCode.PushNumber, parameters.Length);
			}

			// then call the function itself
			compiler.Emit(ByteCode.CallFunc, functionName);
            // everything went fine, return true
            return true;
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

            compiler.Emit(ByteCode.Label, endOfIfStatementLabel);

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
                compiler.Emit(ByteCode.JumpIfFalse, endOfClauseLabel);
            }

            // running through all of the children statements
            foreach (var child in children)
            {
                Visit(child);
            }

            compiler.Emit(ByteCode.JumpTo, jumpLabel);

            if (expression != null)
            {
                compiler.Emit(ByteCode.Label, endOfClauseLabel);
                compiler.Emit(ByteCode.Pop);
            }
        }

        // tiny helper to return the text of a short cut
        // making it a separate method call because I am positive shortcuts will change
        private string ShortcutText(YarnSpinnerParser.Shortcut_textContext context)
        {
            return context.SHORTCUT_TEXT().GetText().Trim();
        }
        // for the shortcut options
        // (-> line of text <<if expression>> indent statements dedent)+
        public override int VisitShortcut_statement(YarnSpinnerParser.Shortcut_statementContext context)
        {
            string endOfGroupLabel = compiler.RegisterLabel("group_end");

            var labels = new List<string>();

            int optionCount = 0;

            foreach (var shortcut in context.shortcut())
            {
                string optionDestinationLabel = compiler.RegisterLabel("option_" + (optionCount + 1));
                labels.Add(optionDestinationLabel);

                string endOfClauseLabel = null;
                if (shortcut.shortcut_conditional() != null)
                {
                    endOfClauseLabel = compiler.RegisterLabel("conditional_" + optionCount);

                    Visit(shortcut.shortcut_conditional().expression());

                    compiler.Emit(ByteCode.JumpIfFalse, endOfClauseLabel);
                }

                // getting the lineID from the hashtags if it has one
                string lineID = compiler.GetLineID(shortcut.hashtag_block());

                string shortcutLine = ShortcutText(shortcut.shortcut_text());
                string labelStringID = compiler.program.RegisterString(shortcutLine, compiler.currentNode.name, lineID, shortcut.Start.Line, true);

                compiler.Emit(ByteCode.AddOption, labelStringID, optionDestinationLabel);

                if (shortcut.shortcut_conditional() != null)
                {
                    compiler.Emit(ByteCode.Label, endOfClauseLabel);
                    compiler.Emit(ByteCode.Pop);
                }
                optionCount++;
            }

            compiler.Emit(ByteCode.ShowOptions);

            // TODO: investigate a cleaner way because this is odd...
            if (compiler.flags.DisableShuffleOptionsAfterNextSet == true)
            {
                compiler.Emit(ByteCode.PushBool, false);
                compiler.Emit(ByteCode.StoreVariable, VirtualMachine.SpecialVariables.ShuffleOptions);
                compiler.Emit(ByteCode.Pop);
                compiler.flags.DisableShuffleOptionsAfterNextSet = false;
            }

            compiler.Emit(ByteCode.Jump);

            optionCount = 0;
            foreach (var shortcut in context.shortcut())
            {
                compiler.Emit(ByteCode.Label, labels[optionCount]);

                // running through all the children statements of the shortcut
                foreach (var child in shortcut.statement())
                {
                    Visit(child);
                }

                compiler.Emit(ByteCode.JumpTo, endOfGroupLabel);

                optionCount++;
            }

            compiler.Emit(ByteCode.Label, endOfGroupLabel);
            compiler.Emit(ByteCode.Pop);

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
            int expression = Visit(context.expression());

            // TODO: temp operator call
            compiler.Emit(ByteCode.CallFunc, TokenType.UnaryMinus.ToString());

            return 0;
        }
        // (not NOT !)expression
        public override int VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            Visit(context.expression());

            // TODO: temp operator call
            compiler.Emit(ByteCode.CallFunc, TokenType.Not.ToString());

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
            compiler.Emit(ByteCode.CallFunc, tokens[op].ToString());
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
            compiler.Emit(ByteCode.PushVariable, varName);

            // run the expression
            Visit(expression);

            // Stack now contains [currentValue, expressionValue]

            // now we evaluate the operator
            // op will match to one of + - / * %
            compiler.Emit(ByteCode.CallFunc, tokens[op].ToString());

            // Stack now has the destination value
            // now store the variable and clean up the stack
            compiler.Emit(ByteCode.StoreVariable, varName);
            compiler.Emit(ByteCode.Pop);
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
            float number = float.Parse(context.BODY_NUMBER().GetText());
            compiler.Emit(ByteCode.PushNumber, number);

            return 0;
        }
        public override int VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            compiler.Emit(ByteCode.PushBool, true);

            return 0;
        }
        public override int VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            compiler.Emit(ByteCode.PushBool, false);
            return 0;
        }
        public override int VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            string variableName = context.VAR_ID().GetText();
            compiler.Emit(ByteCode.PushVariable, variableName);

            return 0;
        }
        public override int VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            // stripping the " off the front and back
            // actually is this what we want?
            string stringVal = context.COMMAND_STRING().GetText().Trim('"');

            int lineNumber = context.Start.Line;
            string id = compiler.program.RegisterString(stringVal, compiler.currentNode.name, null, lineNumber, false);
            compiler.Emit(ByteCode.PushString, id);

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
            compiler.Emit(ByteCode.PushNull);
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
        String yarnName = "G";
        public override void EnterHeader_title(YarnSpinnerParser.Header_titleContext context)
        {
            currentNode = context.HEADER_TITLE().GetText();
            graph.nodes.Add(currentNode);
        }
        public override void ExitOption_statement(YarnSpinnerParser.Option_statementContext context)
        {
            var link = context.OPTION_LINK();
            if (link != null)
            {
                graph.edge(currentNode,link.GetText());
            }
            else
            {
                graph.edge(currentNode, context.OPTION_TEXT().GetText());
            }
        }
    }
}
