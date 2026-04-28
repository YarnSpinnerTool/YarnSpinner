---
name: DuplicateLineID
code: YS0018
tags: ["line-ids","semantic"]
description: Duplicate line ID
messageTemplate: "Duplicate line ID '{0}'"
messageValues: 
    - Line ID
summary: |
    All line IDs in a Yarn Spinner project must be unique.
defaultSeverity: error
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        first line #line:abc123
        secondline #line:abc123
        ===
---
