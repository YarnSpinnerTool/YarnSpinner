---
name: RogueChevronWithCommand
code: YS0064
tags: ["syntax"]
description: Extra chevrons on the same line as a command.
messageTemplate: "Command \"<<{0}>>\" has extra chevrons on the same line as it."
messageValues: 
    - Command content
summary: |
    Commands must start on their own line and should not have additional chevrons before or after. This is often indicative of a mistake.
defaultSeverity: warning
minimumSeverity: none
examples:
    - script: |
        title: Start
        -=-
        <<wait 1>>>
        ===
    - script: |
        title: Start
        -=-
        <<<wait 1>>
        ===
---