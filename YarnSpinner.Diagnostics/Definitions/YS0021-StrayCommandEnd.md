---
name: StrayCommandEnd
code: YS0021
tags: ["semantic"]
description: Stray command end marker without matching start marker.
messageTemplate: "Stray '>>' without matching '<<'. Did you forget to open the command?"
defaultSeverity: warning
minimumSeverity: none
published: v3.2.0
examples:
    - script: |
        title: Start
        -=-
        <some command>>
        ===
---
   