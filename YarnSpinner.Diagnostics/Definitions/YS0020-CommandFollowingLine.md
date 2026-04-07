---
name: CommandFollowingLine
code: YS0020
tags: ["semantic", "line-content"]
description: Line content before a command.
messageTemplate: "Command \"{0}\" found following a line of dialogue. Commands should start on a new line."
messageValues: 
    - Content
summary: |
    Commands must start on their own line. Only line conditions are allowed to follow a line of dialogue.
severity: error
published: v3.2.0
---
