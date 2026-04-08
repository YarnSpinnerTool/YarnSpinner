---
name: NodeGroupMissingWhen
code: YS0031
tags: ["semantic", "node-groups"]
description: All nodes in a group must have a 'when' clause.
messageTemplate: "All nodes in the group '{0}' must have a 'when' clause (use 'when: always' if you want this node to not have any conditions)."
messageValues: 
    - Node group name
summary: |
    When some nodes in a group have 'when' clauses, all must have them.
severity: error
published: v3.2.0
---
