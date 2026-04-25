---
name: LanguageVersionTooLow
code: YS0036
tags: ["project"]
description: Language version too low for feature.
messageTemplate: "{0}"
messageValues: 
    - Message
summary: |
    A language feature was used that requires a newer Yarn Spinner project version.
defaultSeverity: error
published: v3.2.0
examples:
    - script: skip_test_generation
---
