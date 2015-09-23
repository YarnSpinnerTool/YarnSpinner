using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Yarn {


	// An exception representing something going wrong during parsing
	[Serializable]
	public class ParseException : Exception {

		public static ParseException Make(Token foundToken, params TokenType[] expectedTypes) {

			string possibleValues = string.Join(",", expectedTypes);

			string message = string.Format("{0}:{1}: Expected {2}, but found {3}",
			                               foundToken.lineNumber,
			                               foundToken.columnNumber,
			                               possibleValues,
			                               foundToken.type.ToString()
			                               );
			return new ParseException(message);
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

			// ParseNodes do their parsing by consuming tokens from the Parser.
			// You parse a piece of grammar using its constructor.
			public ParseNode(Parser p) { }

			// Recursively prints the ParseNode, and all of its child ParseNodes.
			public abstract string DumpTree (int indentLevel);

			public override string ToString ()
			{
				return DumpTree (0);
			}
		}

		// The top-level unit of parsing.
		// Node = (Statement)* EndOfInput
		public class Node : ParseNode {

			// The statements in this node
			List<Statement> statements = new List<Statement> ();
			
			public Node(Parser p) : base(p) {
				// Consume statements until we run out of input
				while (p.NextSymbolIs(TokenType.EndOfInput) == false) {
					statements.Add(new Statement(p));
				}
				p.ExpectSymbol(TokenType.EndOfInput);
			}

			// Dump our statements
			public override string DumpTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "Node: {"));
				foreach (var statement in statements) {
					sb.Append( Tab(indentLevel, statement.DumpTree (indentLevel + 1), newLine: false));
				}
				sb.Append (Tab (indentLevel, "}", newLine: false));
				return sb.ToString();
			}

		}

		// Statements are the items of execution in nodes.
		// Statement = Expression
		// Statement = Block
		// Statement = IfStatement
		// Statement = OptionStatement
		// Statement = <Text>
		// TODO: other statements
		public class Statement : ParseNode {
			// The possible types of statements we can have
			Expression expression;
			Block block;
			IfStatement ifStatement;
			OptionStatement optionStatement;
			Token line;

			public Statement(Parser p) : base(p) {

				// Try and parse an expression
				try {
					var p1 = p.Fork();
					expression = new Expression(p1);
					p.MergeWithParser(p1);
					return;
				} catch (Yarn.ParseException) {}

				// No? Then try to parse a block
				try {
					var p1 = p.Fork();
					block = new Block(p1);
					p.MergeWithParser(p1);
					return;
				} catch (Yarn.ParseException) {}

				// Try to parse an if statement
				try {
					var p1 = p.Fork();
					ifStatement = new IfStatement(p1);
					p.MergeWithParser(p1);
					return;
				} catch (Yarn.ParseException) {}

				// Try to parse an option
				try {
					var p1 = p.Fork();
					optionStatement = new OptionStatement(p1);
					p.MergeWithParser(p1);
					return;
				} catch (Yarn.ParseException) {}

				// It must be a basic line, then
				line = p.ExpectSymbol(TokenType.Text);


			}

			public override string DumpTree (int indentLevel)
			{
				if (expression != null) {
					return expression.DumpTree (indentLevel);
				} else if (block != null) {
					return block.DumpTree (indentLevel);
				} else if (ifStatement != null) {
					return ifStatement.DumpTree (indentLevel);
				} else if (optionStatement != null) {
					return optionStatement.DumpTree (indentLevel);
				} else if (line != null) {
					return Tab (indentLevel, line.ToString ());
				}
				throw new ArgumentNullException ();
			}

		}

		// Blocks are indented groups of statements
		// Block = Indent Statement* Dedent
		public class Block : ParseNode {
			List<Statement> statements = new List<Statement> ();

			public Block(Parser p) : base(p) {

				// Read the indent token
				p.ExpectSymbol(TokenType.Indent);

				// Keep reading statements until we hit a dedent
				while (p.NextSymbolIs(TokenType.Dedent) == false) {
					// fun fact! because Blocks are a type of Statement,
					// we get nested block parsing for free! \:D/
					statements.Add(new Statement(p));
				}

				// Tidy up by reading the dedent
				p.ExpectSymbol(TokenType.Dedent);

			}


			public override string DumpTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab(indentLevel, "Block {"));
				foreach (var statement in statements) {
					sb.Append (statement.DumpTree (indentLevel + 1));
				}
				sb.Append (Tab(indentLevel, "}"));

				return sb.ToString ();
			}
		}

		// Options are links to other nodes
		// OptionStatement = OptionStart <Text> OptionEnd
		// OptionStatement = OptionStart <Text> OptionDelimit <Text> OptionEnd
		public class OptionStatement : ParseNode {
			string destination;
			string label;

			public OptionStatement(Parser p) : base(p) {

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


			public override string DumpTree (int indentLevel)
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
			Expression expression;
			List<Statement> statements = new List<Statement>();
			List<Statement> elseStatements = new List<Statement>();

			public IfStatement(Parser p) : base(p) {

				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.If);
				expression = new Expression(p);
				p.ExpectSymbol(TokenType.EndCommand);

				// Keep going until we hit an <<endif or <<else
				while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false &&
						p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else) == false) {
					statements.Add(new Statement(p));
				}

				// Handle <<else>> if we have it
				if (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.Else)) {
					p.ExpectSymbol(TokenType.BeginCommand);
					p.ExpectSymbol(TokenType.Else);
					p.ExpectSymbol(TokenType.EndCommand);

					while (p.NextSymbolsAre(TokenType.BeginCommand, TokenType.EndIf) == false) {
						elseStatements.Add(new Statement(p));
					}
				}

				// TODO: elseif

				// Tidy up by reading <<endif>>
				p.ExpectSymbol(TokenType.BeginCommand);
				p.ExpectSymbol(TokenType.EndIf);
				p.ExpectSymbol(TokenType.EndCommand);

			}

			public override string DumpTree (int indentLevel)
			{
				var sb = new StringBuilder ();
				sb.Append (Tab (indentLevel, "If:"));
				sb.Append (expression.DumpTree (indentLevel + 1));
				sb.Append (Tab (indentLevel, "Then:"));
				foreach (var statement in statements) {
					sb.Append (statement.DumpTree (indentLevel + 1));
				}
				if (elseStatements.Count > 0) {
					sb.Append (Tab (indentLevel, "Else:"));
					foreach (var statement in elseStatements) {
						sb.Append (statement.DumpTree (indentLevel + 1));
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

			TokenType type;
			float number;
			string variable;

			public Value(Parser p) : base(p) {

				// Parse a number or a variable name
				Token t = p.ExpectSymbol(TokenType.Number, TokenType.Variable);

				type = t.type;

				// Store the value depending on type
				switch (t.type) {
				case TokenType.Number:
					number = float.Parse(t.value as String);
					break;
				case TokenType.Variable:
					variable = t.value as string;
					break;
				}

			}

			public override string DumpTree (int indentLevel)
			{
				switch (type) {
				case TokenType.Number:
					return Tab (indentLevel, number.ToString());
				case TokenType.Variable:
					return Tab (indentLevel, variable);
				}
				throw new ArgumentException ();

			}
		}

		// Expressions are things like "1 + 2 * 5 / 2 - 1"
		// Expression = Value Operator Value
		// TODO: Expression = Expression Operator Expression
		// TODO: Expression = Value
		// TODO: operator precedence; currently expressions are limited to nothing more
		// complex than "1 + 1"
		public class Expression : ParseNode {
			
			Value leftHand;
			Operator exprOperator;
			Value rightHand;

			public Expression(Parser p) : base(p) {

				leftHand = new Value(p);
				exprOperator = new Operator(p);
				rightHand = new Value(p);

			}

			public override string DumpTree (int indentLevel)
			{
				var stringBuilder = new StringBuilder ();

				stringBuilder.Append (Tab (indentLevel, "Expression: {"));
				stringBuilder.Append (leftHand.DumpTree(indentLevel+1));
				stringBuilder.Append (exprOperator.DumpTree(indentLevel+1));
				stringBuilder.Append (rightHand.DumpTree(indentLevel+1));
				stringBuilder.Append (Tab (indentLevel, "}"));

				return stringBuilder.ToString ();
			}
		}

		// Operators are used in expressions - things like + - / * != neq
		public class Operator : ParseNode {
			TokenType operatorType;

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
						
						TokenType.AddAssign,
						TokenType.MinusAssign,
						TokenType.DivideAssign,
						TokenType.MultiplyAssign,						
					};
				}
			}

			public Operator(Parser p) : base(p) {
				operatorType = p.ExpectSymbol(Operator.validTokens).type;
			}

			public override string DumpTree (int indentLevel)
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
			return new Node(this);
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

		// Do we have tokens left?
		bool HasTokensRemaining {
			get {
				return this.tokens.Count > 0;
			}
		}

		// The next two methods allow us to backtrack - 
		// to speculatively parse some grammar, you
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