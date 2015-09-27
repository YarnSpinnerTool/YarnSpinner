using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

// TODO: Shortcut options

namespace Yarn {


	// An exception representing something going wrong during parsing
	[Serializable]
	public class ParseException : Exception {

		public int lineNumber = 0;

		public static ParseException Make(Token foundToken, params TokenType[] expectedTypes) {

			var lineNumber = foundToken.lineNumber+1;



			string possibleValues = string.Join(",", expectedTypes);

			string message = string.Format("{0}:{1}: Expected {2}, but found {3}",
			                               lineNumber,
			                               foundToken.columnNumber,
			                               possibleValues,
			                               foundToken.type.ToString()
			                               );
			var e = new ParseException (message);
			e.lineNumber = lineNumber;
			return e;
		}

		public static ParseException Make(Token mostRecentToken, string message) {
			var lineNumber = mostRecentToken.lineNumber+1;
			string theMessage = string.Format ("{0}:{1}: {2}",
				                 lineNumber,
								mostRecentToken.columnNumber,
				                 message);
			var e = new ParseException (theMessage);
			e.lineNumber = lineNumber;
			return e;
		}

		// Some necessary constructors for the exception
		public ParseException () {}
		
		public ParseException (string message) : base(message) {}
		
		public ParseException (string message, Exception innerException) : base (message, innerException) {}
		
		protected ParseException (SerializationInfo info, StreamingContext context) : base (info, context) {}
	}


	// Magic abstract syntax tree producer - feed it tokens, and it gives you
	// a tree representation! Or an error!
	// TODO: actually useful parse errors
	public class Parser {



		// Indents the 'input' string 'indentLevel' times
		private static string Tab(int indentLevel, string input, bool newLine = true) {
			var sb = new StringBuilder();

			for (int i = 0; i < indentLevel; i++) {
				sb.Append ("| ");
			}
			sb.Append (input);

			if (newLine)
				sb.Append ("\n");
			
			return sb.ToString ();
		}

		#region Parse Nodes
		// Base class for nodes in th parse tree
		public abstract class ParseNode {

			private ParseNode parent;

			// ParseNodes do their parsing by consuming tokens from the Parser.
			// You parse tokens into a ParseNode by using its constructor.
			public ParseNode(ParseNode parent, Parser p) { this.parent = parent; }

			// Recursively prints the ParseNode and all of its child ParseNodes.
			public abstract string PrintTree (int indentLevel);

			public override string ToString ()
			{
				return PrintTree (0);
			}

			// The closest parent to this ParseNode that is a Node.
			public Node NodeParent() {
				var node = this;

				do {
					if (node is Node) {
						return node as Node;
					}
					node = node.parent;
				} while (node 
					!= null);					

				return null;
			}
		}

		// The top-level unit of parsing.
		// Node = (Statement)* EndOfInput
		public class Node : ParseNode {

			public string name { get; private set;}

			// Read-only public accessor for statements
			public IEnumerable<Statement> statements { get { return _statements; }}

			// The statements in this node
			List<Statement> _statements = new List<Statement> ();
			
			public Node(string name, ParseNode parent, Parser p) : base(parent, p) {
				this.name = name;
				// Consume statements until we run out of input or we hit a dedent
				while (p.tokens.Count > 0 && p.NextSymbolIs(TokenType.Dedent,TokenType.EndOfInput) == false) {
					_statements.Add(new Statement(this, p));
				}



			}

			// Print the statements we have
			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "Node "+name+" {"));
				foreach (var statement in _statements) {
					sb.Append( statement.PrintTree (indentLevel + 1));
				}
				sb.Append (Tab (indentLevel, "}"));
				return sb.ToString();
			}

		}

		// Statements are the items of execution in nodes.
		// Statement = Block
		// Statement = IfStatement
		// Statement = OptionStatement
		// Statement = ShortcutOptionGroup
		// Statement = <Text>
		// TODO: set statements
		// TODO: shortcut options
		public class Statement : ParseNode {

			public enum Type {
				CustomCommand,
				ShortcutOptionGroup,
				Block,
				IfStatement,
				OptionStatement,
				AssignmentStatement,
				Line
			}

			public Statement.Type type { get; private set; }

			// The possible types of statements we can have
			public Block block { get; private set;}
			public IfStatement ifStatement {get; private set;}
			public OptionStatement optionStatement {get; private set;}
			public AssignmentStatement assignmentStatement {get; private set;}
			public CustomCommand customCommand {get;private set;}
			public string line {get; private set;}

			public ShortcutOptionGroup shortcutOptionGroup { get; private set; }


			public Statement(ParseNode parent, Parser p) : base(parent, p) {

				if (Block.CanParse(p)) {
					type = Type.Block;
					block = new Block(this, p);
					return;
				} else if (IfStatement.CanParse(p)) {
					type = Type.IfStatement;
					ifStatement = new IfStatement(this, p);
					return;
				} else if (OptionStatement.CanParse(p)) {
					type = Type.OptionStatement;
					optionStatement = new OptionStatement(this, p);
					return;
				} else if (AssignmentStatement.CanParse(p)) {
					type = Type.AssignmentStatement;
					assignmentStatement = new AssignmentStatement(this, p);
					return;
				} else if (ShortcutOptionGroup.CanParse(p)) {
					type = Type.ShortcutOptionGroup;
					shortcutOptionGroup = new ShortcutOptionGroup(this, p);
					return;
				} else if (CustomCommand.CanParse(p)) {
					type = Type.CustomCommand;
					customCommand = new CustomCommand(this, p);
					return;
				}
				// It must be a basic line, then
				line = p.ExpectSymbol(TokenType.Text).value as string;
				type = Type.Line;

			}

			public override string PrintTree (int indentLevel)
			{
				switch (type) {
				case Type.Block:
					return block.PrintTree (indentLevel);
				case Type.IfStatement:
					return ifStatement.PrintTree (indentLevel);
				case Type.OptionStatement:
					return optionStatement.PrintTree (indentLevel);
				case Type.AssignmentStatement:
					return assignmentStatement.PrintTree (indentLevel);
				case Type.ShortcutOptionGroup:
					return shortcutOptionGroup.PrintTree (indentLevel);
				case Type.CustomCommand:
					return customCommand.PrintTree (indentLevel);
				case Type.Line:
					return Tab (indentLevel, "Line: "+ line);
				}

				throw new ArgumentNullException ();
			}

		}

		// Custom commands are meant to be interpreted by whatever
		// system that owns this dialogue sytem. eg <<stand>>
		// CustomCommand = BeginCommand <Text> EndCommand
		public class CustomCommand : ParseNode {
			public static bool CanParse (Parser p)
			{
				return p.NextSymbolsAre (TokenType.BeginCommand, TokenType.Text);
			}

			public string command { get; private set;}

			public CustomCommand(ParseNode parent, Parser p) : base(parent, p) {
				p.ExpectSymbol(TokenType.BeginCommand);
				command = p.ExpectSymbol(TokenType.Text).value as string;
				p.ExpectSymbol(TokenType.EndCommand);
			}

			public override string PrintTree (int indentLevel)
			{
				return Tab (indentLevel, "Command: " + command);
			}
		}

		// Shortcut option groups are groups of shortcut options,
		// followed by the node that they rejoin.
		// ShortcutOptionGroup = ShortcutOption+ Node
		public class ShortcutOptionGroup : ParseNode {
			public static bool CanParse (Parser p)
			{
				return p.NextSymbolIs (TokenType.ShortcutOption);
			}

			public IEnumerable<ShortcutOption> options { get { return _options; }}

			// The options in this group
			private List<ShortcutOption> _options = new List<ShortcutOption>();

			// The node that all options link back to - this is actually everything after the options
			public Node epilogue { get; private set; }

			public ShortcutOptionGroup(ParseNode parent, Parser p) : base(parent, p) {

				// keep parsing options until we can't, but expect at least one (otherwise it's
				// not actually a list of options)
				int shortcutIndex = 1; // give each option a number so it can name itself
				do {						
					_options.Add(new ShortcutOption(shortcutIndex++, this, p));
				} while (p.NextSymbolIs(TokenType.ShortcutOption));

				// finally parse everything after this option group as the epilogue
				epilogue = new Node(NodeParent().name+".Epilogue", this, p);
			}

			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "Shortcut option group {"));

				foreach (var option in options) {
					sb.Append (option.PrintTree (indentLevel + 1));
				}
				sb.Append (Tab (indentLevel, "} Epilogue {"));
				sb.Append (epilogue.PrintTree (indentLevel + 1));
				sb.Append (Tab (indentLevel, "}"));

				return sb.ToString ();
			}


		}

		// Shortcut options are a convenient way to define new options.
		// ShortcutOption = -> <text> [BeginCommand If Expression EndCommand] [Block]
		public class ShortcutOption : ParseNode {
			public string label { get; private set;}
			public Expression condition { get; private set;}
			public Node optionNode { get; private set;}

			public ShortcutOption(int optionIndex, ParseNode parent, Parser p) : base(parent, p) {
				p.ExpectSymbol(TokenType.ShortcutOption);
				label = p.ExpectSymbol(TokenType.Text).value as string;

				// Parse the conditional ("<<if $foo>>") if it's there
				if (p.NextSymbolIs(TokenType.BeginCommand)) {
					p.ExpectSymbol(TokenType.BeginCommand);
					p.ExpectSymbol(TokenType.If);
					condition = Expression.Parse(this, p);
					p.ExpectSymbol(TokenType.EndCommand);
				}

				// Parse the node if it's there
				try {
					var p1 = p.Fork();
					p1.ExpectSymbol(TokenType.Indent);
					optionNode = new Node(NodeParent().name + "." + optionIndex, this, p1);
					p1.ExpectSymbol(TokenType.Dedent);

					p.MergeWithParser(p1);
				} catch (ParseException) {}
			}

			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "Option \"" +label + "\""));

				if (condition != null) {
					sb.Append (Tab (indentLevel + 1, "(if"));
					sb.Append (condition.PrintTree(indentLevel+2));
					sb.Append (Tab (indentLevel + 1, ")"));
				}

				if (optionNode != null) {
					sb.Append (Tab (indentLevel, "{"));
					sb.Append (optionNode.PrintTree (indentLevel + 1));
					sb.Append (Tab (indentLevel, "}"));
				}

				return sb.ToString ();
			}


		}

		// Blocks are indented groups of statements
		// Block = Indent Statement* Dedent
		public class Block : ParseNode {
			public static bool CanParse (Parser p)
			{
				return p.NextSymbolIs (TokenType.Indent);
			}

			
			public IEnumerable<Statement> statements { get { return _statements; }}

			List<Statement> _statements = new List<Statement> ();

			public Block(ParseNode parent, Parser p) : base(parent, p) {

				// Read the indent token
				p.ExpectSymbol(TokenType.Indent);

				// Keep reading statements until we hit a dedent
				while (p.NextSymbolIs(TokenType.Dedent) == false) {
					// fun fact! because Blocks are a type of Statement,
					// we get nested block parsing for free! \:D/
					_statements.Add(new Statement(this, p));
				}

				// Tidy up by reading the dedent
				p.ExpectSymbol(TokenType.Dedent);

			}


			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab(indentLevel, "Block {"));
				foreach (var statement in _statements) {
					sb.Append (statement.PrintTree (indentLevel + 1));
				}
				sb.Append (Tab(indentLevel, "}"));

				return sb.ToString ();
			}
		}

		// Options are links to other nodes
		// OptionStatement = OptionStart <Text> OptionEnd
		// OptionStatement = OptionStart <Text> OptionDelimit <Text> OptionEnd
		public class OptionStatement : ParseNode {
			public static bool CanParse (Parser p)
			{
				return p.NextSymbolIs (TokenType.OptionStart);
			}

			public string destination { get; private set;}
			public string label { get; private set;}


			public OptionStatement(ParseNode parent, Parser p) : base(parent, p) {

				// The meaning of the string(s) we have changes
				// depending on whether we have one or two, so
				// keep them both and decide their meaning once
				// we know more

				string firstString;
				string secondString;

				// Parse [[Foo
				p.ExpectSymbol(TokenType.OptionStart);
				firstString = p.ExpectSymbol(TokenType.Text).value as String;

				// If there's a | in there, get the string that comes after it
				if (p.NextSymbolIs(TokenType.OptionDelimit)) {

					p.ExpectSymbol(TokenType.OptionDelimit);
					secondString = p.ExpectSymbol(TokenType.Text).value as String;

					// And now we know what the strings are!
					label = firstString;
					destination = secondString;
				} else {
					label = null;
					destination = firstString;
				}

				// Parse ]]
				p.ExpectSymbol(TokenType.OptionEnd);
			}


			public override string PrintTree (int indentLevel)
			{
				if (label != null) {
					return Tab (indentLevel, string.Format ("Option: \"{0}\" -> {1}", label, destination));
				} else {
					return Tab (indentLevel, string.Format ("Option: -> {0}", destination));
				}
			}
		}

		// If statements are the usual if-else-elseif-endif business.
		// If = BeginCommand If Expression EndCommand Statement* BeginCommand EndIf EndCommand
		// TODO: elseif
		public class IfStatement : ParseNode {
			public static bool CanParse (Parser p)
			{
				return p.NextSymbolsAre (TokenType.BeginCommand, TokenType.If);
			}

			public struct IfClause {
				public Expression expression;
				public IEnumerable<Statement> statements;
				public string PrintTree(int indentLevel) {
					var sb = new StringBuilder ();
					if (expression != null)
						sb.Append (expression.PrintTree (indentLevel));
					sb.Append (Tab (indentLevel, "{"));
					foreach (var statement in statements) {
						sb.Append (statement.PrintTree (indentLevel + 1));
					}
					sb.Append (Tab (indentLevel, "}"));
					return sb.ToString ();
				}
			}

			public IfClause primaryClause;
			public List<IfClause> elseIfClauses = new List<IfClause>(); // TODO: should be read-only
			public IfClause elseClause;

			public IList f;
			public IfStatement(ParseNode parent, Parser p) : base(parent, p) {
				
				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.If);

				primaryClause.expression = Expression.Parse(this, p);
				p.ExpectSymbol(TokenType.EndCommand);

				var statements = new List<Statement>();
				// Keep going until we hit an <<endif or <<else
				while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false &&
					p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else) == false &&
					p.NextSymbolsAre(TokenType.BeginCommand, TokenType.ElseIf) == false) {
					statements.Add(new Statement(this, p));
				}
				primaryClause.statements = statements;

				// Handle <<elseif 
				while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.ElseIf)) {

					var newElseClause = new IfClause();

					p.ExpectSymbol(TokenType.BeginCommand);
					p.ExpectSymbol(TokenType.ElseIf);
					newElseClause.expression = Expression.Parse(this, p);
					p.ExpectSymbol(TokenType.EndCommand);

					var elseStatements = new List<Statement>();
					while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false &&
						p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else) == false &&
						p.NextSymbolsAre(TokenType.BeginCommand, TokenType.ElseIf) == false) {
						statements.Add(new Statement(this, p));
					}

					newElseClause.statements = elseStatements;

						elseIfClauses.Add(newElseClause);
				}

				// Handle <<else>> if we have it
				if (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else, TokenType.EndCommand)) {
					p.ExpectSymbol(TokenType.BeginCommand);
					p.ExpectSymbol(TokenType.Else);
					p.ExpectSymbol(TokenType.EndCommand);

					var elseStatements = new List<Statement>();
					while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false) {
						elseStatements.Add(new Statement(this, p));
					}
					elseClause.statements = elseStatements;
				}

				// TODO: elseif

				// Tidy up by reading <<endif>>
				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.EndIf);
				p.ExpectSymbol(TokenType.EndCommand);


			}

			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "If:"));
				sb.Append (primaryClause.PrintTree (indentLevel + 1));
				foreach (var elseIf in elseIfClauses) {
					sb.Append (Tab (indentLevel, "Else If:"));
					sb.Append (elseIf.PrintTree (indentLevel + 1));
				}
				if (elseClause.statements != null) {
					sb.Append (Tab (indentLevel, "Else:"));
					sb.Append (elseClause.PrintTree (indentLevel + 1));
				}

				return sb.ToString ();
			}
		}

		// Raw values are either numbers, or variables.
		// TODO: values can be strings??
		// Value = <Number>
		// Value = <Variable>
		public class Value : ParseNode {

			public enum Type {
				Number,
				Variable
			}

			public Value.Type type { get; private set; }

			public float number {get; private set;}
			public string variableName {get; private set;}

			public Value(ParseNode parent, Token t) : base (parent, null) {
				UseToken(t);
			}

			private void UseToken(Token t) {
				// Store the value depending on type
				switch (t.type) {
				case TokenType.Number:
					type = Type.Number;
					number = float.Parse(t.value as String);
					break;
				case TokenType.Variable:
					type = Type.Variable;
					variableName = t.value as string;
					break;
				default:
					throw ParseException.Make (t, "Invalid token type " + t.ToString ());
				}
			}

			public Value(ParseNode parent, Parser p) : base(parent, p) {

				// Parse a number or a variable name
				Token t = p.ExpectSymbol(TokenType.Number, TokenType.Variable);

				UseToken(t);

			}

			public override string PrintTree (int indentLevel)
			{
				switch (type) {
				case Type.Number:
					return Tab (indentLevel, number.ToString());
				case Type.Variable:
					return Tab (indentLevel, variableName);
				}
				throw new ArgumentException ();

			}
		}

		// Expressions are things like "1 + 2 * 5 / 2 - 1"
		// Expression = Expression Operator Expression
		// Expression = Value
		// TODO: operator precedence; currently expressions are limited to nothing more
		// complex than "1 + 1"
		public class Expression : ParseNode {

			public enum Type {
				Value,
				Compound
			}

			public Type type;

			public Value value;
			// - or -
			public Expression leftHand;
			public Operator operation;
			public Expression rightHand;

			public Expression(ParseNode parent, Value value) : base(parent, null) {
				this.type = Type.Value;
				this.value = value;
			}

			public Expression(ParseNode parent, Expression lhs, Operator op, Expression rhs) : base(parent,null) {
				type = Type.Compound;
				leftHand = lhs;
				operation = op;
				rightHand = rhs;
			}

			public static Expression Parse(ParseNode parent, Parser p) {

				// Applies Djikstra's "shunting-yard" algorithm to convert the 
				// stream of infix expressions into postfix notation; we then
				// build a tree of expressions from the result

				// https://en.wikipedia.org/wiki/Shunting-yard_algorithm

				Queue<Token> _expressionRPN = new Queue<Token> ();
				var operatorStack = new Stack<Token>();

				var allValidTokenTypes = new List<TokenType>(Operator.operatorTypes);
				allValidTokenTypes.Add(TokenType.Number);
				allValidTokenTypes.Add(TokenType.Variable);
				allValidTokenTypes.Add(TokenType.LeftParen);
				allValidTokenTypes.Add(TokenType.RightParen);

				while (p.NextSymbolIs(allValidTokenTypes.ToArray())) {

					Token nextToken = p.ExpectSymbol(allValidTokenTypes.ToArray());

					if (nextToken.type == TokenType.Number ||
						nextToken.type == TokenType.Variable) {

						// Primitive values go straight onto the output
						_expressionRPN.Enqueue(nextToken);
						continue;

					} else if (Operator.IsOperator(nextToken.type)) {
						// This is an operator

						// O1 = this operator
						// O2 = the token at the top of the stack
						// While O2 is an operator, and EITHER: 1. O1 is left-associative and
						// has precedence <= O2, or 2. O1 is right-associative and
						// has precedence > O2:
						while (ShouldApplyPrecedence(nextToken.type, operatorStack)) {
							var o = operatorStack.Pop();
							_expressionRPN.Enqueue(o);
						}
						operatorStack.Push(nextToken);

					} else if (nextToken.type == TokenType.LeftParen) {
						operatorStack.Push(nextToken);
						
					} else if (nextToken.type == TokenType.RightParen) {

						try {
							// pop operators until we reach a left paren
							while (operatorStack.Peek().type != TokenType.LeftParen) {
								_expressionRPN.Enqueue(operatorStack.Pop());
							}
							// pop the left paren
							operatorStack.Pop();
						} catch (InvalidOperationException) {
							// we reached the end of the stack prematurely
							throw ParseException.Make(nextToken, "Unbalanced parameters");
						}

					}

				}

				// No more tokens; pop all operators onto the output queue
				while (operatorStack.Count > 0) {
					_expressionRPN.Enqueue(operatorStack.Pop());
				}

				// If the output queue is empty, then this is not an expression
				if (_expressionRPN.Count == 0) {
					throw new ParseException ("Error parsing expression");
				}

				// We've now got this in more easily parsed RPN form; 
				// time to build the expression tree.
				Token firstToken = _expressionRPN.Peek();
				var evaluationStack = new Stack<Expression>();
				while (_expressionRPN.Count > 0) {

					var next = _expressionRPN.Dequeue();
					if (Operator.IsOperator(next.type)) {

						var info = Operator.InfoForOperator(next.type);
						if (evaluationStack.Count < info.arguments) {
							throw ParseException.Make(next, "Error parsing exception");
						}
						Expression lhs = null, rhs = null;

						if (info.arguments == 1) {
							rhs = evaluationStack.Pop();
						} else if (info.arguments == 2) {
							rhs = evaluationStack.Pop();
							lhs = evaluationStack.Pop();
						} else {
							string error = string.Format("Unsupported number of parameters for operator {0} ({1})",
								next.type.ToString(),
								info.arguments
							);
							throw ParseException.Make(next, error);
						}
						var expr = new Expression(parent, lhs, new Operator(parent, next.type), rhs);
						evaluationStack.Push(expr);
					} else {
						Value v = new Value(parent, next);
						Expression expr = new Expression(parent, v);
						evaluationStack.Push(expr);

					}
				}
				// We should now have a single expression in this stack
				if (evaluationStack.Count != 1) {
					throw ParseException.Make(firstToken, "Error parsing exception");
				}

				// Return it
				return evaluationStack.Pop ();


			}

			private static bool ShouldApplyPrecedence(TokenType o1, Stack<Token> operatorStack) {
				
				if (operatorStack.Count == 0) {
					return false;
				}
				if (Operator.IsOperator (o1) == false) {
					throw new ParseException ("Internal error parsing expression");
				}
				TokenType o2 = operatorStack.Peek ().type;

				if (Operator.IsOperator (o2) == false)
					return false;

				var o1Info = Operator.InfoForOperator (o1);
				var o2Info = Operator.InfoForOperator (o2);

				if (o1Info.associativity == Operator.Associativity.Left && o1Info.precedence <= o2Info.precedence) {
					return true;
				}

				if (o1Info.associativity == Operator.Associativity.Right && o1Info.precedence < o2Info.precedence) {
					return true;
				}

				return false;

			
			}


			public override string PrintTree (int indentLevel)
			{
				
				switch (type) {
				case Type.Value:
					return value.PrintTree (indentLevel);
				case Type.Compound:
					var stringBuilder = new StringBuilder ();

					stringBuilder.Append (Tab (indentLevel, "Expression: {"));

					if (leftHand != null)
						stringBuilder.Append (leftHand.PrintTree (indentLevel + 1));

					stringBuilder.Append (operation.PrintTree (indentLevel + 1));

					if (rightHand != null)
						stringBuilder.Append (rightHand.PrintTree (indentLevel + 1));
					
					stringBuilder.Append (Tab (indentLevel, "}"));

					return stringBuilder.ToString ();
				}

				return Tab(indentLevel, "<error printing expression!>");
			}
		}

		// AssignmentStatements are things like <<set $foo = 1>>
		// AssignmentStatement = BeginCommand Set <variable> <operation> Expression EndCommand
		public class AssignmentStatement : ParseNode {
			public static bool CanParse (Parser p)
			{
				return p.NextSymbolsAre (TokenType.BeginCommand, TokenType.Set);
			}

			public string destinationVariableName { get; private set; }

			public Expression valueExpression { get; private set; }

			public TokenType operation { get; private set; }

			private static TokenType[] validOperators = {
				TokenType.EqualToOrAssign,
				TokenType.AddAssign,
				TokenType.MinusAssign,
				TokenType.DivideAssign,
				TokenType.MultiplyAssign
			};

			public AssignmentStatement(ParseNode parent, Parser p) : base(parent, p) {

				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.Set);
				destinationVariableName = p.ExpectSymbol(TokenType.Variable).value as string;
				operation = p.ExpectSymbol(validOperators).type;
				valueExpression = Expression.Parse(this, p);
				p.ExpectSymbol(TokenType.EndCommand);

			}

			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab(indentLevel, "Set:"));
				sb.Append (Tab(indentLevel+1, destinationVariableName));
				sb.Append (Tab (indentLevel+1,  operation.ToString()));
				sb.Append (valueExpression.PrintTree (indentLevel + 1));
				return sb.ToString ();

			}
		}

		// Operators are used in expressions - things like + - / * != neq
		public class Operator : ParseNode {
			public TokenType operatorType { get; private set; }

			public enum Associativity {
				Left,
				Right,
				None
			}

			public struct OperatorInfo {
				public Associativity associativity;
				public int precedence;
				public int arguments;
				public OperatorInfo(Associativity associativity, int precedence, int arguments) {
					this.associativity = associativity;
					this.precedence = precedence;
					this.arguments = arguments;
				}
			} 

			public static OperatorInfo InfoForOperator(TokenType op) {
				if (Array.IndexOf(operatorTypes, op) == -1) {
					throw new ParseException (op.ToString () + " is not a valid operator");
				}

				switch (op) {
				case TokenType.Multiply:
				case TokenType.Divide:
					return new OperatorInfo(Associativity.Left, 20,2);
				case TokenType.Add:
				case TokenType.Minus:
					return new OperatorInfo(Associativity.Left, 15,2);
				case TokenType.GreaterThan:
				case TokenType.LessThan:
				case TokenType.GreaterThanOrEqualTo:
				case TokenType.LessThanOrEqualTo:
					return new OperatorInfo(Associativity.Left, 10,2);
				case TokenType.EqualTo:
				case TokenType.EqualToOrAssign:
				case TokenType.NotEqualTo:
					return new OperatorInfo(Associativity.Left, 5,2);
				case TokenType.And:
					return new OperatorInfo(Associativity.Left, 4,2);
				case TokenType.Or:
					return new OperatorInfo(Associativity.Left, 3,2);
				case TokenType.Xor:
					return new OperatorInfo(Associativity.Left, 2,2);

				}
				throw new InvalidOperationException ();
				
			}

			public static bool IsOperator(TokenType type) {
				return Array.IndexOf (operatorTypes, type) != -1;
			}

			public static  TokenType[] operatorTypes {
				get {
					return new TokenType[] {
						TokenType.Add,
						TokenType.Minus,
						TokenType.Divide,
						TokenType.Multiply,

						TokenType.EqualToOrAssign,
						TokenType.EqualTo,
						TokenType.GreaterThan,
						TokenType.GreaterThanOrEqualTo,
						TokenType.LessThan,
						TokenType.LessThanOrEqualTo,
						TokenType.NotEqualTo,

						TokenType.And,
						TokenType.Or,
						TokenType.Not,
						TokenType.Xor
												
					};
				}
			}

			public Operator(ParseNode parent, TokenType t) : base(parent, null) {
				operatorType = t;
			}

			public Operator(ParseNode parent, Parser p) : base(parent, p) {
				operatorType = p.ExpectSymbol(Operator.operatorTypes).type;
			}

			public override string PrintTree (int indentLevel)
			{
				return Tab (indentLevel, operatorType.ToString ());
			}
		}
		#endregion Parse Nodes

		// Use a queue since we're continuously consuming them as 
		// we parse
		Queue<Token> tokens;

		// Take whatever we were given and make a queue out of it
		public Parser(ICollection<Token> tokens) {
			this.tokens = new Queue<Token>(tokens);
		}

		public Node Parse() {

			// Kick off the parsing process by trying to parse a whole node
			return new Node("Start", null, this);
		}

		// Returns true if the next symbol is one of 'validTypes'
		bool NextSymbolIs(params TokenType[] validTypes) {
			var t = this.tokens.Peek().type;

			foreach (var validType in validTypes) {
				if (t == validType) {
					return true;
				}
			}
			return false;
		}

		// Returns true if the next symbols are of the same type as
		// 'validTypes' - this is used to look further ahead in the 
		// token stream, eg when we're looking for '<<' 'else' 
		bool NextSymbolsAre(params TokenType[] validTypes) {
			var tempQueue = new Queue<Token> (tokens);
			foreach (var type in validTypes) {
				if (tempQueue.Dequeue ().type != type)
					return false;
			}
			return true;
		}

		// Return the next token, which must be of type 'type',
		// or throw an exception
		Token ExpectSymbol(TokenType type) {
			var t = this.tokens.Dequeue();
			if (t.type != type) {

				throw ParseException.Make(t, type);
			}
			return t;
		}

		// Return the next token, which must be one of 'validTypes',
		// or throw an exception
		Token ExpectSymbol(params TokenType[] validTypes) {
			var t = this.tokens.Dequeue();

			foreach (var validType in validTypes) {
				if (t.type == validType) {
					return t;
				}
			}

			throw ParseException.Make(t, validTypes);
		}

		// The next two methods allow us to backtrack.
		// To speculatively parse some grammar, you
		// use Fork() to copy the current state of the 
		// parser, try to parse using this new temp parser, 
		// and if you catch a ParseException, no harm done.
		// But if you DON'T, then parsing succeeded, so use
		// MergeWithParser to grab the temporary parser's state
		// so you can carry from that point

		// Create a copy of ourselves in our current state
		Parser Fork() {
			return new Parser (this.tokens.ToArray());
		}
			
		// Take this other parser's state and use that
		void MergeWithParser(Parser p) {
			this.tokens = p.tokens;
		}

	}


}


