---
name: UnknownCommand
code: YS0060
tags: ["semantic"]
description: Command is not recognized or has invalid syntax
messageTemplate: "Unknown command: {0}"
messageValues: 
    - Command name
defaultSeverity: warning
minimumSeverity: none
published: v3.2.0
generated_in: languageserver
examples:
    - script: |
        title: Start
        -=-
        <<made up command>>
        ===
---
