parser grammar YarnSpinnerV1Parser;

options { tokenVocab=YarnSpinnerV1Lexer; }

dialogue 
    : (file_hashtag*) node+ 
    ;

// File-global hashtags, which precede all nodes
file_hashtag
    : HASHTAG HASHTAG_TEXT TEXT_COMMANDHASHTAG_NEWLINE
    ;

node
    : header+  BODY_START  body BODY_END
    ;

header 
    : header_key=ID HEADER_DELIMITER  header_value=REST_OF_LINE? HEADER_NEWLINE
    ;

body
    : statement*
    ;

statement
    : line_statement
    | if_statement
    | set_statement
    | option_statement
    | shortcut_option_statement
    | call_statement
    | command_statement
    | INDENT statement* DEDENT
    ;

line_statement
    : 
        line_formatted_text // text, interspersed with expressions
        line_condition? // a line condition
        hashtag*  // any number of hashtags
        (TEXT_NEWLINE|TEXT_COMMANDHASHTAG_NEWLINE) // the end of the line
    ;

line_formatted_text
    : ( TEXT // a chunk of text to show to the player
        | TEXT_EXPRESSION_START expression EXPRESSION_END // an expression to evaluate
      | (FORMAT_FUNCTION_START|TEXT_FORMAT_FUNCTION_START) format_function FORMAT_FUNCTION_END // a format function
      )* 
    ;

format_function
    : function_name=FORMAT_FUNCTION_ID FORMAT_FUNCTION_EXPRESSION_START variable EXPRESSION_END key_value_pair*
    ;

// key="value"
key_value_pair
    : pair_key=FORMAT_FUNCTION_ID FORMAT_FUNCTION_EQUALS pair_value=FORMAT_FUNCTION_STRING #KeyValuePairNamed
    | pair_key=FORMAT_FUNCTION_NUMBER FORMAT_FUNCTION_EQUALS pair_value=FORMAT_FUNCTION_STRING #KeyValuePairNumber
    ;

hashtag
    : (TEXT_HASHTAG|TEXT_COMMANDHASHTAG_HASHTAG|HASHTAG_TAG|BODY_HASHTAG|HASHTAG) text=HASHTAG_TEXT
    ;

line_condition
    : (TEXT_COMMANDHASHTAG_COMMAND_START|TEXT_COMMAND_START) COMMAND_IF expression EXPRESSION_COMMAND_END
    ;

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

value
    : NUMBER         #valueNumber
    | KEYWORD_TRUE   #valueTrue
    | KEYWORD_FALSE  #valueFalse
    | variable       #valueVar
    | STRING #valueString
    | KEYWORD_NULL   #valueNull
    | function       #valueFunc

    ;
variable
    : VAR_ID
    ;

function 
    : FUNC_ID '(' expression? (COMMA expression)* ')' ;

if_statement
    : if_clause                                 // <<if foo>> statements...
      else_if_clause*                           // <<elseif bar>> statements.. (can have zero or more of these)
      else_clause?                              // <<else>> statements (optional)
      COMMAND_START COMMAND_ENDIF COMMAND_END	// <<endif>>
    ;

if_clause
    : COMMAND_START COMMAND_IF expression EXPRESSION_COMMAND_END statement*
    ;

else_if_clause
    : COMMAND_START COMMAND_ELSEIF expression EXPRESSION_COMMAND_END statement*
    ;

else_clause
    : COMMAND_START COMMAND_ELSE COMMAND_END statement*
    ;

set_statement
    : COMMAND_START COMMAND_SET VAR_ID OPERATOR_ASSIGNMENT expression EXPRESSION_COMMAND_END #setVariableToValue
    | COMMAND_START COMMAND_SET expression EXPRESSION_COMMAND_END #setExpression
    ;

call_statement
    : COMMAND_START COMMAND_CALL function EXPRESSION_COMMAND_END
    ;

command_statement
    : COMMAND_START command_formatted_text COMMAND_TEXT_END (hashtag* TEXT_COMMANDHASHTAG_NEWLINE)?
    ;

command_formatted_text
	: (COMMAND_TEXT|COMMAND_EXPRESSION_START expression EXPRESSION_END)*
	;

shortcut_option_statement
    : shortcut_option+
    ;

shortcut_option
    : '->' line_statement (INDENT statement* DEDENT)?
    ;

option_statement
    : '[[' option_formatted_text '|' NodeName=OPTION_ID ']]' (hashtag* TEXT_COMMANDHASHTAG_NEWLINE)? #OptionLink
    | '[[' NodeName=OPTION_TEXT ']]' #OptionJump
    ;

option_formatted_text
    : (
        OPTION_TEXT 
        | OPTION_EXPRESSION_START expression EXPRESSION_END 
        | OPTION_FORMAT_FUNCTION_START format_function FORMAT_FUNCTION_END
      )+
    ;