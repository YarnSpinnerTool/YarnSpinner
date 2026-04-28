---
name: WrongCommandParameterCount
code: YS0061
tags: ["semantic"]
description: Command was called with the wrong number of parameters
messageTemplate: "Command {0} was called with {1} parameters, but expected {2}"
messageValues: 
    - Command name
    - Number of parameters used
    - Number of parameters expected
defaultSeverity: warning
minimumSeverity: none
published: v3.2.0
generated_in: languageserver
examples:
    - script: |
        title: Start
        -=-
        <<wait 1 2>>
        ===
---
