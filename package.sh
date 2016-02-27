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

if [ `uname -s` != "Darwin" ]; then
	echo "This script only works on OS X."
	exit 1
fi

# Build the project.
./build.sh

# Determine what build name this is.
set -e

function strip-v () { echo -n "${1#v}"; }

FULL_VERSION=$(strip-v $(git describe --tags --match 'v[0-9]*' --always --dirty))

echo "Packaging Version $FULL_VERSION"

OUTFILE="$OUTDIR/YarnSpinner-$FULL_VERSION.unitypackage"

# Find where Unity is installed; sort it to put the latest 
# version name at the end, then pick the last one
UNITY="`mdfind "kind:application Unity.app" | sort | tail -n 1`/Contents/MacOS/Unity"

if [[ -f $UNITY ]]; then
	echo "Using $UNITY"
	"$UNITY" -batchmode -projectPath "`pwd`/Unity" -logFile $LOGFILE -exportPackage "Assets/Yarn Spinner" "$OUTFILE" -quit
	
	if [ $? -ne 0 ]; then
		echo "Error: Unity failed to build the package (see $LOGFILE)"
		exit 1
	fi
	
	if [ -f $OUTFILE ]; then
		echo "Package created in $OUTFILE"		
	fi
else
	echo "Error: Unity not found"
	exit 1
fi

echo
echo "Since the last tag:"

git log v0.9.5..master --pretty=format:"* %s"