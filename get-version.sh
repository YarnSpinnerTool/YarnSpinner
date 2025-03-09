#!/bin/bash

# Get info about the current commit
most_recent_tag=$(git describe --tags --match="v*" --abbrev=0)
commits_since_tag=$(git rev-list $most_recent_tag..HEAD | wc -l | awk '{$1=$1};1')
sha=$(git log -1 --format=%H)
short_sha=$(git log -1 --format=%h)
branch=$(git rev-parse --abbrev-ref HEAD)

# A regex for extracting data from a version number: major, minor, patch,
# [prerelease]
REGEX='v(\d+)\.(\d+)\.(\d+)(-.*)?'

raw_version=${1:-"$most_recent_tag"}

# Extract the data from the version number
major=$(echo $raw_version | perl -pe "s|$REGEX|\1|" )
minor=$(echo $raw_version | perl -pe "s|$REGEX|\2|" )
patch=$(echo $raw_version | perl -pe "s|$REGEX|\3|" )
prerelease=$(echo $raw_version | perl -pe "s|$REGEX|\4|" )

# Calculate the semver from the version (should be the same as the version, but
# just in case)
SemVer="$major.$minor.$patch$prerelease"

# If there are any commits since the current tag and we aren't overriding our
# version, add that note
if [ "$commits_since_tag" -gt 0 -a -z "$1"  ]; then
    SemVer="$SemVer+$commits_since_tag"
fi

# Create the version strings we'll write into the AssemblyInfo files
OutputAssemblyVersion=$(echo "$major.$minor.$patch.$commits_since_tag" | perl -pe "s|\/|\\\/|" )
OutputAssemblyInformationalVersion=$(echo "$SemVer.Branch.$branch.Sha.$sha" | perl -pe "s|\/|\\\/|" )
OutputAssemblyFileVersion=$(echo "$major.$minor.$patch.$commits_since_tag" | perl -pe "s|\/|\\\/|" )

# Update the AssemblyInfo.cs files
for infoFile in $(find . -name "AssemblyInfo.cs"); do
    perl -pi -e "s/AssemblyVersion\(\".*\"\)/AssemblyVersion(\"$OutputAssemblyVersion\")/" $infoFile
    perl -pi -e "s/AssemblyInformationalVersion\(\".*\"\)/AssemblyInformationalVersion(\"$OutputAssemblyInformationalVersion\")/" $infoFile
    perl -pi -e "s/AssemblyFileVersion\(\".*\"\)/AssemblyFileVersion(\"$OutputAssemblyFileVersion\")/" $infoFile
done

# If we're running in GitHub Workflows, output our calculated SemVer
if [[ -n $GITHUB_OUTPUT ]]; then
    echo "SemVer=$SemVer" >> "$GITHUB_OUTPUT"
    echo "ShortSha=$short_sha" >> "$GITHUB_OUTPUT"
fi

# Log our SemVer
echo $SemVer
