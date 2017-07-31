#!/usr/bin/env bash
# The MIT License (MIT)
#
# Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.
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

set -e

show_help () {
    echo "build.sh: Build Yarn Spinner"
    echo
    echo "Usage: build.sh [bcdnuvh]"
    echo " -b: Build Yarn Spinner"
    echo " -c: Clean any pre-existing builds and documentation"
    echo " -d: Build documentation"
    echo " -n: Build (OSX) native **ONLY WORKS ON OSX**"
    echo " -u: Execute unit tests"
    echo " -v: verbose output"
    echo " -h: Show this text and exit"
}

init_build () {
    CONFIGURATION="Release"
    VERBOSITY="quiet"
    while getopts "bcdnuvh" opt; do
        case $opt in
            b)
                BUILD=true ;;
            c)
                CLEAN=true ;;
            d)
                DOCUMENTATION=true ;;
            n)
                NATIVE=true ;;
            u)
                UNIT=true ;;
            v)
                VERBOSITY=normal ;;
            h)
                HELP=true ;;
            \?) echo; show_help ; exit 0 ;;
        esac
    XBUILD_ARGS="/verbosity:${VERBOSITY} /p:Configuration=${CONFIGURATION}"
    if [ "${OSTYPE}" = "linux-gnu" ]; then
        FAILED_PREREQ=""
        if [ ! $(which xbuild) ]; then
            echo "xbuild not installed or not in \$PATH"
            FAILED_PREREQ="true"
        fi
        if [ ! $(which nuget) ]; then
            echo "nuget not installed or not in \$PATH"
            FAILED_PREREQ="true"
        fi
        if [ -n "${FAILED_PREREQ}" ]; then
            echo "Failed pre-requisite checks, aborting"
            exit 1
        fi
        XBUILD_ARGS="${XBUILD_ARGS} /p:TargetFrameworkVersion=v4.5"
    fi
done
}

clean_yarnspinner () {
    if [ -f YarnSpinner/bin/${CONFIGURATION}/YarnSpinner.dll ]; then
        xbuild ${XBUILD_ARGS} /target:clean YarnSpinner.sln
    fi
    echo "Cleaning documentation"
    rm -fvr Documentation/{docbook,html,latex,rtf,xml}
    rm -fvr GPATH GRTAGS GTAGS doxygen
}


build_yarnspinner () {
    nuget restore YarnSpinner.sln
    xbuild ${XBUILD_ARGS} YarnSpinner.sln

    if [ $? -ne 0 ]; then
        echo "Error during: xbuild ${XBUILD_ARGS}"
        exit 1
    fi

    # this is an appalling test for not windows or osx and with unity
    if [ "${OSTYPE}" != "linux-gnu" ]; then
        OUTPUT_DLL="YarnSpinner.dll"
        BUILD_DIR="YarnSpinner/bin/${CONFIGURATION}/"
        UNITY_DIR="Unity/Assets/YarnSpinner/Code/"

        if [ -f "$BUILD_DIR/$OUTPUT_DLL" ]; then
            cp -v "$BUILD_DIR/$OUTPUT_DLL" "$UNITY_DIR/$OUTPUT_DLL"
        else
            echo "Install for Unity failed."
            exit 1
        fi
    fi
}

build_native () {
    if [ "$(uname -s)" != "Darwin" ]; then
        echo "Building native only works on OSX."
        exit 1
    fi
    # Build the product
    build_yarnspinner

    # Ensure it can find pkg-config:
    export PKG_CONFIG_PATH=$PKG_CONFIG_PATH:/usr/lib/pkgconfig:/Library/Frameworks/Mono.framework/Versions/3.4.0/lib/pkgconfig

    # Manually set some clang linker properties:
    export AS="as "
    export CC="cc -lobjc -liconv -framework Foundation"

    # Build:
    mkbundle YarnSpinnerConsole/bin/${CONFIGURATION}/YarnSpinnerConsole.exe  YarnSpinnerConsole/bin/${CONFIGURATION}/*.dll --static --deps -o yarn_native
}

unit_tests () (
    if [ -r ./YarnSpinnerTests/bin/${CONFIGURATION}/YarnSpinnerTests.dll ]; then
        if [ ! $(which nuget) ]; then
            echo "nuget not installed or not in \$PATH"
            exit 1
        fi
        nuget restore YarnSpinner.sln
        nuget install NUnit.ConsoleRunner -Version 3.6.1 -OutputDirectory testrunner
        mono ./testrunner/NUnit.ConsoleRunner.3.6.1/tools/nunit3-console.exe ./YarnSpinnerTests/bin/Release/YarnSpinnerTests.dll
    else
        echo "Failed to find unit tests; exiting"; exit 1
    fi
)

build_documentation () {
    # Quick statement to build documents
    FAILED_PREREQ=""
    if [ ! "$(which doxygen)" ]; then
        echo "doxygen not found or not in \${PATH}"
        FAILED_PREREQ="true"
    fi
    if [ ! "$(which dot)" ]; then
        echo "dot not found or not in \${PATH}"
        FAILED_PREREQ="true"
    fi
    if [ -n "${FAILED_PREREQ}" ]; then
        echo "Failed pre-requisite checks, aborting"
        exit 1
    fi
    if [ "$CLEAN" ]; then
        echo "Cleaning documentation"
        rm -fvr Documentation/{docbook,html,latex,rtf,xml}
        rm -fvr GPATH GRTAGS GTAGS doxygen
    fi
    doxygen Documentation/Doxyfile
}

# Pass all command line options to inuit_build
init_build "$@"

if [ "$HELP" ]; then
    show_help
    exit 1
fi
if [ "$CLEAN" ]; then
    echo "Cleaning Yarn Spinner"
    clean_yarnspinner
fi
if [ "$NATIVE" ]; then
    echo "Building native Yarn Spinner"
    build_native
fi
if [ "$BUILD" ]; then
    echo "Building Yarn Spinner"
    build_yarnspinner
fi
if [ "$UNIT" ]; then
    echo "Running Yarn Spinner unit tests"
    unit_tests
fi
if [ "$DOCUMENTATION" ]; then
    echo "Building documentation"
    build_documentation
fi
