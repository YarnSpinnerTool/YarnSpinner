---
name: NullDefaultValue
code: YS0046
tags: ["semantic", "type-checker"]
description: Variable declaration has a null default value.
messageTemplate: "Variable declaration {0} (type {1}) has a null default value. This is not allowed."
messageValues: 
    - Variable name
    - Type name
severity: error
published: v3.2.0
deprecated: v3.2.1
deprecation_note: This error is an internal error - the compiler will not reach a point where it produces default values for variables if there is a syntax error, and that's the only way you could have 'no default value'.
---