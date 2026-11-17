#!/bin/bash

# Compiles, aliases, and copies the Yarn Spinner DLLs into a Yarn Spinner for
# Unity project
#
# Usage: ./sync-yarnspinner-dlls.sh {PATH TO YARNSPINNER-UNITY PROJECT} --release

set -e

YARNSPINNER_FOLDER=$(readlink -f "$(dirname $0)")
YARNSPINNER_DLLS_DIR=$1/Runtime/DLLs/
YARNSPINNER_SOURCE_GENERATOR_DIR=$1/SourceGenerator/

pushd $1

if [ ! -d $YARNSPINNER_DLLS_DIR ]; then
    echo "Can't copy Yarn Spinner DLLS to $YARNSPINNER_DLLS_DIR because this directory does not exist"
    exit 1
fi

if [ -d .build-tmp ]; then 
    echo "Can't build Yarn Spinner DLLs to .build-tmp because this directory already exists, and I don't want to overwrite it."
    exit 1
fi

# if the "--release" flag is set then we want to do our build as a release build instead of a debug build
# if this isn't set or is set to something other than "--release" we do a debug build
# assumption here is that most of the time this script is used it's during development so having to specify that each time is bad
# because this tool is pretty jank the order does matter, this has to go after the path to the unity project
if [ -z "$2" ]; then
    CONFIGURATION=Debug
elif [ $2 == "--release" ]; then
    CONFIGURATION=Release
else
    CONFIGURATION=Debug
fi

cd $YARNSPINNER_FOLDER
dotnet-gitversion /updateAssemblyInfo
mkdir -p .build-tmp
dotnet clean --configuration $CONFIGURATION
dotnet build --configuration $CONFIGURATION YarnSpinner.Compiler
cp -v YarnSpinner.Compiler/bin/$CONFIGURATION/netstandard2.1/* .build-tmp

cp -v .build-tmp/*.dll $YARNSPINNER_DLLS_DIR
cp -v .build-tmp/*.pdb $YARNSPINNER_DLLS_DIR || true
cp -v .build-tmp/*.xml $YARNSPINNER_DLLS_DIR || true

# we never need Microsoft.CSharp so can delete it
rm -fv $YARNSPINNER_DLLS_DIR/Microsoft.CSharp.dll
# because of Unity changes we also don't need any of these
rm -fv $YARNSPINNER_DLLS_DIR/System.Buffers.dll
rm -fv $YARNSPINNER_DLLS_DIR/System.Memory.dll
rm -fv $YARNSPINNER_DLLS_DIR/System.Numerics.Vectors.dll
rm -fv $YARNSPINNER_DLLS_DIR/System.Reflection.TypeExtensions.dll
# rm -fv $YARNSPINNER_DLLS_DIR/System.Runtime.CompilerServices.Unsafe.dll # required for Unity 2022.3
rm -fv $YARNSPINNER_DLLS_DIR/System.Threading.Tasks.Extensions.dll

# Build the source generator and copy it into place
pushd YarnSpinner.SourceGenerator
dotnet clean --configuration $CONFIGURATION
dotnet build --configuration $CONFIGURATION
ilrepack --out=YarnSpinner.Unity.SourceCodeGenerator.dll bin/$CONFIGURATION/netstandard2.0/Analyser.dll bin/$CONFIGURATION/netstandard2.0/YarnSpinner.HostAnalysis.dll bin/$CONFIGURATION/netstandard2.0/YarnSpinner.Shared.dll --lib=bin/$CONFIGURATION/netstandard2.0/
popd
cp YarnSpinner.SourceGenerator/YarnSpinner.Unity.SourceCodeGenerator.{dll,pdb} $YARNSPINNER_SOURCE_GENERATOR_DIR

# Clean up
rm -rf .build-tmp
git checkout  "*/AssemblyInfo.cs"

echo "Synced current working directory of Yarn Spinner from $YARNSPINNER_FOLDER"

popd
