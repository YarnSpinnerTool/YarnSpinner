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
---