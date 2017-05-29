// Grammar file for the YarnSpinner lexer
// currently does not support shortcut -> syntax

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

NUMBER : [0-9]+('.'[0-9]+)? ; // match numbers
ID : (([a-zA-Z0-9])|('_'))+ ;

NEWLINE : [\r\n]+ ;
WS : ' ' -> skip ;

// ----------------------
// Body mode
// for handling normal dialogue lines and moving between modes
mode Body;

BODY_CLOSE : '===' -> popMode ;

COMMAND_ENTER : '<<' -> pushMode(Command) ;
OPTION_ENTER : '[[' -> pushMode(Option) ;

BODY_NEWLINE : [\r\n]+ -> skip;

TEXT: TEXTCOMPONENT+ ;
fragment TEXTCOMPONENT: ~('<'|'|'|'['|']'|'\n') ;

COMMENT : '//' .*? '\n' -> skip ;
WS_IN_BODY : [ \t\r\n]+ -> skip ; // skip spaces, tabs, newlines

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

// All the operators YarnSpinner currently supports
OPERATOR_LOGICAL_LESS_THAN_EQUALS : '<=' ;
OPERATOR_LOGICAL_GREATER_THAN_EQUALS : '>=' ;
OPERATOR_LOGICAL_EQUALS : '==' | 'IS' | 'is' ;
OPERATOR_LOGICAL_LESS : '<' ;
OPERATOR_LOGICAL_GREATER : '>' ;
OPERATOR_LOGICAL_NOT_EQUALS : '!=' ;
OPERATOR_LOGICAL_AND : 'and' | 'AND' | '&&' ;
OPERATOR_LOGICAL_OR : 'or' | 'OR' | '||' ;
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

VAR_ID : '$' BODY_ID ;
BODY_NUMBER : [0-9]+('.'[0-9]+)? ; // match numbers
BODY_ID : (([a-zA-Z0-9])|('_'))+ ;

WS_IN_COMMAND : WS_IN_BODY -> skip ; // skip spaces, tabs, newlines

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
