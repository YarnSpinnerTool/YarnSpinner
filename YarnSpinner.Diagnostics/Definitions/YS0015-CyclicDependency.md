---
name: CyclicDependency
code: YS0015
tags: ["semantic"]
description: Cyclic dependency between nodes detected.
messageTemplate: "Cyclic dependency detected: {0}"
messageValues: 
    - Error message
defaultSeverity: info
minimumSeverity: none
published: v3.2.0

# TODO: this is currently the case because the logic for it is in the language server,but it could (should?) be moved to the compiler
generated_in: languageserver 

examples:
    - script: |
        title: Start
        -=-
        <<jump Another>>
        ===

        title: Another
        -=-
        <<jump Start>>
        ===
# TODO: set minimum and default severity to None
# TODO: move this into a 'stylistic diagnostics' group?
---
