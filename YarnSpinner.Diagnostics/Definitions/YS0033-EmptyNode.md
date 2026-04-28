---
name: EmptyNode
code: YS0033
tags: ["semantic"]
description: Node is empty and will not be compiled.
messageTemplate: "Node \"{0}\" is empty and will not be included in the compiled output."
messageValues: 
    - Node title
summary: |
    An empty node has no statements and will be excluded from compilation.
defaultSeverity: warning
minimumSeverity: none
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        ===
---
        