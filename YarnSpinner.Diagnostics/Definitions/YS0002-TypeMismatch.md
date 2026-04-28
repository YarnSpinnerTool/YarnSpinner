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
deprecated: v3.2.1
deprecation_note: This diagnostic is no longer emitted, and is replaced by YS0050.

# TODO: consider using 'additional information' objects to indicate extra info about this mismatch
# see LSP 'diagnostic related information'
---
