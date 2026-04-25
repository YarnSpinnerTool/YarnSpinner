---
name: UnknownLineIDForShadowLine
code: YS0042
tags: ["shadow-lines", "line-ids", "semantic"]
description:  Unknown line ID for shadow line.
messageTemplate: "Unknown line ID {0} for shadow line"
messageValues: 
    - Line ID
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        this is the normal line #line:abc123
        this is the normal line #shadow:abc123
        this is the normal line #shadow:abc124
        ===
---
