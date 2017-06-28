// Basic Parser grammar for YarnSpinner

parser grammar YarnSpinnerParser;

options { tokenVocab=YarnSpinnerLexer; }

dialogue : node+ EOF;

node: header body NEWLINE*;

// this is wrong, this means you have to have a title (correct)
// but can have any number (including 0) of the others
// we only want 0-1 of each in any order but to do this means writing them out
// or doing it with some code
// the listener code option seems the best here
// at least according to https://stackoverflow.com/questions/14934081/antlr4-matching-all-input-alternatives-exaclty-once
header : header_title (header_tags | header_colour | header_position)* ;

header_title    : HEADER_TITLE    ':' ID                NEWLINE ;
header_tags     : HEADER_TAGS     ':' ID*               NEWLINE ;
header_colour   : HEADER_COLOUR   ':' NUMBER            NEWLINE ;
header_position : HEADER_POSITION ':' NUMBER ',' NUMBER NEWLINE ;

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
shortcut : '->' TEXT ('<<' KEYWORD_IF expression '>>')? (INDENT statement* DEDENT)? ;

if_statement
    : if_clause (else_if_clause)* (else_clause)? '<<' KEYWORD_ENDIF '>>'
    ;
if_clause :'<<' KEYWORD_IF expression '>>' statement* ;
else_if_clause : '<<' KEYWORD_ELSE_IF expression '>>' statement* ;
else_clause : '<<' KEYWORD_ELSE '>>' statement* ;

// this is a hack until I can work out exactly what the rules for setting are
set_statement
    : '<<' KEYWORD_SET variable KEYWORD_TO* expression '>>'
    | '<<' KEYWORD_SET expression '>>'
    ;

option_statement
    : '[[' OPTION_TEXT '|' OPTION_LINK ']]'
    | '[[' OPTION_TEXT ']]'
    ;

// the two unkey-worded commands
// solo function and an action
function : ACTION_TEXT '(' expression? (COMMAND_COMMA expression)* ')' ;
action : (ACTION_TEXT | BODY_NUMBER | '+' | '-')+ ;
function_statement : '<<' function '>>' ;
action_statement : '<<' action '>>' ;

line_statement : TEXT ;

// this feel a bit crude
// need to work on this
expression
    : '(' expression ')'                                                                           #expParens
    | <assoc=right>'-' expression                                                                  #expNegative
    | expression op=('*' | '/' | '%') expression                                                   #expMultDivMod
    | expression op=('+' | '-') expression                                                         #expAddSub
    | expression op=('<' | '>' | '<=' | '>=' ) expression                                          #expComparison
    | expression op=(OPERATOR_LOGICAL_EQUALS | OPERATOR_LOGICAL_NOT_EQUALS) expression             #expEquality
    | variable op=('*=' | '/=' | '%=') expression                                                  #expMultDivModEquals
    | variable op=('+=' | '-=') expression                                                         #expPlusMinusEquals
    | expression op=(OPERATOR_LOGICAL_AND | OPERATOR_LOGICAL_OR | OPERATOR_LOGICAL_XOR) expression #expAndOrXor
    | value                                                                                        #expValue
    ;

// can add in support for more values in here
value
    : BODY_NUMBER	 #valueNumber
    | KEYWORD_TRUE   #valueTrue
    | KEYWORD_FALSE  #valueFalse
	| variable		 #valueVar
	| COMMAND_STRING #valueString
	| function		 #valueFunc
    ;
variable
    : VAR_ID
    ;
