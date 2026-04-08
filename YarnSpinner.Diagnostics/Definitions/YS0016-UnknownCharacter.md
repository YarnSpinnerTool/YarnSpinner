---
name: UnknownCharacter
code: YS0016
tags: ["line-content"]
description: Character name not defined in project configuration
messageTemplate: "Unknown character: '{0}'"
messageValues: 
    - Character name
severity: info
published: v3.2.0
# TODO: lower default severity to None - this diagnostic is highly dependent on the needs of the project (especially if character names are loc keys)
---
