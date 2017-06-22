// Grammar file for the YarnSpinner lexer

lexer grammar YarnSpinnerLexer;

// ----------------------
// Default mode
// handles headers and pushes into body mode

BODY_ENTER : '---' -> pushMode(Body) ;

HEADER_TITLE : 'title' ;
HEADER_TAGS : 'tags' ;
HEADER_COLOUR : 'colorID' ;
HEADER_POSITION : 'position' ;
HEADER_SEPARATOR : ':' ;
COMMA : ',' ;

// this should allow normal "programming style" strings
STRING : '"' .*? '"';
//STRING : '"' (~('"' | '\\' | '\n') | '\\' ('"' | '\\'))* '"' ;

NUMBER : [0-9]+('.'[0-9]+)? ; // match numbers
ID : (([a-zA-Z0-9])|('_'))+ ;

NEWLINE : [\r\n]+ ;
WS : (' '|'\t') -> skip ;

// ----------------------
// Body mode
// for handling normal dialogue lines and moving between modes

mode Body;

BODY_CLOSE : '===' -> popMode ;

SHORTCUT_ENTER : '->' ;
// currently using \a and \v as the indent and dedent symbols
// these play the role that { and } play in many other languages
// not sure if this is the best idea, feels like it might break
// but at this stage the yarn file has gone through the preprocessor so it shouldnt really matter
// have ruled out people using \a and \v as normal text, not likely to cause issue but worth pointing out
INDENT : '\u0007' ;//'{' ;
DEDENT : '\u000B';//'}' ;

COMMAND_ENTER : '<<' -> pushMode(Command) ;
OPTION_ENTER : '[[' -> pushMode(Option) ;

BODY_NEWLINE : [\r\n]+ -> skip ;

TEXT : STRING | TEXTCOMPONENT+ ;
fragment TEXTCOMPONENT : ~('>'|'<'|'['|']'|'\n'|'\u0007'|'\u000B') ;

COMMENT : '//' .*? '\n' -> skip ;
WS_IN_BODY : [ \t\r\n]+ -> skip ; // skip spaces, tabs, newlines

// ----------------------
// Command mode
// for handling branching and expression

mode Command;

COMMAND_CLOSE : '>>' -> popMode ;

COMMAND_STRING : STRING ;

KEYWORD_IF : 'if' | 'IF' ;
KEYWORD_ELSE : 'else' | 'ELSE' ;
KEYWORD_ELSE_IF : 'elseif' | 'ELSEIF' ;
KEYWORD_ENDIF : 'endif' | 'ENDIF' ;

KEYWORD_SET : 'set' | 'SET' ;
KEYWORD_TO : 'to' | 'TO' | '=' ;

KEYWORD_TRUE  : 'true' | 'TRUE' ;
KEYWORD_FALSE : 'false' | 'FALSE' ;

// All the operators YarnSpinner currently supports
OPERATOR_LOGICAL_LESS_THAN_EQUALS : '<=' ;
OPERATOR_LOGICAL_GREATER_THAN_EQUALS : '>=' ;
OPERATOR_LOGICAL_EQUALS : '==' | 'IS' | 'is' ;
OPERATOR_LOGICAL_LESS : '<' ;
OPERATOR_LOGICAL_GREATER : '>' ;
OPERATOR_LOGICAL_NOT_EQUALS : '!=' ;
OPERATOR_LOGICAL_AND : 'and' | 'AND' | '&&' ;
OPERATOR_LOGICAL_OR : 'or' | 'OR' | '||' ;
OPERATOR_LOGICAL_XOR : 'xor' | 'XOR' | '^' ;
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

LPAREN: '(' ;
RPAREN: ')' ;

VAR_ID : '$' ID ;

// this should allow for 1, 1.1, and .1 all fine
BODY_NUMBER : '-'? DIGIT+('.'DIGIT+)? ;
fragment DIGIT : [0-9] ;

FUNCTION_TEXT : ID '(' .*? ')' ;

ACTION_TEXT : ID ;

WS_IN_COMMAND : (' ' | '\n' | '\t') -> skip ; // skip spaces, tabs, newlines

// ----------------------
// Option mode
// For handling options
// pops when hits ]]

mode Option;

OPTION_SEPARATOR: '|' -> pushMode(OptionLink) ;
OPTION_TEXT : ~('|'|']')+ ;
OPTION_CLOSE: ']]' -> popMode ;

mode OptionLink;
OPTION_LINK : ~(']')+ -> popMode ;
