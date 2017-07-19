// Basic Parser grammar for YarnSpinner

parser grammar YarnSpinnerParser;

options { tokenVocab=YarnSpinnerLexer; }

dialogue : node+ EOF;

node: header body NEWLINE*;

// this is wrong, this means you have to have a title (correct)
// but can have any number (including 0) of the others (also correct)
// we only want 0-1 of each in any order but to do this means writing them out
// or doing it with some code, the in code option seems the best here
// at least according to https://stackoverflow.com/questions/14934081/antlr4-matching-all-input-alternatives-exaclty-once
header : header_title (header_tag | header_line)* ;
header_title : HEADER_TITLE TITLE_TEXT NEWLINE ;
header_tag : HEADER_TAGS TAG_TEXT NEWLINE ;
header_line : HEADER_NAME ':' HEADER_TEXT NEWLINE ;

body : BODY_ENTER statement* BODY_CLOSE ;

statement
    : shortcut_statement
    | if_statement
    | set_statement
    | option_statement
	| function_statement
    | action_statement
    | line_statement
    ;

shortcut_statement : shortcut+ ;
shortcut : SHORTCUT_ENTER shortcut_text shortcut_conditional? hashtag_block? (INDENT statement* DEDENT)? ;
shortcut_conditional : COMMAND_IF expression COMMAND_CLOSE ;
shortcut_text : SHORTCUT_TEXT ;

if_statement : if_clause (else_if_clause)* (else_clause)? COMMAND_ENDIF (hashtag_block)? ;
if_clause : COMMAND_IF expression COMMAND_CLOSE statement* ;
else_if_clause : COMMAND_ELSE_IF expression COMMAND_CLOSE statement* ;
else_clause : COMMAND_ELSE statement* ;

// this is a hack until I can work out exactly what the rules for setting are
set_statement
    : COMMAND_SET variable KEYWORD_TO* expression COMMAND_CLOSE
    | COMMAND_SET expression COMMAND_CLOSE
    ;

option_statement
	: ('[[' OPTION_TEXT '|' OPTION_LINK ']]'
	| '[[' OPTION_TEXT ']]')
	(hashtag_block)? ;

function : FUNC_ID '(' expression? (COMMA expression)* ')' ;
// this is messy
function_statement : COMMAND_FUNC expression (COMMA expression)* ')' COMMAND_CLOSE ;
// this isn't ideal but works quite well
action_statement : ACTION ;

text : TEXT | TEXT_STRING ;
line_statement : text (hashtag_block)? ;

hashtag_block : hashtag+ ;
hashtag : HASHTAG ;

// this feel a bit crude
// need to work on this
expression
	: '(' expression ')' #expParens
	| <assoc=right>'-' expression #expNegative
	| <assoc=right>OPERATOR_LOGICAL_NOT expression #expNot
	| expression op=('*' | '/' | '%') expression #expMultDivMod
	| expression op=('+' | '-') expression #expAddSub
	| expression op=(OPERATOR_LOGICAL_LESS_THAN_EQUALS | OPERATOR_LOGICAL_GREATER_THAN_EQUALS | OPERATOR_LOGICAL_LESS | OPERATOR_LOGICAL_GREATER ) expression #expComparison
	| expression op=(OPERATOR_LOGICAL_EQUALS | OPERATOR_LOGICAL_NOT_EQUALS) expression #expEquality
	| variable op=('*=' | '/=' | '%=') expression #expMultDivModEquals
	| variable op=('+=' | '-=') expression #expPlusMinusEquals
	| expression op=(OPERATOR_LOGICAL_AND | OPERATOR_LOGICAL_OR | OPERATOR_LOGICAL_XOR) expression #expAndOrXor
	| value #expValue
    ;

// can add in support for more values in here
value
    : BODY_NUMBER	 #valueNumber
    | KEYWORD_TRUE   #valueTrue
    | KEYWORD_FALSE  #valueFalse
	| variable		 #valueVar
	| COMMAND_STRING #valueString
	| function		 #valueFunc
    | KEYWORD_NULL   #valueNull
    ;
variable
    : VAR_ID
    ;
