---
name: TypeSolverTimeout
code: YS0047
tags: ["type-checker"]
description: Expression failed to resolve in a reasonable time.
messageTemplate: "Expression failed to resolve in a reasonable time ({0}). Try simplifying this expression."
messageValues: 
    - Time limit in seconds
summary: |
    The type solver exceeded its time limit while resolving this expression.
defaultSeverity: error
published: v3.2.0
skip_test_generation: true # Testing a timeout is unfeasible in CI
---