#!/usr/bin/env bash

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

