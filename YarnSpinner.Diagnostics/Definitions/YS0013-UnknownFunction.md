---
name: UnknownFunction
code: YS0013
tags: ["type-checker"]
description: Unknown function
messageTemplate: "Unknown function {0}"
messageValues: 
    - Function name
defaultSeverity: error
published: v3.2.1

# TODO: this will be moved into the compiler when async functions land
# (and function declarations become mandatory)
generated_in: languageserver 

examples:
    - script: |
        title: Start
        -=-
        Using the unknown function {guid()}
        ===
---
