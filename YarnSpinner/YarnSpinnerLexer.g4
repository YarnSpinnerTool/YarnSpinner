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

NEWLINE : [\n]+ ;

UNKNOWN : . ;

// ----------------------
// Title mode
// for handling the title of the node
// pops when it hits the end of the line
// A title is allowed to be anything excluding a space or newline
mode Title;
TITLE_WS : (' ' | '\t') -> skip ;
TITLE_TEXT : ~('\n' | ' ' | '\t')+ -> popMode ;

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

WS_IN_BODY : (' ' | '\t' | '\n')+ -> skip ; // skip spaces, tabs, newlines
COMMENT : '//' .*? '\n' -> skip ;

BODY_CLOSE : '===' -> popMode ;

TEXT_STRING : '"' .*? '"' ;

SHORTCUT_ENTER : ('->' | '-> ') -> pushMode(Shortcuts);

// currently using \a and \v as the indent and dedent symbols
// these play the role that { and } play in many other languages
// not sure if this is the best idea, feels like it might break
// but at this stage the yarn file has gone through the preprocessor so it shouldnt really matter
// have ruled out people using \a and \v as normal text, not likely to cause issue but worth pointing out

//INDENT : '{' ;
//DEDENT : '}' ;
INDENT : '\u0007';
DEDENT : '\u000B';

COMMAND_IF : COMMAND_OPEN KEYWORD_IF -> pushMode(Command) ;
//COMMAND_ELSE : COMMAND_OPEN KEYWORD_ELSE -> pushMode(Command) ;
COMMAND_ELSE : COMMAND_OPEN KEYWORD_ELSE COMMAND_CLOSE | '<<else>>' | '<<ELSE>>' ;
COMMAND_ELSE_IF : COMMAND_OPEN KEYWORD_ELSE_IF -> pushMode(Command) ;
COMMAND_ENDIF : COMMAND_OPEN ('endif' | 'ENDIF') '>>' ;
COMMAND_SET : COMMAND_OPEN KEYWORD_SET -> pushMode(Command) ;
COMMAND_FUNC : COMMAND_OPEN ID '(' -> pushMode(Command) ;

ACTION_CMD : COMMAND_OPEN -> more, pushMode(Action) ;

COMMAND_OPEN : '<<' ' '* ;

OPTION_ENTER : '[[' -> pushMode(Option) ;

HASHTAG : '#' TEXT ;

BODY_GOBBLE : . -> more, pushMode(Text);

// ----------------------
// Text mode
// for handling the raw lines of dialogue
// goes until it hits a hashtag, or an indent/dedent and then pops
// is zero or more as it will always have the first symbol passed by BODY_GOBBLE
mode Text;

TEXT : ~('\n'|'\u0007'|'\u000B'|'#')* -> popMode;

// ----------------------
// Shortcut mode
// Handles any form of text except the delimiters or <<
// currently uses a semantic predicate to handle << which I don't like and would like to change
mode Shortcuts;

// these 3 commented out bits work but use a semantic predicate
fragment CHEVRON : '<' ~('<'|'#'|'\n'|'\u0007'|'\u000B') ;
fragment PARTIAL : (~('<'|'#'|'\n'|'\u0007'|'\u000B') | CHEVRON)+ ;
SHORTCUT_TEXT : (PARTIAL | PARTIAL* '<' {_input.LA(1) != '<'}?) -> popMode ;

// this is the bit I am trying to get working based on what was said on SO
//SHORTCUT_TEXT : CHAR+ -> popMode;

//SHORTCUT_COMMAND : '<<' -> popMode, pushMode(Command);
//SHORTCUT_COMMAND : '<<' -> popMode, pushMode(Command);
//CHEVRON : '<' -> type(SHORTCUT_TEXT) ;
//fragment CHAR : ~('<'|'#'|'\n'|'\u0007'|'\u000B') ;

// ----------------------
// Command mode
// for handling branching and expression

mode Command;

COMMAND_WS : (' ' | '\n' | '\t')+ -> skip ; // skip spaces, tabs, newlines

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
