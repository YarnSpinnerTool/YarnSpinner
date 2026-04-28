---
name: ShadowLinesCantHaveExpressions
code: YS0043
tags: ["shadow-lines", "line-ids", "semantic"]
description: "Shadow lines contains an expression"
messageTemplate: "Shadow lines must not have expressions"
summary: |
    Shadow lines must be text, and not contain any expressions.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        this is a normal line with an expression {1 + 2} in it #line:abc123
        this is a normal line with an expression {1 + 2} in it #shadow:abc123
        ===
---
