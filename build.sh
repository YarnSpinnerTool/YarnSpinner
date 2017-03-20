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
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN T

CONFIGURATION="Release"
VERBOSITY="quiet"
XBUILD_ARGS="/verbosity:${VERBOSITY} /p:Configuration=${CONFIGURATION}"
if [ "${OSTYPE}" = "linux-gnu" ]; then
    XBUILD_ARGS="${XBUILD_ARGS} /p:TargetFrameworkVersion=v4.5"
fi

if [ -f YarnSpinner/bin/Release/YarnSpinner.dll ]; then
    xbuild "${XBUILD_ARGS}" /target:clean YarnSpinner.sln
fi
xbuild "${XBUILD_ARGS}" YarnSpinner.sln

if [ $? -ne 0 ]; then
    echo "Error during: xbuild ${XBUILD_ARGS}"
    exit 1
fi

# this is an appalling test for not windows or osx and with unity
if [ "${OSTYPE}" != "linux-gnu" ]; then
    OUTPUT_DLL="YarnSpinner.dll"
    BUILD_DIR="YarnSpinner/bin/Release/"
    UNITY_DIR="Unity/Assets/Yarn Spinner/Code/"

    if [ -f "$BUILD_DIR/$OUTPUT_DLL" ]; then
        cp -v "$BUILD_DIR/$OUTPUT_DLL" "$UNITY_DIR/$OUTPUT_DLL"
    else
        echo "Install for Unity failed."
        exit 1
    fi
fi


# Quick statement to build documents
#if [ $(which doxygen) ]; then
#    rm -fvr Documentation/{docbook,html,latex,rtf,xml}
#    rm -fvr GPATH GRTAGS GTAGS doxygen
#    doxygen Documentation/Doxyfile
#
#fi

