---
name: SmartVariableReadOnly
code: YS0030
tags: ["semantic", "type-checker", "smart-variables"]
description: Smart variable cannot be modified.
messageTemplate: "{0} cannot be modified (it's a smart variable and is always equal to {1})"
messageValues: 
    - Variable name
    - Variable definition expression
summary: |
    Smart variables are read-only computed values and cannot be assigned to.
severity: error
published: v3.2.0
---
