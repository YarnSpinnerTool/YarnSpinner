// Grammar file for the YarnSpinner lexer

lexer grammar YarnSpinnerLexer;

// ----------------------
// Default mode
// handles headers and pushes into body mode

BODY_ENTER : '---' -> pushMode(Body) ;

// the two predetermined and important headers
HEADER_TITLE : 'title:' -> pushMode(Title) ;
HEADER_TAGS : 'tags:' -> pushMode(Tags) ;
// the catchall for all other headers, anything except spaces ending in a :
HEADER_NAME : ~(':' | ' ' | '\n')+ ;

HEADER_SEPARATOR : ':' -> pushMode(HeaderText);

// this should allow normal "programming style" strings
STRING : '"' .*? '"';
// format for identifiers used in numerous places
ID : (([a-zA-Z0-9])|('_'))+ ;

NEWLINE : [\r\n]+ ;

UNKNOWN : . ;

// ----------------------
// Title mode
// for handling the title of the node
// pops when it hits the end of the line
// A title is allowed to be anything excluding a space or newline
mode Title;
TITLE_WS : (' ' | '\t') -> skip ;
TITLE_TEXT : ~('\n' | ' ')+ -> popMode ;

// ----------------------
// Tag mode
// for handling the tags of the node
// pops when it hits the end of the line
// currently this is just the same as the Header Text
// but will likely change so better to set it up now
mode Tags;
TAG_TEXT : ~('\n')+ -> popMode ;

// ----------------------
// Header Text mode
// for grabbing all the non-title/tag header text
// pops when it hits the end of a line
mode HeaderText;
HEADER_WS : (' ' | '\t') -> skip ;
HEADER_TEXT : ~('\n')+ -> popMode;

// ----------------------
// Body mode
// for handling normal dialogue lines and moving between modes

mode Body;

BODY_CLOSE : '===' -> popMode ;

WS_IN_BODY : [ \t\r\n]+ -> skip ; // skip spaces, tabs, newlines
COMMENT : '//' .*? '\n' -> skip ;

SHORTCUT_ENTER : '->' | '-> ';
// currently using \a and \v as the indent and dedent symbols
// these play the role that { and } play in many other languages
// not sure if this is the best idea, feels like it might break
// but at this stage the yarn file has gone through the preprocessor so it shouldnt really matter
// have ruled out people using \a and \v as normal text, not likely to cause issue but worth pointing out
INDENT : '\u0007' ;//'{' ;
DEDENT : '\u000B';//'}' ;

//COMMAND_ENTER : '<<' -> pushMode(Command) ;
ACTION_CMD : '<<' -> more, pushMode(Action) ;

COMMAND_IF : '<<' KEYWORD_IF -> pushMode(Command) ;
COMMAND_ELSE : '<<' KEYWORD_ELSE -> pushMode(Command) ;
COMMAND_ELSE_IF : '<<' KEYWORD_ELSE_IF -> pushMode(Command) ;
COMMAND_ENDIF : '<<' ('endif' | 'ENDIF') '>>' ;
COMMAND_SET : '<<' KEYWORD_SET -> pushMode(Command) ;
COMMAND_FUNC : '<<' ID '(' -> pushMode(Command) ;

OPTION_ENTER : '[[' -> pushMode(Option) ;

BODY_NEWLINE : [\r\n]+ -> skip ;

HASHTAG : '#' TEXT ;

TEXT : BODY_STRING | TEXTCOMPONENT+ ;
BODY_STRING : '"' .*? '"';
fragment TEXTCOMPONENT : ~('>'|'<'|'['|']'|'\n'|'\u0007'|'\u000B'|'#') ;

BODY_UNKNOWN : . ;

// ----------------------
// Command mode
// for handling branching and expression

mode Command;

COMMAND_WS : (' ' | '\n' | '\t') -> skip ; // skip spaces, tabs, newlines

COMMAND_CLOSE : '>>' -> popMode ;

COMMAND_STRING : STRING ;

// adding a space after the keywords to get around the issue of
//<<iffy>> being detected as an if statement
KEYWORD_IF : ('if' | 'IF') ' ' ;
KEYWORD_ELSE : ('else' | 'ELSE') ' ' ;
KEYWORD_ELSE_IF : ('elseif' | 'ELSEIF') ' ' ;
//KEYWORD_FUNC : 'func' | 'FUNC' ;
KEYWORD_SET : ('set' | 'SET') ' ' ;

KEYWORD_TRUE  : 'true' | 'TRUE' ;
KEYWORD_FALSE : 'false' | 'FALSE' ;

KEYWORD_NULL : 'null' | 'NULL' ;

KEYWORD_TO : 'to' | 'TO' | '=' ;
// All the operators YarnSpinner currently supports
OPERATOR_LOGICAL_LESS_THAN_EQUALS : '<=' | 'lte' | 'LTE' ;
OPERATOR_LOGICAL_GREATER_THAN_EQUALS : '>=' | 'gte' | 'GTE' ;
OPERATOR_LOGICAL_EQUALS : '==' | 'IS' | 'is' | 'eq' | 'EQ' ;
OPERATOR_LOGICAL_LESS : '<' | 'lt' | 'LT' ;
OPERATOR_LOGICAL_GREATER : '>' | 'gt' | 'GT' ;
OPERATOR_LOGICAL_NOT_EQUALS : '!=' | 'neq' | 'NEQ' ;
OPERATOR_LOGICAL_AND : 'and' | 'AND' | '&&' ;
OPERATOR_LOGICAL_OR : 'or' | 'OR' | '||' ;
OPERATOR_LOGICAL_XOR : 'xor' | 'XOR' | '^' ;
OPERATOR_LOGICAL_NOT : 'not' | 'NOT' | '!' ;
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

VAR_ID : '$' ID ;

// this should allow for 1, 1.1, and .1 all fine
BODY_NUMBER : '-'? DIGIT+('.'DIGIT+)? ;
fragment DIGIT : [0-9] ;

FUNC_ID : ID ;

COMMAND_UNKNOWN : . ;

// ----------------------
// Action mode
// handles the <<anything you want>> command
mode Action;
ACTION : '>>' -> popMode ;
IGNORE : . -> more ;

//ACTION_TEXT : ID ;

//WS_IN_COMMAND : (' ' | '\n' | '\t') -> skip ; // skip spaces, tabs, newlines

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
