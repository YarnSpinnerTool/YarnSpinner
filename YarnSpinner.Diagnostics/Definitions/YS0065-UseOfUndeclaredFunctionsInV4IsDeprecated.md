---
name: UseOfUndeclaredFunctionsInV4IsDeprecated
code: YS0065
tags: ["type-checker"]
description: Use of undeclared functions is deprecated
messageTemplate: "Function {0} is unknown. You should declare it in your game so that the Yarn Spinner compiler is aware of it. This will become an error in future versions."
messageValues: 
    - Function name
defaultSeverity: warning
published: v3.3.0

examples:
    - project_version: 3
      script: |
        title: Start
        -=-
        Using the unknown function {guid()}
        ===
    - project_version: 4
      script: |
        title: Start
        -=-
        Using the unknown function {guid()}
        ===
---
