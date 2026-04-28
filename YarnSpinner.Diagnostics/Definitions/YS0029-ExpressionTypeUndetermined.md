---
name: ExpressionTypeUndetermined
code: YS0029
tags: ["semantic", "type-checker"]
description: Can't determine the type of an expression.
messageTemplate: "Can't determine the type of the expression {0}."
messageValues: 
    - Expression
summary: |
    The compiler could not resolve the type of this expression.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        Using {$undeclared} in a line
        ===

---
   