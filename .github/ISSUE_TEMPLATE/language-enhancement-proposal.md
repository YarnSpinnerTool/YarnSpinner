---
name: Language enhancement proposal
about: Propose a change to the Yarn language!
title: 'Proposal: '
labels: proposal
assignees: ''

---

<!-- NOTE: This issue template is for proposing changes to the Yarn language itself. If you have a feature request for the Yarn Spinner compiler and tools that doesn't involve changing the Yarn language, use the Feature Request template. -->

## Introduction

A short description of what the feature is. Try to keep it to a single-paragraph "elevator pitch" so the reader understands what problem this proposal is addressing.

## Rationale

Describe the problems that this proposal seeks to address. If the problem is that some common task is currently hard to express in Yarn Spinner, show how one can currently get a similar effect, and describe its drawbacks. If it's completely new functionality that can't be emulated in the current language, explain why this new functionality would help writers or programmers work with Yarn code.

## Proposed solution

Describe your solution to the problem. Provide examples and describe how they work. Show how your solution is better than current workarounds: is it cleaner, safer, or more efficient?

## Detailed design

Describe the design of the solution in detail. If it involves new syntax in the language, show the additions and changes to the Yarn grammar. If it's a new API, show the full API and its documentation comments detailing what it does. The detail in this section should be sufficient for someone who is not one of the proposal authors to be able to reasonably implement the feature.

## Backwards Compatibility

Describe the impact that your solution will have on code written in the most recent shipping version of the language. If your proposed changes mean that existing code would need to be changed in order to work, describe in detail what changes would be required, and describe an algorithm (pseudocode is fine) for detecting where these changes are necessary, and how an automated upgrader would either make changes or flag that a human must make changes.

## Alternatives considered

Describe alternative approaches to addressing the same problem, and why you chose this approach instead.

## Acknowledgments

If significant changes or improvements suggested by members of the community were incorporated into the proposal as it developed, take a moment here to thank them for their contributions. Designing the Yarn language is a collaborative process, and everyone's input should receive recognition!

<!-- This issue template is heavily inspired by the Swift Evolution proposal format: https://github.com/apple/swift-evolution/blob/main/proposal-templates/0000-swift-template.md -->
