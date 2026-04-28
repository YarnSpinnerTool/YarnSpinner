---
name: ShadowLinesMustHaveSameTextAsSource
code: YS0044
tags: ["shadow-lines", "line-content"]
description: Shadow line contains different text than its source
messageTemplate: "Shadow lines must have the same text as their source"
summary: |
    Shadow lines are copies of their source lines, and must have the exact same text as their source line.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        This is a line. #line:source #apple
        This is also a line. #shadow:source #banana
        ===
---
