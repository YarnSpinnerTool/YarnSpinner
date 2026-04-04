---
name: SmartVariableLoop
code: YS0045
tags: ["semantic", "smart-variables"]
description: Smart variable creates a reference loop
messageTemplate: "Smart variables cannot contain reference loops (referencing {0} here creates a loop for the smart variable {1})."
messageValues: 
    - Variable reference
    - Smart variable
severity: error
---
