---
name: MissingDelimiter
code: YS0004
tags: ["syntax"]
description: Missing node delimiter (=== or ---)
messageTemplate: "Missing node delimiter"
summary: |
    Node is missing its start or end delimiter.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        Line
---
