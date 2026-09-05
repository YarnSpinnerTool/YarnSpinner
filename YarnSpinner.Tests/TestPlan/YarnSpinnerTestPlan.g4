grammar YarnSpinnerTestPlan;


testplan
    : (environment '---')? run ('---' run)* EOF
    ;

run
    : start? step+
    ;

environment
    : 'environment:' contextName=IDENTIFIER
    ;

start
    : 'start:' nodeName=IDENTIFIER
    ;

step
    : lineExpected
    | optionExpected
    | commandExpected
    | stopExpected
    | actionSelect
    | actionSet
    | actionJumpToNode
    | actionSetSaliencyMode
    ;

hashtag: HASHTAG_CONTENT ;

lineExpected
    : 'line:' TEXT hashtag* #lineWithSpecificTextExpected
    | 'line:' '*' hashtag* #lineWithAnyTextExpected
    ;

optionExpected
    : 'option:' TEXT hashtag* (isDisabled='[disabled]')? 
    ;

commandExpected
    : 'command:' TEXT
    ;

stopExpected
    : 'stop'
    ;

actionSelect
    : 'select:' option=NUMBER
    ;

actionSet
    : 'set:' variable=VARIABLE '=' value=BOOL #actionSetBool
    | 'set:' variable=VARIABLE '=' value=NUMBER #actionSetNumber
    ;

actionSetSaliencyMode
    : 'saliency:' saliencyMode=IDENTIFIER
    ;

actionJumpToNode
    : 'node:' nodeName=IDENTIFIER;

COMMENT: '//' ~[\r\n]* -> skip;
WS: [ \t\r\n]+ -> skip;
BOOL: 'true' | 'false';
IDENTIFIER: [a-zA-Z_][a-zA-Z0-9_]*;
HASHTAG_CONTENT: '#' ~[ \t\r\n#]+ ;
VARIABLE: '$' IDENTIFIER ;
NUMBER: [0-9]+;
TEXT: '`' .*? '`';
