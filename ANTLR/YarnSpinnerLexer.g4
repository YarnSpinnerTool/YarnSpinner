// Grammar file for the YarnSpinner lexer
// currently does not support shortcut -> syntax

lexer grammar YarnSpinnerLexer;

// ----------------------
// Default mode
// for handling normal dialogue lines and moving between modes

BODY_ENTER : '---' ;
BODY_CLOSE : '===' ;

COMMAND_ENTER : '<<' -> pushMode(Command) ;
OPTION_ENTER : '[[' -> pushMode(Option) ;

NEWLINE : [\r\n]+ -> skip;

TEXT: TEXTCOMPONENT+ ;
fragment TEXTCOMPONENT: ~('<'|'|'|'['|']'|'\n') ;

WS : [ \t\r\n]+ -> skip ; // skip spaces, tabs, newlines
COMMENT : '//' .*? '\n' -> skip ;

// ----------------------
// Command mode
// for handling branching and expression
mode Command;

COMMAND_CLOSE : '>>' -> popMode ;

KEYWORD_IF : 'if' | 'IF' ;
KEYWORD_ELSE : 'else' | 'ELSE' ;
KEYWORD_ELSE_IF : 'elseif' | 'ELSEIF' ;
KEYWORD_ENDIF : 'endif' | 'ENDIF' ;

KEYWORD_SET : 'set' | 'SET' ;
KEYWORD_TO : 'to' | 'TO' | '=' ;

KEYWORD_TRUE  : 'true' | 'TRUE' ;
KEYWORD_FALSE : 'false' | 'FALSE' ;

NUMBER : [0-9]+(.[0-9]+)? ; // match numbers
VAR_ID : '$' ID ;
ID : (([a-zA-Z0-9])|('_'))+ ;

// All the operators YarnSpinner currently supports
OPERATOR_LOGICAL_LESS_THAN_EQUALS : '<=' ;
OPERATOR_LOGICAL_GREATER_THAN_EQUALS : '>=' ;
OPERATOR_LOGICAL_EQUALS : '==' | 'IS' | 'is' ;
OPERATOR_LOGICAL_LESS : '<' ;
OPERATOR_LOGICAL_GREATER : '>' ;
OPERATOR_LOGICAL_NOT_EQUALS : '!=' ;
OPERATOR_MATHS_ADDITION : '+' ;
OPERATOR_MATHS_SUBTRACTION : '-' ;
OPERATOR_MATHS_MULTIPLICATION : '*' ;
OPERATOR_MATHS_DIVISION : '/' ;
OPERATOR_MATHS_MODULUS : '%' ;

LPAREN: '(' ;
RPAREN: ')' ;

WS_IN_COMMAND : WS -> skip ; // skip spaces, tabs, newlines

// ----------------------
// Option mode
// For handling options
// pops when hits ]]
mode Option;

OPTION_SEPARATOR: '|' -> pushMode(OptionLink) ;
OPTION_TEXT: TEXT ;
OPTION_CLOSE: ']]' -> popMode ;

mode OptionLink;
OPTION_LINK : TEXT -> popMode;
