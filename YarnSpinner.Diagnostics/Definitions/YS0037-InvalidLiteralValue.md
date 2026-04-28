---
name: InvalidLiteralValue
code: YS0037
tags: ["semantic"]
description: Invalid literal value
messageTemplate: "{0}"
messageValues: 
    - Error message
summary: |
    A constant literal value could not be parsed or is of an unexpected type.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <<enum Example>>
            <<case Test = max(1,2)>>
        <<endenum>>
        ===

---
