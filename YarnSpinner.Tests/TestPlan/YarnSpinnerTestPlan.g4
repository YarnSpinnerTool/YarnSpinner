grammar YarnSpinnerTestPlan;


testplan
    : run ('---' run)*
    | EOF
    ;

run
    : step+
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

hashtag: '#' .+? ;

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
    : 'set' variable=VARIABLE '=' value=BOOL #actionSetBool
    | 'set' variable=VARIABLE '=' value=NUMBER #actionSetNumber
    ;

actionSetSaliencyMode
    : 'saliency:' saliencyMode=IDENTIFIER
    ;

actionJumpToNode
    : 'node:' nodeName=.+?;

COMMENT: '#' ~[\r\n]* -> skip;
WS: [ \t\r\n]+ -> skip;
BOOL: 'true' | 'false';
IDENTIFIER: [a-zA-Z_][a-zA-Z0-9_]*;
VARIABLE: '$' IDENTIFIER ;
NUMBER: [0-9]+;
TEXT: '`' .*? '`';
