#!/bin/bash

# Compiles, aliases, and copies the Yarn Spinner DLLs into a Yarn Spinner for
# Unity project
#
# Usage: ./sync-yarnspinner-dlls.sh {PATH TO YARNSPINNER-UNITY PROJECT}
# Run this script in the root of the Yarn Spinner Core directory.

set -e 

YARNSPINNER_UNITY_FOLDER=$1

YARNSPINNER_FOLDER=.

YARNSPINNER_DLLS_DIR=$1/Packages/dev.yarnspinner.unity/Runtime/DLLs/

if [ ! -d $YARNSPINNER_DLLS_DIR ]; then
    echo "Can't copy Yarn Spinner DLLS to $YARNSPINNER_DLLS_DIR because this directory does not exist"
    exit 1
fi

if [ -d .build-tmp ]; then 
    echo "Can't build Yarn Spinner DLLs to .build-tmp because this directory already exists, and I don't want to overwrite it."
    exit 1
fi

cd $YARNSPINNER_FOLDER
mkdir -p .build-tmp
dotnet build  --configuration Debug -o .build-tmp YarnSpinner.Compiler

# some types we are going to want to use externally but the rest should be fully internal to Yarn Spinner itself
assemblyalias --target-directory ".build-tmp" --prefix "Yarn." --assemblies-to-alias "Antlr*;Csv*;Google*;"
assemblyalias --target-directory ".build-tmp" --internalize --prefix "Yarn." --assemblies-to-alias "System*;Microsoft.Bcl*;Microsoft.Extensions*"

cp -v .build-tmp/*.dll $YARNSPINNER_DLLS_DIR
cp -v .build-tmp/*.pdb $YARNSPINNER_DLLS_DIR
cp -v .build-tmp/*.xml $YARNSPINNER_DLLS_DIR
rm -fv $YARNSPINNER_DLLS_DIR/Microsoft.CSharp.dll

rm -rf .build-tmp

echo "Synced current working directory of Yarn Spinner from $YARNSPINNER_FOLDER"
