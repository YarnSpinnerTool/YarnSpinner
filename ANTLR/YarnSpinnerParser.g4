// Basic Parser grammar for YarnSpinner
// currently does not support shortcut -> syntax

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

header_title    : HEADER_TITLE    ':' ID+               NEWLINE ;
header_tags     : HEADER_TAGS     ':' ID*               NEWLINE ;
header_colour   : HEADER_COLOUR   ':' NUMBER            NEWLINE ;
header_position : HEADER_POSITION ':' NUMBER ',' NUMBER NEWLINE ;

body : BODY_ENTER statement* BODY_CLOSE ;

statement
    : if_statement
    | set_statement
    | action_statement
    | line_statement
    | option_statement
    ;

if_statement
    : '<<' KEYWORD_IF expression '>>' statement* ('<<' KEYWORD_ELSE_IF expression '>>' statement*)* ('<<' KEYWORD_ELSE '>>' statement*)* '<<' KEYWORD_ENDIF '>>'
    ;

// this is a hack until I can work out exactly what the rules for setting are
set_statement
    : '<<' KEYWORD_SET variable KEYWORD_TO* expression '>>'
    | '<<' KEYWORD_SET expression '>>'
    ;

option_statement
    : '[[' OPTION_TEXT '|' OPTION_LINK ']]'
    | '[[' OPTION_TEXT ']]'
    ;

action_statement
    : '<<' BODY_ID+ '>>'
    ;

line_statement
    : TEXT
    ;

// this feel a bit crude
// need to work on this
expression
    : '(' expression ')'                                                                           #expParens
    | expression op=('*' | '/' | '%') expression                                                   #expMultDivMod
    | expression op=('+' | '-') expression                                                         #expAddSub
    | expression op=('<' | '>' | '<=' | '>=' ) expression                                          #expComparison
    | expression op=(OPERATOR_LOGICAL_EQUALS | OPERATOR_LOGICAL_NOT_EQUALS) expression             #expEquality
    | expression op=('*=' | '/=' | '%=') expression                                                #expMultDivModEquals
    | expression op=('+=' | '-=') expression                                                       #expPlusMinusEquals
    | expression op=(OPERATOR_LOGICAL_AND | OPERATOR_LOGICAL_OR | OPERATOR_LOGICAL_XOR) expression #expAndOrXor
    | variable                                                                                     #expVariable
    | value                                                                                        #expValue
    ;

// can add in support for more values in here
value
    : BODY_NUMBER
    | KEYWORD_TRUE
    | KEYWORD_FALSE
    ;
variable
    : VAR_ID
    ;
