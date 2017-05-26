// Basic Parser grammar for YarnSpinner
// currently does not support shortcut -> syntax

parser grammar YarnSpinnerParser;

options { tokenVocab=YarnSpinnerLexer; }

node: BODY_ENTER statement* BODY_CLOSE ;

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

set_statement
    : '<<' KEYWORD_SET variable KEYWORD_TO expression '>>'
    ;

action_statement
    : '<<' ID+ '>>'
    ;

option_statement
    : '[[' OPTION_TEXT '|' OPTION_LINK ']]'
    | '[[' OPTION_TEXT ']]'
    ;

line_statement
    : TEXT
    ;

// this feel a bit crude
// need to work on this
expression
    : '(' expression ')'                                                               #expParens
    | value                                                                            #expValue
    | expression op=('*' | '/') expression                                             #expMultDiv
    | expression op=('+' | '-') expression                                             #expAddSub
    | expression op=(OPERATOR_LOGICAL_EQUALS | OPERATOR_LOGICAL_NOT_EQUALS) expression #expEquality
    | expression op=('<' | '>' | '<=' | '>=' ) expression                              #expComparison
    | variable                                                                         #expVariable
    ;

//ok so a command is << any text is allowed in here especially spaces>>
//a function is <<anyText>>
// function can't have spaces and has a parentheses at the end
// <<hello()>> is a function
// <<hello() there is a command>>

// can add in support for more values in here
value
    : NUMBER
    | KEYWORD_TRUE
    | KEYWORD_FALSE
    ;
variable
    : VAR_ID
    ;
