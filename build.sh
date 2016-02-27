#!/usr/bin/env bash

xbuild /verbosity:quiet /p:Configuration=Release YarnSpinner.sln

OUTPUT_DLL="YarnSpinner.dll"

BUILD_DIR="YarnSpinner/bin/Release/"
UNITY_DIR="Unity/Assets/Yarn Spinner/Code/"

if [ -f "$BUILD_DIR/$OUTPUT_DLL" ]; then
	cp "$BUILD_DIR/$OUTPUT_DLL" "$UNITY_DIR/$OUTPUT_DLL"
else
	echo "Build failed."
fi