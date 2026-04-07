---
name: UnclosedScope
code: YS0007
tags: ["syntax"]
description: A control flow block was not properly closed
messageTemplate: "Unclosed scope: expected an <<{0}>> to match the <<{1}>> statement on line {2}"
messageValues: 
    - "\"endif\" or \"endonce\""
    - "\"if\" or \"once\""
    - line number of opening context
summary: |
    Unclosed control flow scope (missing endif, endonce, etc.)
severity: error
published: v3.2.0
---
