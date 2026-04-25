---
name: UnusedVariable
code: YS0010
tags: ["semantic"]
description: Variable is declared but never used
messageTemplate: "Variable '{0}' is declared but never used"
messageValues: 
    - Variable name
defaultSeverity: info
minimumSeverity: none
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <<declare $somevar = 123>>
        <<declare $usedVar = true>>
        ===

        title: Another
        -=-
        => line that uses usedVar <<if $usedVar>>
        ===
---
