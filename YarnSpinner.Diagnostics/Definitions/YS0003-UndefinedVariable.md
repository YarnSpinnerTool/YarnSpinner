---
name: UndefinedVariable
code: YS0003
tags: ["type-checker"]
description: Variable used without being declared
messageTemplate: "Variable '{0}' is used but not declared. Declare it with: <<declare {0} = value>>"
messageValues: 
    - Variable name
severity: warning
---
