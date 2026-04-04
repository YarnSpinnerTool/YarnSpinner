---
name: LineContentAfterCommand
code: YS0019
tags: ["semantic", "line-content"]
description: Line content after a command
messageTemplate: "Dialogue \"{0}\" content found following a command. Commands should be on their own line."
messageValues: 
    - Dialogue content
summary: |
    Commands must start on their own line and should not have dialogue following them. This is often indicative of a mistake.
severity: warning
---
        