lexer grammar YarnSpinnerLexer;

// Root mode: skip whitespaces, set up some commonly-seen 
// tokens
WS : ([ \t\r\n])+ -> skip;

// Some commonly-seen tokens that other lexer modes will use
NEWLINE: [\r\n]+ ;
ID : [a-zA-Z_](([a-zA-Z0-9])|('_'))* ;

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
REST_OF_LINE : ~('\n')+;
HEADER_NEWLINE : NEWLINE -> popMode;

COMMENT: '//' .*? NEWLINE -> skip;

// The main body of a node.
mode BodyMode;

// Ignore all whitespace and comments
BODY_WS : WS -> skip;
BODY_COMMENT : COMMENT -> skip ;

// End of this node; return to global mode (eg node headers)
// or end of file
BODY_END : '===' -> popMode; 

// Indent tokens. (TODO: do not use these strings)
INDENT : 'INDENT' ;
DEDENT : 'DEDENT' ;

// The start of a shortcut option
SHORTCUT_ARROW : '->' ;

// The start of a command
COMMAND_START: '<<' -> pushMode(CommandMode) ;

// The start of an option or jump
OPTION_START: '[[' -> pushMode(OptionMode) ;

// The start of a hashtag. Can goes at the end of the 
// line, but this rule allows us to capture '#' at the start 
// of a line, or following an Option.
BODY_HASHTAG: '#' -> pushMode(HashtagMode);

// Any other text means this is a Line
ANY: . -> more, pushMode(TextMode);

// Arbitrary text, punctuated by expressions, and ended by 
// hashtags and/or a newline.
mode TextMode;
TEXT_NEWLINE: NEWLINE -> popMode;

// The start of a hashtag. Swap to Hashtag mode here, because 
// we aren't looking for any more free text.
TEXT_HASHTAG: HASHTAG -> mode(HashtagMode); 

// push into expression mode here, because we might lex more 
// free text after the expression is done
TEXT_EXPRESSION_START: '{' -> pushMode(ExpressionMode); 

// Finally, lex anything up to a newline, a hashtag, or the 
// start of an expression as free text.
TEXT: ~[\n#{]+ ;

// TODO: support detecting a comment at the end of a line by looking 
// ahead and seeing '//', then skipping the rest of the line. 
// Currently "woo // foo" is parsed as one whole TEXT.

// TODO: support for mid-line or end-line <<if expr>> - 
// requires looking ahead two characters, so do something like:
//   TEXT: TEXT_FRAG
//   TEXT_FRAG:  {_input.LA(2) != "<<"}? ~[\n#{] -> more
// This is a little complex because we can't just stop runs of text
// at '<' because that prevents us from lexing in-line HTML-like 
// syntax (eg. 'Mae: <i>Woo!</i> <<if $var>>' )

// Hashtags at the end of a Line, Command or Option.
mode HashtagMode;
HASHTAG_WS: [ \t] -> skip;
// comments at the end of a hashtag list - note that we capture 
// everything UP TO the newline, because we still want to capture
// the newline after the comment as a HASHTAG_NEWLINE token, which 
// the parser is looking for to mark the end of the run of hashtags.
HASHTAG_COMMENT: '//' ~[\n]* -> skip; 
HASHTAG_TAG: HASHTAG;
// A newline; we're done looking for hashtag-related symbols
HASHTAG_NEWLINE: NEWLINE -> popMode;
// A command - this marks the start of a line condition
HASHTAG_COMMAND_START: '<<' -> pushMode(CommandMode);
HASHTAG_TEXT: ~[ \t\n#$<]+ ;

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

// A run of text.
STRING : '"' .*? '"';

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
COMMAND_ELSE: 'else' [\p{White_Space}];
COMMAND_SET : 'set' [\p{White_Space}] -> pushMode(ExpressionMode);
COMMAND_ENDIF: 'endif';

// End of a command.
COMMAND_END: '>>' -> popMode;

// If we see anything that we don't expect, assume that this 
// is a command with arbitrary text inside it. Replace this 
// lexer state with CommandTextMode so that when it finishes 
// up, it returns to BodyMode
COMMAND_ARBITRARY: . -> more, mode(CommandTextMode);

// Arbitrary commands, which may contain expressions, and end with a '>>'.
mode CommandTextMode;
COMMAND_TEXT_END: '>>' -> popMode;
COMMAND_EXPRESSION_START: '{' -> pushMode(ExpressionMode);
COMMAND_TEXT: ~[>{]+;

// Options [[Description|NodeName]] or jumps [[NodeName]]. May be followed 
// by hashtags, so we lex those here too.
mode OptionMode;
OPTION_NEWLINE: NEWLINE -> popMode;
OPTION_WS: WS -> skip;
OPTION_END: ']]' -> popMode ;
OPTION_HASHTAG: HASHTAG -> pushMode(HashtagMode);
OPTION_DELIMIT: '|' -> pushMode(OptionIDMode); // time to specifically look for IDs here
OPTION_EXPRESSION_START: '{' -> pushMode(ExpressionMode);
OPTION_TEXT: ~[\]{|]+ ;

// Only allow seeing runs of text as an ID after a '|' is 
// seen. This prevents an option being parsed 
// as TEXT | TEXT, and lets us prohibit multiple IDs in the
// second half of the statement.
mode OptionIDMode;
OPTION_ID_WS: [ \t] -> skip;
OPTION_ID: ID -> popMode ; 
// (We return immediately to OptionMode after we've seen this 
// single OPTION_ID, so that we can lex the OPTION_END that 
// closes the option.)
