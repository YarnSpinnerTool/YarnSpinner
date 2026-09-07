#!/usr/bin/env bash
# MIT License
# 
# Copyright (c) 2025 Peter Lawler <relwalretep@gmail.com>
# 
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
# 
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
# 
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.



set -euo pipefail
IFS=$'\n\t'

# Hardened get-version script:
# - Works without tags (defaults to 0.0.0+<shortsha>)
# - Handles shallow clones by attempting to fetch tags/unshallow
# - Avoids hard dependency on Perl; only uses Perl for file updates if available
# - Emits SemVer and ShortSha to GITHUB_OUTPUT when set

# Ensure git is available
if ! command -v git >/dev/null 2>&1; then
  echo "Error: git is not installed or not in PATH." >&2
  SemVer="0.0.0+unknown"
  short_sha="unknown"
  echo "$SemVer"
  exit 0
fi

# Ensure we are in a git repository
if ! git rev-parse --git-dir >/dev/null 2>&1; then
  echo "Warning: not a git repository; using default version." >&2
  SemVer="0.0.0+unknown"
  short_sha="unknown"
  echo "$SemVer"
  exit 0
fi

# Attempt to ensure tags are available (handle shallow clones gracefully)
shallow_state=$(git rev-parse --is-shallow-repository 2>/dev/null || echo "false")
if [[ "$shallow_state" == "true" ]]; then
  (git fetch --tags --unshallow >/dev/null 2>&1 || git fetch --tags --depth=1000 >/dev/null 2>&1 || true)
else
  (git fetch --tags >/dev/null 2>&1 || true)
fi

# Basic VCS info
sha=$(git rev-parse HEAD 2>/dev/null || echo "")
short_sha=$(git rev-parse --short=7 HEAD 2>/dev/null || echo "")
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "HEAD")

# Find most recent v*-style tag (if any)
most_recent_tag=""
if git describe --tags --match "v*" --abbrev=0 >/dev/null 2>&1; then
  most_recent_tag=$(git describe --tags --match "v*" --abbrev=0 2>/dev/null || echo "")
fi

# Version regex: vMAJOR.MINOR.PATCH[-prerelease]
REGEX='^v([0-9]+)\.([0-9]+)\.([0-9]+)(-.+)?$'

raw_version="${1:-$most_recent_tag}"

major="0"; minor="0"; patch="0"; prerelease=""
if [[ -n "${raw_version:-}" && "$raw_version" =~ $REGEX ]]; then
  major="${BASH_REMATCH[1]}"
  minor="${BASH_REMATCH[2]}"
  patch="${BASH_REMATCH[3]}"
  prerelease="${BASH_REMATCH[4]:-}"
fi

# Commits since tag (0 if no tag)
commits_since_tag=0
if [[ -n "$most_recent_tag" ]]; then
  if git rev-list "$most_recent_tag"..HEAD >/dev/null 2>&1; then
    commits_since_tag=$(git rev-list "$most_recent_tag"..HEAD | wc -l | awk '{$1=$1};1')
  fi
fi

SemVer="$major.$minor.$patch$prerelease"

# Append metadata
if [[ -n "$most_recent_tag" && "$commits_since_tag" -gt 0 && -z "${1:-}" ]]; then
  SemVer="$SemVer+$commits_since_tag"
elif [[ -z "$most_recent_tag" ]]; then
  id="${short_sha:-unknown}"
  SemVer="0.0.0+${id}"
fi

# Prepare assembly version strings
OutputAssemblyVersion="$major.$minor.$patch.$commits_since_tag"
OutputAssemblyInformationalVersion="$SemVer.Branch.$branch.Sha.$sha"
OutputAssemblyFileVersion="$major.$minor.$patch.$commits_since_tag"

# Update AssemblyInfo.cs files only if Perl is available
if command -v perl >/dev/null 2>&1; then
  esc_av=$(printf '%s\n' "$OutputAssemblyVersion" | perl -pe 's|/|\\/|g')
  esc_aiv=$(printf '%s\n' "$OutputAssemblyInformationalVersion" | perl -pe 's|/|\\/|g')
  esc_fv=$(printf '%s\n' "$OutputAssemblyFileVersion" | perl -pe 's|/|\\/|g')
  while IFS= read -r -d '' infoFile; do
    perl -pi -e "s/AssemblyVersion\(\".*\"\)/AssemblyVersion(\"$esc_av\")/" "$infoFile" || true
    perl -pi -e "s/AssemblyInformationalVersion\(\".*\"\)/AssemblyInformationalVersion(\"$esc_aiv\")/" "$infoFile" || true
    perl -pi -e "s/AssemblyFileVersion\(\".*\"\)/AssemblyFileVersion(\"$esc_fv\")/" "$infoFile" || true
  done < <(find . -name "AssemblyInfo.cs" -print0 2>/dev/null)
else
  echo "Warning: perl not found; skipping AssemblyInfo.cs updates." >&2
fi

# GitHub Actions outputs
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "SemVer=$SemVer"
    echo "ShortSha=${short_sha:-}"
  } >> "$GITHUB_OUTPUT"
fi

# Log SemVer
echo "$SemVer"
