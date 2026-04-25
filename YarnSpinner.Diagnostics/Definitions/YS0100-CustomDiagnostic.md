---
# The internal name of the diagnostic. Must be a valid C# identifier.
name: CustomDiagnostic

# The unique code of the diagnostic.
code: YS0100

# A list of descriptive tags used to categorise this diagnostic
tags: []

# A one-sentence summary of the diagnostic
description: A user-defined diagnostic has been generated.

# The template used for producing the diagnostic's message.
# Template placeholders may be used multiple times in the template.
messageTemplate: "{0}"

# The descriptions of the placeholders. There must be as many descriptions as there
# are unique placeholders.
messageValues: 
    - Diagnostic message

# An optional short summary of when this issue occurs.
summary: |
    This diagnostic is created by custom user code.

# The default severity of the diagnostic. Allowed values are 'error', 'warning', 'info', 'none'; default is 'error'.
defaultSeverity: warning

# The minimum severity of the diagnostic. Allowed values are 'error', 'warning', 'info', 'none'; default is the value of defaultSeverity.
minimumSeverity: none
---

