---
name: TypeMismatch
code: YS0002
tags: ["type-checker"]
description: A type mismatch occurred during type checking.
messageTemplate: "Type mismatch: expected {0}, got {1}"
messageValues: 
    - Expected type
    - Actual type
defaultSeverity: error
published: v3.2.0

# TODO: consider using 'additional information' objects to indicate extra info about this mismatch
# see LSP 'diagnostic related information'
---
