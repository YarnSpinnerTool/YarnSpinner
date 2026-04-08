---
name: InvalidLibraryFunction
code: YS0034
tags: ["semantic"]
description: Function cannot be used in Yarn Spinner scripts
messageTemplate: "Function {0} cannot be used in Yarn Spinner scripts: {1}"
messageValues: 
    - Function name
    - Reason
summary: |
    A library function has an incompatible signature for use in Yarn scripts.
severity: error
published: v3.2.0
---
