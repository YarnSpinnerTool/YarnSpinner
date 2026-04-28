---
name: UnknownCharacter
code: YS0016
tags: ["line-content"]
description: Character name not defined in project configuration
messageTemplate: "Unknown character: '{0}'"
messageValues: 
    - Character name
defaultSeverity: info
minimumSeverity: none
published: v3.2.0
generated_in: languageserver
examples:
    - script: |
        title: Start
        -=-
        Gary: I am a character that is not defined
        ===
# TODO: lower default severity to None - this diagnostic is highly dependent on the needs of the project (especially if character names are loc keys)
---
