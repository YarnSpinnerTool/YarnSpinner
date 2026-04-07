---
name: TypeInferenceFailure
code: YS0028
tags: ["type-checker", "semantic"]
description: Can't determine type of variable given its usage.
messageTemplate: "Can't determine type of {0} given its usage. Manually specify its type with a declare statement."
messageValues: 
    - Variable name
summary: |
    The compiler could not infer the type of this variable from how it is used.
severity: error
published: v3.2.0
---
