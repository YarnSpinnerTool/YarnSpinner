# Yarn Spinner Release Process

This document outlines the steps taken to do a release of Yarn Spinner, Yarn Spinner for Unity, and Yarn Spinner Console, as well as the documentation and post-release messaging.

## Pre-Release

During pre-release, we figure out what the most important things we need to communicate about the upcoming release are, and prepare posts and material to go out immediately after the packages are built and released.

We primarily communicate via Twitter and Discord.

- Build a list of new features and important changes
- Write up the announcement for the Discord, highlighting important changes, links to new documentation, and (if applicable) a screenshot. Aim for 100 words or less, and with a friendly, casual, excited tone.

For example, here's v2.1's announcement:

> Hey @everyone! We're delighted to announce that Yarn Spinner 2.1 has just been released!
> 
> Full release notes are here: https://github.com/YarnSpinnerTool/YarnSpinner-Unity/releases/tag/v2.1.0
> 
> In this release, we've made it a lot easier to write your own custom dialogue views, by simplifying the API a lot! Your code now needs to do a lot less. Read more about it in the documentation! https://docs.yarnspinner.dev/using-yarnspinner-with-unity/components/dialogue-view/custom-dialogue-views 
> 
> Please note: If you already have existing custom Dialogue Views, you'll need to update them to use the new API. However, it's a lot simpler to deal with! For an example of how to use the new API, take a look at the sample code in the YarnSpinnerTool/ExampleProjects repo: https://github.com/YarnSpinnerTool/ExampleProjects/blob/main/UtilityScripts/SimpleSpeechBubbleLineView.cs
> 
> We've also added a long-requested feature: you can now jump to a variable or other expression!
> ```
> <<set $destination = "Home">>
> <<jump {$destination}>>
> ```
> 
> This release also contains a number of bug fixes, including:
> - That bug where the built-in functions didn't work (sorry, sorry)
> - That bug where all lines would vanish until the next re-import if you modified a Yarn script in Play Mode
> 
> We hope you like it!

* Draft Twitter posts for release
  * Example tweets:
    * Yarn Spinner 2.1's release tweet: https://twitter.com/YarnSpinnerTool/status/1494207420222296067
    * Try Yarn Spinner's release tweet: https://twitter.com/YarnSpinnerTool/status/1504305532777435144
  * (Release tweets generally do better when there's an image to post, even if that image is nothing but text on a background. Contact @desplesda or @TheMartianLife if you need graphics to post, or if you need access to the marketing assets.)

## Yarn Spinner Core

- Ensure that current build has passed checks: https://github.com/YarnSpinnerTool/YarnSpinner/actions/workflows/build.yml
- Review `CHANGELOG.md`:
  - Check for typos, grammar, etc
  - Rename `## [Unreleased]` section to mark the release:
    - Format: `## [VERSION] YYYY-MM-DD`
    - Note that the version number doesn't include the `v`!
  - Add a new `## [Unreleased]` header, ready for new entries
- Once we're happy with the final, tag it!
  - Tags must be of format `vX.Y.Z` - the release action is looking for this
  - `git tag` it
  - Push the repo and tags 
  - Release action will automatically run
    - https://github.com/YarnSpinnerTool/YarnSpinner/actions/workflows/release.yml
    - It'll download it, test it, package it, and release it to NuGet.
    - A new draft GitHub release will be created by this run - go to https://github.com/YarnSpinnerTool/YarnSpinner/releases, review the release notes (which were extracted from CHANGELOG.md), and once satisfied, release the draft.

## Yarn Spinner for Unity

- Update the project to use the new DLLs that Yarn Spinner Core just built:
  - https://github.com/YarnSpinnerTool/YarnSpinner-Unity/actions/workflows/update_dlls.yml
  - Run the workflow, using the `main` branch
  - The workflow will download Yarn Spinner, build it, update the Unity project, and open a new pull request for the change
  - Merge this new pull request
- Verify that the new build passes its tests
  - https://github.com/YarnSpinnerTool/YarnSpinner-Unity/actions/workflows/test.yml
- Update `AssemblyInfo.cs`:
  - For each of the following files in the YarnSpinner-Unity repo:
    - `Runtime/AssemblyInfo.cs` 
    - `Editor/AssemblyInfo.cs`
  - Update the `AssemblyVersion`, `AssemblyFileVersion` and `AssemblyInformationalVersion` versions to match the new version. 
    - For `AssemblyVersion` and `AssemblyFileVersion`, these values **must** be formatted as `MAJOR.MINOR.PATCH.BUILD`.
    - We generally use zero for the build number, and generally use the same value in `AssemblyInformationalVersion`.
- Review `CHANGELOG.md`:
  - Check for typos, grammar, etc
  - Rename `## [Unreleased]` section to mark the release:
    - Format: `## [VERSION] YYYY-MM-DD`
    - Note that the version number doesn't include the `v`!
  - Add a new `## [Unreleased]` header, ready for new entries
- Update Unity package version
  - https://github.com/YarnSpinnerTool/YarnSpinner-Unity/blob/main/package.json
  - Package version should match the tag version, without the `v`.
  - (This is a very important step, because if it's missed, OpenUPM's build will fail.)
  - Commit and push the change.
- Once we're happy with the final, tag it!
  - Tags must be of format `vX.Y.Z` - the release action is looking for this
  - `git tag` it
  - Push the repo and tags.
  - Release action will automatically run
    - https://github.com/YarnSpinnerTool/YarnSpinner-Unity/actions/workflows/release.yml
    - A new draft GitHub release will be created by this run - go to https://github.com/YarnSpinnerTool/YarnSpinner/releases, review the release notes (which were extracted from CHANGELOG.md), and once satisfied, release the draft.
    - After a while (generally about 5-10 minutes), OpenUPM will notice the new tag, and start building it: https://openupm.com/packages/dev.yarnspinner.unity/?subPage=pipelines
    - If the 'latest' version doesn't change to the new version after a few minutes (generally less than 10 minutes after it first appears), or if the build is marked as failed, this generally indicates a build failure.
      - This is usually caused by the package.json version not being updated. In this case, delete the tag in GitHub, fix the file, push and re-tag it (with the same tag name), and OpenUPM should notice the updated tag and try again. (You will need to delete the duplicate GitHub release that the previous attempt produced.)

## Yarn Spinner Console

- Update the version of `YarnSpinner` and `YarnSpinner.Compiler` in `src/YarnSpinner.Console/ysc.csproj` to the new version you released earlier.
- Review `CHANGELOG.md`:
  - Check for typos, grammar, etc
  - Rename `## [Unreleased]` section to mark the release:
    - Format: `## [VERSION] YYYY-MM-DD`
    - Note that the version number doesn't include the `v`!
  - Add a new `## [Unreleased]` header, ready for new entries
- Tag and push. The build and release action will run automatically:
  - https://github.com/YarnSpinnerTool/YarnSpinner-Console/actions/workflows/build.yml
  - A new release will be created in GitHub. 
  - Review it and release it when satisfied.

## Documentation

- Branch the current documentation in `gitbook`, in the `YSDocs` repo, into a new branch.
  - For example, if the current release number is 2.1, then branch it into `versions/2.1`:
    - `git checkout -b versions/2.1 gitbook`
    - `git push origin versions/2.1`
- Notify @desplesda to make this branch available as a variant in GitBook
- Merge `staging` into `gitbook`:
  - `git checkout gitbook`
  - `git merge staging`
  - `git push origin gitbook`
- Wait a couple of minutes, and then check that the docs on https://docs.yarnspinner.dev are up-to-date

# Post-Release

- Post the release tweets that were prepared earlier.
- Post the version announcement in Discord.
- Blow your party horn 🥳
