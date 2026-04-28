---
name: DuplicateNodeTitle
code: YS0011
tags: ["semantic"]
description: Duplicate node title
messageTemplate: "Duplicate node title: '{0}'"
messageValues: 
    - Node title
summary: |
    This diagnostic is not emitted for node groups where nodes
    share a title but have different `when:` clauses.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        empty node
        ===

        title: Start
        -=-
        empty node
        ===
---
