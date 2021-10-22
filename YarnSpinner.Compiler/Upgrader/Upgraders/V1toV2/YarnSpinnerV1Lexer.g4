lexer grammar YarnSpinnerV1Lexer;

tokens { INDENT, DEDENT }
 
@lexer::header{
using System.Linq;
using System.Text.RegularExpressions;
}

@lexer::members {
	// A queue where extra tokens are pushed on (see the NEWLINE lexer rule).
	private System.Collections.Generic.LinkedList<IToken> Tokens = new System.Collections.Generic.LinkedList<IToken>();
	// The stack that keeps track of the indentation level.
	private System.Collections.Generic.Stack<int> Indents = new System.Collections.Generic.Stack<int>();
	// The amount of opened braces, brackets and parenthesis.
	private int Opened = 0;
	// The most recently produced token.
	private IToken LastToken = null;

	public override void Emit(IToken token)
    {
        base.Token = token;
        Tokens.AddLast(token);
    }

    private CommonToken CommonToken(int type, string text)
    {
        int stop = CharIndex - 1;
        int start = text.Length == 0 ? stop : stop - text.Length + 1;
        var tokenFactorySourcePair = Tuple.Create((ITokenSource)this, (ICharStream)InputStream);
        return new CommonToken(tokenFactorySourcePair, type, DefaultTokenChannel, start, stop);
    }

	private IToken CreateDedent()
	{
	    var dedent = CommonToken(YarnSpinnerParser.DEDENT, "");
	    dedent.Line = LastToken.Line;
	    return dedent;
	}

	public override IToken NextToken()
	{
	    // Check if the end-of-file is ahead and there are still some DEDENTS expected.
	    if (InputStream.LA(1) == Eof && Indents.Count != 0)
	    {
            // Remove any trailing EOF tokens from our buffer.
            for (var node  = Tokens.First; node != null; )
            {
                var temp = node.Next;
                if (node.Value.Type == Eof)
                {
                    Tokens.Remove(node);
                }
                node = temp;
            }
            
            // First emit an extra line break that serves as the end of the statement.
            this.Emit(CommonToken(YarnSpinnerParser.NEWLINE, "\n"));

	        // Now emit as much DEDENT tokens as needed.
	        while (Indents.Count != 0)
	        {
	            Emit(CreateDedent());
	            Indents.Pop();
	        }

	        // Put the EOF back on the token stream.
	        Emit(CommonToken(YarnSpinnerParser.Eof, "<EOF>"));
	    }

	    var next = base.NextToken();
	    if (next.Channel == DefaultTokenChannel)
	    {
	        // Keep track of the last token on the default channel.
	        LastToken = next;
	    }

	    if (Tokens.Count == 0)
	    {
	        return next;
	    }
	    else
	    {
	        var x = Tokens.First.Value;
	        Tokens.RemoveFirst();
	        return x;
	    }
	}

    // Calculates the indentation of the provided spaces, taking the
    // following rules into account:
    //
    // "Tabs are replaced (from left to right) by one to eight spaces
    //  such that the total number of characters up to and including
    //  the replacement is a multiple of eight [...]"
    //
    //  -- https://docs.python.org/3.1/reference/lexical_analysis.html#indentation
    static int GetIndentationCount(string spaces)
    {
        int count = 0;
        foreach (char ch in spaces.ToCharArray())
        {
            count += ch == '\t' ? 8 - (count % 8) : 1;
        }
        return count;
    }

    bool AtStartOfInput()
    {
        return Column == 0 && Line == 1;
    }

    void CreateIndentIfNeeded(int type = NEWLINE) {

        var newLine = (new Regex("[^\r\n\f]+")).Replace(Text, "");
		var spaces = (new Regex("[\r\n\f]+")).Replace(Text, "");
		// Strip newlines inside open clauses except if we are near EOF. We keep NEWLINEs near EOF to
		// satisfy the final newline needed by the single_put rule used by the REPL.
		int next = InputStream.LA(1);
		int nextnext = InputStream.LA(2);
        
        // '-1' indicates 'do not emit the newline here but do emit indents/dedents'
        if (type != -1) {
            Emit(CommonToken(type, newLine));
        }
        int indent = GetIndentationCount(spaces);
        int previous = Indents.Count == 0 ? 0 : Indents.Peek();
        if (indent == previous)
        {
            // skip indents of the same size as the present indent-size            
        }
        else if (indent > previous) {
            Indents.Push(indent);
            Emit(CommonToken(YarnSpinnerParser.INDENT, spaces));
        }
        else {
            // Possibly emit more than 1 DEDENT token.
            while(Indents.Count != 0 && Indents.Peek() > indent)
            {
                this.Emit(CreateDedent());
                Indents.Pop();
            }        
		}
    }
}

// Root mode: skip whitespaces, set up some commonly-seen 
// tokens
WS : ([ \t])+ -> skip;

fragment SPACES: [ \t]+ ; // used in NEWLINE tokens to calculate the text following a newline

// Some commonly-seen tokens that other lexer modes will use
NEWLINE: [\r\n]+ SPACES? { CreateIndentIfNeeded(-1); } -> skip;

ID : [a-zA-Z_](([a-zA-Z0-9])|('_'))* ;
fragment NODE_ID : [a-zA-Z_](([a-zA-Z0-9])|('_'|'.'))* ;

// The 'end of node headers, start of node body' marker
BODY_START : '---' -> pushMode(BodyMode) ;

// The ':' in 'foo: bar (plus any whitespace after it)
HEADER_DELIMITER : ':' [ ]* -> pushMode(HeaderMode);

// A hashtag. These can appear at the start of a file, or after 
// certain lines (see BODY_HASHTAG rule)
HASHTAG : '#' -> pushMode(HashtagMode);

// Headers before a node.
mode HeaderMode;
// Allow arbitrary text up to the end of the line.
REST_OF_LINE : ~('\r'|'\n')+;
HEADER_NEWLINE : NEWLINE SPACES? {CreateIndentIfNeeded(HEADER_NEWLINE);} -> popMode;

COMMENT: '//' REST_OF_LINE -> skip;

// The main body of a node.
mode BodyMode;

// Ignore all whitespace and comments
BODY_WS : WS -> skip;
BODY_NEWLINE : NEWLINE SPACES? {CreateIndentIfNeeded(-1);} -> skip;
BODY_COMMENT : COMMENT -> skip ;

// End of this node; return to global mode (eg node headers)
// or end of file
BODY_END : '===' -> popMode; 

// The start of a shortcut option
SHORTCUT_ARROW : '->' ;

// The start of a command
COMMAND_START: '<<' -> pushMode(CommandMode) ;

// The start of an option or jump
OPTION_START: '[[' -> pushMode(OptionMode) ;

FORMAT_FUNCTION_START: '[' -> pushMode(TextMode), pushMode(FormatFunctionMode);

// The start of a hashtag. Can goes at the end of the 
// line, but this rule allows us to capture '#' at the start 
// of a line, or following an Option.
BODY_HASHTAG: '#' -> pushMode(TextCommandOrHashtagMode), pushMode(HashtagMode);

// The start of an inline expression. Immediately lex as 
// TEXT_EXPRESSION_START and push into TextMode  and 
// ExpressionMode.
BODY_EXPRESSION_FUNCTION_START: '{' -> type(TEXT_EXPRESSION_START), pushMode(TextMode), pushMode(ExpressionMode);

// The start of a format function. Immediately lex as 
// TEXT_FORMAT_FUNCTION_START and push into TextMode 
// and ExpressionMode.
BODY_FORMAT_FUNCTION_START: '[' -> type(TEXT_FORMAT_FUNCTION_START), pushMode(TextMode), pushMode(FormatFunctionMode);


// Any other text means this is a Line
ANY: . -> more, pushMode(TextMode);

// Arbitrary text, punctuated by expressions, and ended by 
// hashtags and/or a newline.
mode TextMode;
TEXT_NEWLINE: NEWLINE SPACES? {CreateIndentIfNeeded(TEXT_NEWLINE);} -> popMode;

// The start of a hashtag. The remainder of this line will consist of
// commands or hashtags, so swap to this mode and then enter hashtag mode.

TEXT_HASHTAG: HASHTAG -> mode(TextCommandOrHashtagMode), pushMode(HashtagMode) ; 

// push into expression mode here, because we might lex more 
// free text after the expression is done
TEXT_EXPRESSION_START: '{' -> pushMode(ExpressionMode); 

// The start of a hashtag. The remainder of this line will consist of
// commands or hashtags, so swap to this mode, and then enter command mode.
TEXT_COMMAND_START: '<<' -> mode(TextCommandOrHashtagMode), pushMode(CommandMode);

// The start of a format function. Push into this mode, because we may lex
// more free text after the function is done.
TEXT_FORMAT_FUNCTION_START: '[' -> pushMode(FormatFunctionMode);

// Comments after free text.
TEXT_COMMENT: COMMENT -> skip;

// Finally, lex anything up to a newline, a hashtag, the 
// start of an expression as free text, the start of a format function,
// or a command-start marker.
TEXT: TEXT_FRAG+ ;
TEXT_FRAG: {
      !(InputStream.LA(1) == '<' && InputStream.LA(2) == '<') // start-of-command marker
    &&!(InputStream.LA(1) == '/' && InputStream.LA(2) == '/') // start of a comment
    }? ~[\r\n#{[] ;

// TODO: support detecting a comment at the end of a line by looking 
// ahead and seeing '//', then skipping the rest of the line. 
// Currently "woo // foo" is parsed as one whole TEXT.

mode TextCommandOrHashtagMode;
TEXT_COMMANDHASHTAG_WS: WS -> skip;

// Comments following hashtags and line conditions.
TEXT_COMMANDHASHTAG_COMMENT: COMMENT -> skip;

TEXT_COMMANDHASHTAG_COMMAND_START: '<<' -> pushMode(CommandMode);

TEXT_COMMANDHASHTAG_HASHTAG: '#' -> pushMode(HashtagMode);

TEXT_COMMANDHASHTAG_NEWLINE: NEWLINE SPACES? {CreateIndentIfNeeded(TEXT_COMMANDHASHTAG_NEWLINE);} -> popMode;

TEXT_COMMANDHASHTAG_ERROR: . ; 

// Hashtags at the end of a Line, Command or Option.
mode HashtagMode;
HASHTAG_WS: WS -> skip;
HASHTAG_TAG: HASHTAG;

// The text of the hashtag. After we parse it, we're done parsing this
// hashtag, so leave this mode.
HASHTAG_TEXT: ~[ \t\r\n#$<]+ -> popMode;

// A format function, which allows for run-time text replacement for 
// things like pluralisation and gender 
mode FormatFunctionMode;
FORMAT_FUNCTION_WS : WS -> skip;

FORMAT_FUNCTION_ID: ID;

FORMAT_FUNCTION_NUMBER: NUMBER;

// Format functions may have expressions in them.
FORMAT_FUNCTION_EXPRESSION_START: '{' -> pushMode(ExpressionMode);

// Separates keys from values in format functions
FORMAT_FUNCTION_EQUALS: '=';

// A run of text. Escaped quotes, backslashes and format markers are allowed.
fragment FORMAT_FUNCTION_MARKER: '%';
FORMAT_FUNCTION_STRING : '"' (~('"' | '\\' | '\r' | '\n') | '\\' ('"' | '\\' | FORMAT_FUNCTION_MARKER))* '"';

// Leave this mode when we reach the delimiting 'end'
FORMAT_FUNCTION_END: ']' -> popMode;

// Expressions, involving values and operations on values.
mode ExpressionMode;
EXPR_WS : WS -> skip;

// Simple values
KEYWORD_TRUE  : 'true' ;
KEYWORD_FALSE : 'false' ;
KEYWORD_NULL : 'null' ;

OPERATOR_ASSIGNMENT : '=' | 'to' ;

OPERATOR_LOGICAL_LESS_THAN_EQUALS : '<=' | 'lte' ;
OPERATOR_LOGICAL_GREATER_THAN_EQUALS : '>=' | 'gte' ;
OPERATOR_LOGICAL_EQUALS : '==' | 'is' | 'eq' ;
OPERATOR_LOGICAL_LESS : '<' | 'lt' ;
OPERATOR_LOGICAL_GREATER : '>' | 'gt'  ;
OPERATOR_LOGICAL_NOT_EQUALS : '!=' | 'neq' ;
OPERATOR_LOGICAL_AND : 'and' | '&&' ;
OPERATOR_LOGICAL_OR : 'or' | '||' ;
OPERATOR_LOGICAL_XOR : 'xor' |  '^' ;
OPERATOR_LOGICAL_NOT : 'not' | '!' ;
OPERATOR_MATHS_ADDITION_EQUALS : '+=' ;
OPERATOR_MATHS_SUBTRACTION_EQUALS : '-=' ;
OPERATOR_MATHS_MULTIPLICATION_EQUALS : '*=' ;
OPERATOR_MATHS_MODULUS_EQUALS : '%=' ;
OPERATOR_MATHS_DIVISION_EQUALS : '/=' ;
OPERATOR_MATHS_ADDITION : '+' ;
OPERATOR_MATHS_SUBTRACTION : '-' ;
OPERATOR_MATHS_MULTIPLICATION : '*' ;
OPERATOR_MATHS_DIVISION : '/' ;
OPERATOR_MATHS_MODULUS : '%' ;

LPAREN : '(' ;
RPAREN : ')' ;
COMMA : ',' ;

// A run of text. Escaped quotes and backslashes are allowed.
STRING : '"' (~('"' | '\\' | '\r' | '\n') | '\\' ('"' | '\\'))* '"';

FUNC_ID: ID ;

// The end of an expression. Return to whatever we were lexing before.
EXPRESSION_END: '}' -> popMode;

// The end of a command. We need to leave both expression mode, and command mode.
EXPRESSION_COMMAND_END: '>>' -> popMode, popMode;

// Variables, which always begin with a '$'
VAR_ID : '$' ID ;

// Integer or decimal numbers.
NUMBER
    : INT
    | INT '.' INT
    ;

fragment INT: DIGIT+ ;
fragment DIGIT: [0-9];

// Commands; these are either specific ones that the compiler has a special 
// behaviour for, like if, else, set; otherwise, they are arbitrary text 
// (which may contain expressions), which are passed to the game.
mode CommandMode;

COMMAND_WS: WS -> skip;

// Special keywords that can appear in commands. If we see one of these after 
// the <<, it's part of the Yarn language and used for flow control. If we
// see anything else, it's a Command whose text will be passed to the game.

// Certain commands are followed by expressions (separated by some 
// whitespace), and we want to ensure that we don't accidentally match 
// things like "iffy" or "settle" as keywords followed by arbitrary text. 
// So we make some whitespace be part of the definition of the keyword.
COMMAND_IF: 'if' [\p{White_Space}] -> pushMode(ExpressionMode);
COMMAND_ELSEIF: 'elseif' [\p{White_Space}] -> pushMode(ExpressionMode);
COMMAND_ELSE: 'else' [\p{White_Space}]?; // next expected token after 'else' is '>>' so no whitespace is strictly needed 
COMMAND_SET : 'set' [\p{White_Space}] -> pushMode(ExpressionMode);
COMMAND_ENDIF: 'endif';

COMMAND_CALL: 'call' [\p{White_Space}] -> pushMode(ExpressionMode);

// End of a command.
COMMAND_END: '>>' -> popMode;

// If we see anything that we don't expect, assume that this 
// is a command with arbitrary text inside it. Replace this 
// lexer state with CommandTextMode so that when it finishes 
// up, it returns to BodyMode
COMMAND_ARBITRARY: . -> type(COMMAND_TEXT), mode(CommandTextMode);

// Arbitrary commands, which may contain expressions, and end with a '>>'.
mode CommandTextMode;
COMMAND_TEXT_END: '>>' -> popMode;
COMMAND_EXPRESSION_START: '{' -> pushMode(ExpressionMode);
COMMAND_TEXT: ~[>{]+;

// Options [[Description|NodeName]] or jumps [[NodeName]]. May be followed 
// by hashtags, so we lex those here too.
mode OptionMode;
OPTION_NEWLINE: NEWLINE SPACES? {CreateIndentIfNeeded(OPTION_NEWLINE);} -> popMode;
OPTION_WS: WS -> skip;
OPTION_END: ']]' -> popMode ;
OPTION_DELIMIT: '|' -> pushMode(OptionIDMode); // time to specifically look for IDs here
OPTION_EXPRESSION_START: '{' -> pushMode(ExpressionMode);
OPTION_FORMAT_FUNCTION_START: '[' -> pushMode(FormatFunctionMode);
OPTION_TEXT: ~[\]{|[]+ ;

// Only allow seeing runs of text as an ID after a '|' is 
// seen. This prevents an option being parsed 
// as TEXT | TEXT, and lets us prohibit multiple IDs in the
// second half of the statement.
mode OptionIDMode;
OPTION_ID_WS: [ \t] -> skip;
OPTION_ID: NODE_ID -> popMode ; 
// (We return immediately to OptionMode after we've seen this 
// single OPTION_ID, so that we can lex the OPTION_END that 
// closes the option.)
