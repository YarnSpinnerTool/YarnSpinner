---
name: ImplicitVariableTypeConflict
code: YS0001
tags: ["type-checker"]
description: Variable has conflicting implicit type declarations
messageTemplate: "Variable {0} has been implicitly declared with multiple types: {1}"
messageValues: 
    - Variable name
    - Type names
summary: |
    This error occurs when a variable is used with different types across
    different files or contexts without an explicit declaration, and the
    compiler cannot determine which type is correct.
severity: error
---

