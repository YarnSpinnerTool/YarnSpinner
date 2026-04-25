---
name: MultipleLineOrShadowIDsOnALine

code: YS0062

tags: ["shadow-lines", "line-ids", "semantic"]

description: Dialogue has multiple '#line' or '#shadow' IDs.

messageTemplate: "Dialogue has multiple '#line' or '#shadow' IDs."

summary: |
    Lines of dialogue can only have a single line ID or shadow line, having multiple means we cannot uniquely identify this line.

defaultSeverity: error

minimumSeverity: error

examples:
    - script: |
        title: Start
        -=-
        the line #line:abc123 #line:abc123
        ===
---

