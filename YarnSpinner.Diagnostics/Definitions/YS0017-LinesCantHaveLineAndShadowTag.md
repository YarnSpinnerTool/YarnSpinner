---
name: LinesCantHaveLineAndShadowTag
code: YS0017
tags: ["line-ids","semantic"]
description: Lines cannot have both a '#line' tag and a '#shadow' tag.
messageTemplate: "Lines cannot have both a '#line' tag and a '#shadow' tag."
summary: |
    Shadow tags represent copies of another line elsewhere, and don't get their own line ID.
severity: error
---
