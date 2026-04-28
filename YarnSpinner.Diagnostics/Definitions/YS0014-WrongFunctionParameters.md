---
name: WrongFunctionParameters
code: YS0014
tags: ["type-checker"]
description: Function called with incorrect parameters
messageTemplate: "Invalid function call: {0}"
messageValues: 
    - Function name
defaultSeverity: error
published: v3.2.1
examples:
    - script: |
        title: Start
        -=-
        => this line should error <<if visited("Start", 1)>>
        => this line should also error <<if visited(1)>>
        ===
---
