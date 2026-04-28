---
name: InvalidNodeName
code: YS0027
tags: ["syntax"]
description: Node title or subtitle contains invalid characters
messageTemplate: "Unexpected '{1}' in node {0}. Titles can only contain letters, numbers, and underscores."
messageValues: 
    - "\"title\" or \"subtitle\""
    - The invalid character
summary: |
    Node titles and subtitles can only contain letters, numbers, and underscores.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: abc.123
        -=-
        empty node
        ===
---
