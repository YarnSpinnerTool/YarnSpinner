---
name: UnreachableCode
code: YS0008
tags: ["semantic"]
description: Unreachable code detected
messageTemplate: "Unreachable code detected"
summary: |
    Code that will never be executed has been detected.
defaultSeverity: warning
minimumSeverity: none
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <<if true>>
            internal line
        <<else>>
            this line can't be reached
        <<endif>>
        ===

        title: Another
        -=-
        <<jump Start>>
        this line can't be reached
        ===

# TODO: run basic block analysis to produce it; add a flag to project that disables these checks
---
