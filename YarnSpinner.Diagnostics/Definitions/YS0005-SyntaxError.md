---
name: SyntaxError
tags: ["syntax"]
code: YS0005
description: Syntax error in Yarn script
messageTemplate: "Syntax error: {0}"
messageValues: 
    - The error
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        // Missing '1'
        <<if (1 + ) == 2>>
        <<endif>>
        ===
---

A syntax error exists in the Yarn script.