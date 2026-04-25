---
name: UndefinedNode
code: YS0012
tags: ["semantic"]
description: Jump to undefined node
messageTemplate: "Jump to undefined node: '{0}'"
messageValues: 
    - Node title
defaultSeverity: warning
minimumSeverity: none
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <<detour madeUp>>
        ===
    - script: |
        title: Start
        -=-
        <<jump madeUp>>
        ===

---
        