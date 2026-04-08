---
name: UnenclosedCommand
code: YS0022
tags: ["syntax"]
description: Command keyword appearing outside of command markers
messageTemplate: "'{0}' command must be enclosed in '<<' and '>>'. Did you mean '<<{0} ...'?"
messageValues: 
    - Command keyword
severity: warning
published: v3.2.0
---
