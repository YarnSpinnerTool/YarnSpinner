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

NO_EXAMPLES=0

set -e

function show_help {
	echo "package.sh: Package Yarn Spinner into a .unitypackage for distribution"
	echo
	echo "Usage: package.sh [-hx]"
	echo "	-h: Show this text and exit"
	echo "	-x: Do not include example project assets"
	exit 0
}

while getopts ":xh" opt; do
	
	case $opt in
	   x) NO_EXAMPLES=1 ;;
	   h) show_help ;;
	   \?) echo "Invalid option: -$OPTARG" >&2; echo; show_help ;;
	esac

done


echo "NO_EXAMPLES is $NO_EXAMPLES"

if [ `uname -s` != "Darwin" ]; then
	echo "This script only works on OS X."
	exit 1
fi

# Clean the Unity directory of anything ignored
git clean -f -d -X Unity

# Build the Yarn Spinner DLL
./build.sh

# Next, determine what build name this is.

# Strips "v" from tag names
function strip-v () { echo -n "${1#v}"; }

FULL_VERSION=$(strip-v $(git describe --tags --match 'v[0-9]*' --always --dirty))

echo "Packaging Version $FULL_VERSION"

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
	fi
	
	set -e
else
	echo "Error: Unity not found"
	exit 1
fi


LATEST_TAG=`git describe --abbrev=0 --tags`
COMMITS_SINCE_THEN=`git rev-list $LATEST_TAG.. --count`

if [ $COMMITS_SINCE_THEN -eq "0" ]; then
	echo "There has been no further work since the last tag."
else
	echo
	echo "There have been $COMMITS_SINCE_THEN commits since $LATEST_TAG:"

	git log $LATEST_TAG.. --pretty=format:"* %s"
fi

