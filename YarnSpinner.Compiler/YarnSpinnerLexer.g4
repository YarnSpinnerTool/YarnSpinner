lexer grammar YarnSpinnerLexer;

tokens { INDENT, DEDENT }

channels {
    WHITESPACE,
    COMMENTS
}

options {
    superClass=IndentAwareLexer;
}
 
// Root mode: skip whitespaces, set up some commonly-seen 
// tokens
WS : ([ \t])+ -> skip;

COMMENT: '//' ~('\r'|'\n')* -> channel(COMMENTS);

// Newlines are lexed and normally not used in the parser, but we'll put them in a
// separate channel here (rather than skipping them) so that the indent
// checker has access to them
NEWLINE: ( '\r'? '\n' | '\r' ) [ \t]* -> channel(WHITESPACE);

ID : IDENTIFIER_HEAD IDENTIFIER_CHARACTERS?;

fragment IDENTIFIER_HEAD : 
    [a-zA-Z_]
  | '\u00A8' | '\u00AA' | '\u00AD' | '\u00AF' | [\u00B2-\u00B5] | [\u00B7-\u00BA]
  | [\u00BC-\u00BE] | [\u00C0-\u00D6] | [\u00D8-\u00F6] | [\u00F8-\u00FF]
  | [\u0100-\u02FF] | [\u0370-\u167F] | [\u1681-\u180D] | [\u180F-\u1DBF]
  | [\u1E00-\u1FFF]
  | [\u200B-\u200D] | [\u202A-\u202E] | [\u203F-\u2040] | '\u2054' | [\u2060-\u206F]
  | [\u2070-\u20CF] | [\u2100-\u218F] | [\u2460-\u24FF] | [\u2776-\u2793]
  | [\u2C00-\u2DFF] | [\u2E80-\u2FFF]
  | [\u3004-\u3007] | [\u3021-\u302F] | [\u3031-\u303F] | [\u3040-\uD7FF]
  | [\uF900-\uFD3D] | [\uFD40-\uFDCF] | [\uFDF0-\uFE1F] | [\uFE30-\uFE44]
  | [\uFE47-\uFFFD] 
  | [\u{10000}-\u{1FFFD}] | [\u{20000}-\u{2FFFD}] | [\u{30000}-\u{3FFFD}] | [\u{40000}-\u{4FFFD}]
  | [\u{50000}-\u{5FFFD}] | [\u{60000}-\u{6FFFD}] | [\u{70000}-\u{7FFFD}] | [\u{80000}-\u{8FFFD}]
  | [\u{90000}-\u{9FFFD}] | [\u{A0000}-\u{AFFFD}] | [\u{B0000}-\u{BFFFD}] | [\u{C0000}-\u{CFFFD}]
  | [\u{D0000}-\u{DFFFD}] | [\u{E0000}-\u{EFFFD}]
  ;

fragment IDENTIFIER_CHARACTER : [0-9]
  | [\u0300-\u036F] | [\u1DC0-\u1DFF] | [\u20D0-\u20FF] | [\uFE20-\uFE2F]
  | IDENTIFIER_HEAD
  ;

fragment IDENTIFIER_CHARACTERS : IDENTIFIER_CHARACTER+ ;

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
HEADER_NEWLINE : NEWLINE -> type(NEWLINE), channel(WHITESPACE), popMode;

// The main body of a node.
mode BodyMode;

// Ignore all whitespace and comments
BODY_WS : WS -> skip;
BODY_NEWLINE : NEWLINE -> type(NEWLINE), channel(WHITESPACE);
BODY_COMMENT : COMMENT -> type(COMMENT), channel(COMMENTS) ;

// End of this node; return to global mode (eg node headers)
// or end of file
BODY_END : '===' -> popMode; 

// The start of a shortcut option
SHORTCUT_ARROW : '->' ;

// The start of a command
COMMAND_START: '<<' -> pushMode(CommandMode) ;

// The start of a hashtag. Can goes at the end of the 
// line, but this rule allows us to capture '#' at the start 
// of a line.
BODY_HASHTAG: '#' -> type(HASHTAG), pushMode(TextCommandOrHashtagMode), pushMode(HashtagMode);

// The start of an inline expression. Immediately lex as 
// TEXT_EXPRESSION_START and push into TextMode  and 
// ExpressionMode.
EXPRESSION_START: '{' -> pushMode(TextMode), pushMode(ExpressionMode);


// Any other text means this is a Line. Lex this first character as
// TEXT, and enter TextMode.
ANY: .  -> type(TEXT), pushMode(TextMode);

// Arbitrary text, punctuated by expressions, and ended by 
// hashtags and/or a newline.
mode TextMode;

// Newlines while in text mode are syntactically relevant, so we emit them
// into the default channel (rather than into WHITESPACE).
TEXT_NEWLINE: NEWLINE -> type(NEWLINE), popMode;

// An escaped markup bracket. Markup is parsed by a separate phase (see
// LineParser.cs in the C# code), and we want to pass escaped markers
// directly through, so we'll match this and report it as TEXT before
// matching the more general 'escaped character' rule.
TEXT_ESCAPED_MARKUP_BRACKET: ('\\[' | '\\]') -> type(TEXT);

// An escape marker. Skip this token and enter escaped text mode, which 
// allows escaping characters that would otherwise be syntactically 
// meaningful.
TEXT_ESCAPE: '\\' -> skip, pushMode(TextEscapedMode) ;

// The start of a hashtag. The remainder of this line will consist of
// commands or hashtags, so swap to this mode and then enter hashtag mode.

TEXT_HASHTAG: HASHTAG -> type(HASHTAG), mode(TextCommandOrHashtagMode), pushMode(HashtagMode) ; 

// push into expression mode here, because we might lex more 
// free text after the expression is done
TEXT_EXPRESSION_START: '{' -> type(EXPRESSION_START), pushMode(ExpressionMode); 

// The start of a hashtag. The remainder of this line will consist of
// commands or hashtags, so swap to this mode, and then enter command mode.
TEXT_COMMAND_START: '<<' -> type(COMMAND_START), mode(TextCommandOrHashtagMode), pushMode(CommandMode);

// Comments after free text.
TEXT_COMMENT: COMMENT -> channel(COMMENTS);

// Finally, lex text. We'll read either everything up to (but not including) a
// newline, a hashtag, the start of an expression, the start of an escape, the 
// start of a command, or the start of a comma. We'll also lex a _single_ 
// chevron or slash as a text; we know that they won't be a command or a 
// comment, because the earlier rules for TEXT_COMMAND_START and TEXT_COMMENT 
// would have caught them. By lexing a single one of these and then stopping,
// we'll force the lexer to restart scanning after the current character, which
// means we'll correctly lex things like "<<<" as "TEXT(<) COMMAND_START(<<)".
TEXT: TEXT_FRAG+ | '<' | '/' ;
fragment TEXT_FRAG: ~[\r\n#{\\</];

mode TextEscapedMode;

// Lex a single escapable character as text. (When we enter this mode,
// TEXT_ESCAPE has already skipped the leading \ that marks an escaped
// character, so we only need to lex the character itself.)
TEXT_ESCAPED_CHARACTER: [\\<>{}#/] -> type(TEXT), popMode ; 

mode TextCommandOrHashtagMode;
TEXT_COMMANDHASHTAG_WS: WS -> skip;

// Comments following hashtags and line conditions.
TEXT_COMMANDHASHTAG_COMMENT: COMMENT -> channel(COMMENTS);

TEXT_COMMANDHASHTAG_COMMAND_START: '<<' -> type(COMMAND_START), pushMode(CommandMode);

TEXT_COMMANDHASHTAG_HASHTAG: '#' -> type(HASHTAG), pushMode(HashtagMode);

// Newlines while in text mode are syntactically relevant, so we emit them
// into the default channel (rather than into WHITESPACE).
TEXT_COMMANDHASHTAG_NEWLINE: NEWLINE -> type(NEWLINE),  popMode;

TEXT_COMMANDHASHTAG_ERROR: . ; 

// Hashtags at the end of a Line or Command.
mode HashtagMode;
HASHTAG_WS: WS -> skip;
HASHTAG_TAG: HASHTAG -> type(HASHTAG);

// The text of the hashtag. After we parse it, we're done parsing this
// hashtag, so leave this mode.
HASHTAG_TEXT: ~[ \t\r\n#$<]+ -> popMode;

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

EXPRESSION_AS: 'as';

TYPE_STRING: 'string' -> type(FUNC_ID);
TYPE_NUMBER: 'number' -> type(FUNC_ID);
TYPE_BOOL: 'bool' -> type(FUNC_ID);

// A run of text. Escaped quotes and backslashes are allowed.
STRING : '"' (~('"' | '\\' | '\r' | '\n') | '\\' ('"' | '\\'))* '"';

FUNC_ID: ID ;

// The end of an expression. Return to whatever we were lexing before.
EXPRESSION_END: '}' -> popMode;

// The end of a command. We need to leave both expression mode, and command mode.
EXPRESSION_COMMAND_END: '>>' -> type(COMMAND_END), popMode, popMode;

// Variables, which always begin with a '$'
VAR_ID : '$' ID ;

// Dots ('.')
DOT : '.' ;

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

COMMAND_DECLARE: 'declare' [\p{White_Space}] -> pushMode(ExpressionMode);

COMMAND_JUMP: 'jump' [\p{White_Space}] -> pushMode(CommandIDMode);

COMMAND_ENUM: 'enum' [\p{White_Space}] -> pushMode(CommandIDMode);

COMMAND_CASE: 'case' [\p{White_Space}] -> pushMode(CommandIDMode);

COMMAND_ENDENUM: 'endenum' [\p{White_Space}]?;

// Keywords reserved for future language versions
COMMAND_LOCAL: 'local' [\p{White_Space}]; 

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

// A mode in which we expect to parse a node ID.
mode CommandIDMode;
COMMAND_ID: ID -> type(ID), popMode;
COMMAND_ID_END: '>>' -> type(COMMAND_END), popMode; // almost certainly a parse error, but not a lex error
