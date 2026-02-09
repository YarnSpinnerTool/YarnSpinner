#!/bin/bash

# Compiles, aliases, and copies the Yarn Spinner DLLs into a Yarn Spinner for
# Unity project
#
# Usage: ./sync-yarnspinner-dlls.sh {PATH TO YARNSPINNER-UNITY PROJECT}

set -e

YARNSPINNER_FOLDER=$(readlink -f "$(dirname $0)")
YARNSPINNER_DLLS_DIR=$1/Runtime/DLLs/

pushd $1

if [ ! -d $YARNSPINNER_DLLS_DIR ]; then
    echo "Can't copy Yarn Spinner DLLS to $YARNSPINNER_DLLS_DIR because this directory does not exist"
    exit 1
fi

if [ -d .build-tmp ]; then 
    echo "Can't build Yarn Spinner DLLs to .build-tmp because this directory already exists, and I don't want to overwrite it."
    exit 1
fi

cd $YARNSPINNER_FOLDER
dotnet-gitversion /updateAssemblyInfo
mkdir -p .build-tmp
dotnet build -p:TargetFrameworks=netstandard2.1 --configuration Debug -o .build-tmp YarnSpinner.Compiler

# some types we are going to want to use externally but the rest should be fully internal to Yarn Spinner itself
assemblyalias --target-directory ".build-tmp" --prefix "Yarn." --assemblies-to-alias "Antlr*;Csv*;Google*;"
assemblyalias --target-directory ".build-tmp" --internalize --prefix "Yarn." --assemblies-to-alias "System*;Microsoft.Bcl*;Microsoft.Extensions*"

cp -v .build-tmp/*.dll $YARNSPINNER_DLLS_DIR
cp -v .build-tmp/*.pdb $YARNSPINNER_DLLS_DIR || true
cp -v .build-tmp/*.xml $YARNSPINNER_DLLS_DIR || true
rm -fv $YARNSPINNER_DLLS_DIR/Microsoft.CSharp.dll

rm -rf .build-tmp

git checkout  "*/AssemblyInfo.cs"

echo "Synced current working directory of Yarn Spinner from $YARNSPINNER_FOLDER"

popd
