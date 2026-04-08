---
name: InvalidCommand
code: YS0014
tags: ["semantic"]
description: Command is not recognized or has invalid syntax
messageTemplate: "Invalid command: {0}"
messageValues: 
    - Command name
severity: warning
published: v3.2.0

# TODO: split into 'unknown command' and 'wrong parameter count' diags
---
