using System;
using System.Text;
using System.Collections.Generic;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Globalization;

using static Yarn.Compiler.Instruction.Types;

namespace Yarn.Compiler
{

    public partial class Operand {
        public Operand(bool value) : base() {
            this.BoolValue = value;
        }

        public Operand(string value) : base() {
            this.StringValue = value;
        }

        public Operand(float value) : base() {
            this.NumberValue = value;
        }
    }

    internal enum TokenType {


        // Special tokens
        Whitespace,
        Indent,
        Dedent,
        EndOfLine,
        EndOfInput,

        // Numbers. Everybody loves a number
        Number,

        // Strings. Everybody also loves a string
        String,

        // '#'
        TagMarker,

        // Command syntax ("<<foo>>")
        BeginCommand,
        EndCommand,

        // Variables ("$foo")
        Variable,

        // Shortcut syntax ("->")
        ShortcutOption,

        // Option syntax ("[[Let's go here|Destination]]")
        OptionStart, // [[
        OptionDelimit, // |
        OptionEnd, // ]]

        // Command types (specially recognised command word)
        If,
        ElseIf,
        Else,
        EndIf,
        Set,

        // Boolean values
        True,
        False,

        // The null value
        Null,

        // Parentheses
        LeftParen,
        RightParen,

        // Parameter delimiters
        Comma,

        // Operators
        EqualTo, // ==, eq, is
        GreaterThan, // >, gt
        GreaterThanOrEqualTo, // >=, gte
        LessThan, // <, lt
        LessThanOrEqualTo, // <=, lte
        NotEqualTo, // !=, neq

        // Logical operators
        Or, // ||, or
        And, // &&, and
        Xor, // ^, xor
        Not, // !, not

        // this guy's special because '=' can mean either 'equal to'
        // or 'becomes' depending on context
        EqualToOrAssign, // =, to

        UnaryMinus, // -; this is differentiated from Minus
                    // when parsing expressions

        Add, // +
        Minus, // -
        Multiply, // *
        Divide, // /
        Modulo, // %

        AddAssign, // +=
        MinusAssign, // -=
        MultiplyAssign, // *=
        DivideAssign, // /=

        Comment, // a run of text that we ignore

        Identifier, // a single word (used for functions)

        Text // a run of text until we hit other syntax
    }
    
    public class Compiler : YarnSpinnerParserBaseListener
    {

        

        internal struct CompileFlags
        {
            // should we emit code that turns (VAR_SHUFFLE_OPTIONS) off
            // after the next RunOptions OpCode?
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

        internal Compiler(Library library)
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
        void Emit(Node node, OpCode code, Operand operandA = null, Operand operandB = null)
        {
            var instruction = new Instruction();
            instruction.Opcode = code;

            // TODO: replace the operandA and operandB parameters with a list, so that we don't need to do this check
            // Can't have operand B be non-null while operand A is null
            if (operandA == null && operandB != null)
            {
                throw new ArgumentNullException("operandA", "operandA cannot be null while operandB is not null");
            }

            if (operandA != null) {
                instruction.Operands.Add(operandA);            
            }

            if (operandB != null) {
                instruction.Operands.Add(operandB);
            }

            
            node.Instructions.Add(instruction);

            if (code == OpCode.Label)
            {
                // Add this label to the label table
                node.Labels.Add((string)instruction.Operands[0].StringValue, node.Instructions.Count - 1);
            }
        }
        // exactly same as above but defaults to using currentNode
        // creates the relevant instruction and adds it to the stack
        internal void Emit(OpCode code, Operand operandA = null, Operand operandB = null)
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
            if (currentNode != null)
            {
                string newNode = context.header().header_title().TITLE_TEXT().GetText().Trim();
                string message = string.Format(CultureInfo.CurrentCulture, "Discovered a new node {0} while {1} is still being parsed", newNode, currentNode.Name);
				throw new Yarn.Compiler.ParseException(message);
            }
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

        // quick check to make sure we have the required number of headers
        // basically only allowed one of each tags, position, colourID
        // don't need to check for title because without it'll be a parse error
        public override void EnterHeader(YarnSpinnerParser.HeaderContext context)
        {
            if (context.header_tag().Length > 1)
            {
                string message = string.Format(CultureInfo.CurrentCulture, "Too many header tags defined inside {0}", context.header_title().TITLE_TEXT().GetText().Trim());
                throw new Yarn.Compiler.ParseException(message);
            }
        }
        // all we need to do is store the title as the name of the node
        public override void EnterHeader_title(YarnSpinnerParser.Header_titleContext context)
        {
            currentNode.Name = context.TITLE_TEXT().GetText().Trim();
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
            currentNode.Tags.Add(tags);
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
                Operand labelOperand = new Operand();
                labelOperand.StringValue = RegisterLabel();
                Emit(currentNode, OpCode.Label, labelOperand);
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

                currentNode.SourceTextStringID = program.RegisterString(body, currentNode.Name, "line:" + currentNode.Name, context.Start.Line, true);
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

                    if (flags.DisableShuffleOptionsAfterNextSet == true)
                    {
                        Emit(currentNode, OpCode.PushBool, new Operand(false));
                        Emit(currentNode, OpCode.StoreVariable, new Operand(VirtualMachine.SpecialVariables.ShuffleOptions));
                        Emit(currentNode, OpCode.Pop);
                        flags.DisableShuffleOptionsAfterNextSet = false;
                    }

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
            string num = compiler.program.RegisterString(lineText, compiler.currentNode.Name, lineID, lineNumber, true);

            compiler.Emit(OpCode.RunLine, new Operand(num));

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

                string stringID = compiler.program.RegisterString(label, compiler.currentNode.Name, lineID, lineNumber, true);
                compiler.Emit(OpCode.AddOption, new Operand(stringID), new Operand(destination));
            }
            else
            {
                string destination = context.OPTION_TEXT().GetText();
                compiler.Emit(OpCode.RunNode, new Operand(destination));
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
                compiler.Emit(OpCode.StoreVariable, new Operand(variableName));
                compiler.Emit(OpCode.Pop);
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
                    throw Yarn.Compiler.ParseException.Make(context,"Invalid expression inside assignment statement");
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
                    compiler.Emit(OpCode.Stop);
                    break;
                case "shuffleNextOptions":
                    compiler.Emit(OpCode.PushBool, new Operand(true));
                    compiler.Emit(OpCode.StoreVariable, new Operand(VirtualMachine.SpecialVariables.ShuffleOptions));
                    compiler.Emit(OpCode.Pop);
                    compiler.flags.DisableShuffleOptionsAfterNextSet = true;
                    break;
                default:
                    compiler.Emit(OpCode.RunCommand, new Operand(action));
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
                Yarn.Compiler.ParseException.Make(context, "Invalid number of parameters for " + functionName);
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
				compiler.Emit(OpCode.PushNumber, new Operand(parameters.Length));
			}

			// then call the function itself
			compiler.Emit(OpCode.CallFunc, new Operand(functionName));
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

            compiler.Emit(OpCode.Label, new Operand(endOfIfStatementLabel));

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
                compiler.Emit(OpCode.Label, new Operand(endOfClauseLabel));
                compiler.Emit(OpCode.Pop);
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

                    compiler.Emit(OpCode.JumpIfFalse, new Operand(endOfClauseLabel));
                }

                // getting the lineID from the hashtags if it has one
                string lineID = compiler.GetLineID(shortcut.hashtag_block());

                string shortcutLine = ShortcutText(shortcut.shortcut_text());
                string labelStringID = compiler.program.RegisterString(shortcutLine, compiler.currentNode.Name, lineID, shortcut.Start.Line, true);

                compiler.Emit(OpCode.AddOption, new Operand(labelStringID), new Operand(optionDestinationLabel));

                if (shortcut.shortcut_conditional() != null)
                {
                    compiler.Emit(OpCode.Label, new Operand(endOfClauseLabel));
                    compiler.Emit(OpCode.Pop);
                }
                optionCount++;
            }

            compiler.Emit(OpCode.ShowOptions);

            // TODO: investigate a cleaner way because this is odd...
            if (compiler.flags.DisableShuffleOptionsAfterNextSet == true)
            {
                compiler.Emit(OpCode.PushBool, new Operand(false));
                compiler.Emit(OpCode.StoreVariable, new Operand(VirtualMachine.SpecialVariables.ShuffleOptions));
                compiler.Emit(OpCode.Pop);
                compiler.flags.DisableShuffleOptionsAfterNextSet = false;
            }

            compiler.Emit(OpCode.Jump);

            optionCount = 0;
            foreach (var shortcut in context.shortcut())
            {
                compiler.Emit(OpCode.Label, new Operand(labels[optionCount]));

                // running through all the children statements of the shortcut
                foreach (var child in shortcut.statement())
                {
                    Visit(child);
                }

                compiler.Emit(OpCode.JumpTo, new Operand(endOfGroupLabel));

                optionCount++;
            }

            compiler.Emit(OpCode.Label, new Operand(endOfGroupLabel));
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
            compiler.Emit(OpCode.CallFunc, new Operand(TokenType.UnaryMinus.ToString()));

            return 0;
        }
        // (not NOT !)expression
        public override int VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            Visit(context.expression());

            // TODO: temp operator call
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
            float number = float.Parse(context.BODY_NUMBER().GetText(), CultureInfo.InvariantCulture);
            compiler.Emit(OpCode.PushNumber, new Operand(number));

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
            string stringVal = context.COMMAND_STRING().GetText().Trim('"');

            int lineNumber = context.Start.Line;
            string id = compiler.program.RegisterString(stringVal, compiler.currentNode.Name, null, lineNumber, false);
            compiler.Emit(OpCode.PushString, new Operand(id));

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
