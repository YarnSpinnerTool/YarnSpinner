---
name: EnumDeclarationError
code: YS0035
tags: ["enums"]
description: Enum declaration error
messageTemplate: "{0}"
messageValues: 
    - Error message
summary: |
    This is a catch-all message for issues related to enums.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <<enum Fish>>
            <<case Shark = 1>>
            <<case Sunfish = 2>>
            <<case Crab = "Not A Fish">>
        <<endenum>>
        ===
# TODO: split up into multiple smaller diags
---
