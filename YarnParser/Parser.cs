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
			public string line {get; private set;}

			public ShortcutOptionGroup shortcutOptionGroup { get; private set; }


			public Statement(ParseNode parent, Parser p) : base(parent, p) {

				// No? Try to parse a block
				try {
					var p1 = p.Fork();
					block = new Block(this, p1);
					p.MergeWithParser(p1);
					type = Type.Block;
					return;
				} catch (Yarn.ParseException) {}

				// Try to parse an if statement
				try {
					var p1 = p.Fork();
					ifStatement = new IfStatement(this, p1);
					p.MergeWithParser(p1);
					type = Type.IfStatement;
					return;
				} catch (Yarn.ParseException) {}

				// Try to parse an option
				try {
					var p1 = p.Fork();
					optionStatement = new OptionStatement(this, p1);
					p.MergeWithParser(p1);
					type = Type.OptionStatement;

					return;
				} catch (Yarn.ParseException) {}

				// Try to parse an assignment
				try {
					var p1 = p.Fork();
					assignmentStatement = new AssignmentStatement(this, p1);
					p.MergeWithParser(p1);
					type = Type.AssignmentStatement;
					return;
				} catch (Yarn.ParseException) {}

				// Try to parse a shortcut option group
				try {
					var p1 = p.Fork();
					shortcutOptionGroup = new ShortcutOptionGroup(this, p1);
					p.MergeWithParser(p1);
					type = Type.ShortcutOptionGroup;
					return;
				} catch (Yarn.ParseException) {}



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
				case Type.Line:
					return Tab (indentLevel, "Line: "+ line);
				}

				throw new ArgumentNullException ();
			}

		}

		// Shortcut option groups are groups of shortcut options,
		// followed by the node that they rejoin.
		// ShortcutOptionGroup = ShortcutOption+ Node
		public class ShortcutOptionGroup : ParseNode {
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
				sb.Append (Tab (indentLevel, "Option group {"));

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
					condition = new Expression(this, p);
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
			public Expression expression { get; private set; }
			
			public IEnumerable<Statement> statements { get; private set; }
			public IEnumerable<Statement> elseStatements { get; private set; }
			
			public IList f;
			public IfStatement(ParseNode parent, Parser p) : base(parent, p) {

				List<Statement> statements = new List<Statement>();
				List<Statement> elseStatements = new List<Statement>();


				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.If);
				expression = new Expression(this, p);
				p.ExpectSymbol(TokenType.EndCommand);

				// Keep going until we hit an <<endif or <<else
				while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false &&
						p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else) == false) {
					statements.Add(new Statement(this, p));
				}

				// Handle <<else>> if we have it
				if (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else)) {
					p.ExpectSymbol(TokenType.BeginCommand);
					p.ExpectSymbol(TokenType.Else);
					p.ExpectSymbol(TokenType.EndCommand);

					while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false) {
						elseStatements.Add(new Statement(this, p));
					}
				}

				// TODO: elseif

				// Tidy up by reading <<endif>>
				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.EndIf);
				p.ExpectSymbol(TokenType.EndCommand);

				this.statements = statements;
				this.elseStatements = elseStatements;


			}

			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "If:"));
				sb.Append (expression.PrintTree (indentLevel + 1));
				sb.Append (Tab (indentLevel, "Then:"));
				foreach (var statement in statements) {
					sb.Append (statement.PrintTree (indentLevel + 1));
				}
				if ((elseStatements as IList).Count > 0) {
					sb.Append (Tab (indentLevel, "Else:"));
					foreach (var statement in elseStatements) {
						sb.Append (statement.PrintTree (indentLevel + 1));
					}
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

			public Value(ParseNode parent, Parser p) : base(parent, p) {

				// Parse a number or a variable name
				Token t = p.ExpectSymbol(TokenType.Number, TokenType.Variable);

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
				}

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
				PrimitiveValue,
				Expression
			}

			public Type type { get; private set; }

			public Value value { get; private set;}
			
			public Value leftHand { get; private set; }
			public Operator exprOperator { get; private set; }
			public Value rightHand { get; private set; }

			public Expression(ParseNode parent, Parser p) : base(parent, p) {

				Value v = new Value(this, p);


				if (p.NextSymbolIs(Operator.validTokens)) {
					leftHand = v;
					exprOperator = new Operator(this, p);
					rightHand = new Value(this, p);
					type = Type.Expression;
				} else {
					value = v;
					type = Type.PrimitiveValue;
				}

			}

			public override string PrintTree (int indentLevel)
			{
				var stringBuilder = new StringBuilder ();

				stringBuilder.Append (Tab (indentLevel, "Expression: {"));

				switch (type) {
				case Expression.Type.PrimitiveValue:
					stringBuilder.Append (value.PrintTree (indentLevel + 1));
					break;
				case Expression.Type.Expression:
					stringBuilder.Append (leftHand.PrintTree (indentLevel + 1));
					stringBuilder.Append (exprOperator.PrintTree (indentLevel + 1));
					stringBuilder.Append (rightHand.PrintTree (indentLevel + 1));
					stringBuilder.Append (Tab (indentLevel, "}"));
					break;
				}



				return stringBuilder.ToString ();
			}
		}

		// AssignmentStatements are things like <<set $foo = 1>>
		// AssignmentStatement = BeginCommand Set <variable> <operation> Expression EndCommand
		public class AssignmentStatement : ParseNode {
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
				valueExpression = new Expression(this, p);
				p.ExpectSymbol(TokenType.EndCommand);

			}

			public override string PrintTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab(indentLevel, string.Format("Set {0} {1}", destinationVariableName, operation.ToString())));
				sb.Append (valueExpression.PrintTree (indentLevel + 1));
				return sb.ToString ();

			}
		}

		// Operators are used in expressions - things like + - / * != neq
		public class Operator : ParseNode {
			public TokenType operatorType { get; private set; }

			public static  TokenType[] validTokens {
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

						TokenType.Add,
						TokenType.Or,
						TokenType.Not,
						TokenType.Xor
												
					};
				}
			}

			public Operator(ParseNode parent, Parser p) : base(parent, p) {
				operatorType = p.ExpectSymbol(Operator.validTokens).type;
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


