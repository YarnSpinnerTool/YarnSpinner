#!/usr/bin/env bash

# The MIT License (MIT)
# 
# Copyright (c) 2015 Secret Lab Pty. Ltd. and Yarn Spinner contributors.
# 
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
# 
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
# 
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

LOGFILE="export.log"
OUTDIR="`pwd`/Builds"
SOURCE_FILES="YarnSpinner/*.cs"

NO_EXAMPLES=0

SOURCE_BUILD=0

set -e

function show_help {
	echo "package.sh: Package Yarn Spinner into a .unitypackage for distribution"
	echo
	echo "Usage: package.sh [-hsx]"
	echo "  -s: Package the source code, not a built DLL"
	echo "  -c: Show the changes since the last tag and exit"
	echo "  -h: Show this text and exit"
	echo "  -x: Do not include example project assets"
}

function show_changes {
	LATEST_TAG=`git describe --abbrev=0 --tags`
	COMMITS_SINCE_THEN=`git rev-list $LATEST_TAG.. --count`

	if [ $COMMITS_SINCE_THEN -eq "0" ]; then
		echo "There has been no further work since the last tag."
	else
		echo "There have been $COMMITS_SINCE_THEN commits since $LATEST_TAG:"

		git log $LATEST_TAG.. --pretty=format:"* %s"
	fi
}


while getopts ":xhsc" opt; do
	
	case $opt in
	   x) NO_EXAMPLES=1 ;;
	   h) show_help ; exit 0 ;;
	   s) SOURCE_BUILD=1 ;;
	   c) show_changes ; exit 0 ;;
	   \?) echo "Invalid option: -$OPTARG" >&2; echo; show_help ; exit 0 ;;
	esac

done

if [ `uname -s` != "Darwin" ]; then
	echo "This script only works on OS X."
	exit 1
fi

# Clean the Unity directory of anything ignored
echo "Cleaning Unity project..."
git clean -f -d -X Unity

# Build the Yarn Spinner DLL
echo "Building Yarn Spinner..."
./build.sh

if [ $SOURCE_BUILD == 1 ]; then
	echo "Removing YarnSpinner.dll..."
	# Remove the built DLL from the Unity project (we built it to ensure that it actually works)
	rm -v "Unity/Assets/Yarn Spinner/Code/YarnSpinner.dll"
	
	echo "Copying Yarn Spinner source in..."
	# Copy the source files in
	cp -v $SOURCE_FILES "Unity/Assets/Yarn Spinner/Code/"
	
fi

# Next, determine what build name this is.

# Strips "v" from tag names
function strip-v () { echo -n "${1#v}"; }

FULL_VERSION=$(strip-v $(git describe --tags --match 'v[0-9]*' --always --dirty))

if [ $SOURCE_BUILD == 1 ]; then
	FULL_VERSION="$FULL_VERSION-source"
fi

if [ $NO_EXAMPLES == 1 ]; then
	FULL_VERSION="$FULL_VERSION-minimal"
fi

echo "Packaging Version $FULL_VERSION with Unity..."

OUTFILE="$OUTDIR/YarnSpinner-$FULL_VERSION.unitypackage"

# Find where Unity is installed; sort it to put the latest 
# version name at the end, then pick the last one
UNITY="`mdfind "kind:application Unity.app" | sort | tail -n 1`/Contents/MacOS/Unity"

if [[ -f $UNITY ]]; then
	echo "Using $UNITY"
	
	if [ $NO_EXAMPLES -eq 1 ]; then
		ASSET_PATH="Assets/Yarn Spinner/Code"
	else
		ASSET_PATH="Assets/Yarn Spinner"
	fi
	
	# Disable stop-on-error for this - we want better reporting
	set +e
	
	"$UNITY" -batchmode -projectPath "`pwd`/Unity" -logFile $LOGFILE -exportPackage "$ASSET_PATH" "$OUTFILE" -quit
	
	if [ $? -ne 0 ]; then
		
		echo "Error: Unity failed to build the package (see $LOGFILE)"
		exit 1
	fi
	
	if [ -f $OUTFILE ]; then
		echo "Package created in $OUTFILE"		
	else
		echo "Error: Unity reported no error, but the package wasn't created. Check $LOGFILE."
		exit 1
	fi
	
	# Turn stop-on-error back on
	set -e
	
	# Tidy up any untracked files (including source files, if this was a source build)
	git clean -f Unity
	
else
	echo "Error: Unity not found"
	exit 1
fi

echo

echo "Build complete!"

show_changes


