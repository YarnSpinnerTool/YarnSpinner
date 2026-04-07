---
# The internal name of the diagnostic. Must be a valid C# identifier.
name: DeclarationValueDoesntMatchType

# The unique code of the diagnostic.
code: YS0053

# A list of descriptive tags used to categorise this diagnostic
tags: ["semantic", "type-checker"]

# A one-sentence summary of the diagnostic
description: Variable's declared type doesn't match the type of its initial value

# The template used for producing the diagnostic's message.
# Template placeholders may be used multiple times in the template.
messageTemplate: "{0} is declared to be a {1}, but its initial value '{2}' is a {3}"

# The descriptions of the placeholders. There must be as many descriptions as there
# are unique placeholders.
messageValues: 
    - Variable name
    - Declared type
    - Expression
    - Expression's type

# An optional short summary of when this issue occurs.
# summary: |
#     This error occurs when a variable is used with different types across
#     different files or contexts without an explicit declaration, and the
#     compiler cannot determine which type is correct.

# The default severity of the diagnostic. Allowed values are 'error', 'warning' and 'info'; default is 'error'.
severity: error
---

