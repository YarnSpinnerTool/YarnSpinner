---
name: InvalidMemberAccess
code: YS0038
tags: ["semantic"]
description: Invalid member access
messageTemplate: "{0}"
messageValues: 
    - Error message
summary: |
    A type member access could not be resolved.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <<enum Test>>
            <<case Item>>
        <<endenum>>

        <<set $x = Test.Failure>>
        ===

---
