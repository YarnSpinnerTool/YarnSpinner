#!/bin/bash

# Compiles, aliases, and copies the Yarn Spinner DLLs into a Yarn Spinner for
# Unity project
#
# Usage: ./sync-yarnspinner-dlls.sh {PATH TO YARNSPINNER-UNITY PROJECT}

set -e

YARNSPINNER_FOLDER=$(readlink -f "$(dirname $0)")
YARNSPINNER_DLLS_DIR=$1/Packages/dev.yarnspinner.unity/Runtime/DLLs/

CONFIGURATION=Release

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
dotnet clean --configuration $CONFIGURATION
dotnet build -p:UseVendoredProtobuf=true --configuration $CONFIGURATION YarnSpinner.Compiler
cp -v YarnSpinner.Compiler/bin/$CONFIGURATION/netstandard2.1/* .build-tmp

# cp /Users/desplesda/Work/protobuf/csharp/src/Google.Protobuf/bin/Debug/netstandard2.1/Google.Protobuf.dll .build-tmp

# some types we are going to want to use externally but the rest should be fully internal to Yarn Spinner itself
assemblyalias --target-directory ".build-tmp" --prefix "Yarn." --assemblies-to-alias "Antlr*;Csv*;Google*"
assemblyalias --target-directory ".build-tmp" --internalize --prefix "Yarn." --assemblies-to-alias "Microsoft.Extensions*;System.Text.Json;System.Text.Encodings.Web;System.Runtime.CompilerServices.Unsafe;Microsoft.Bcl.AsyncInterfaces;System.Runtime.CompilerServices.Unsafe"

cp -v .build-tmp/Yarn*.dll $YARNSPINNER_DLLS_DIR
cp -v .build-tmp/Yarn*.pdb $YARNSPINNER_DLLS_DIR || true
cp -v .build-tmp/Yarn*.xml $YARNSPINNER_DLLS_DIR || true
rm -fv $YARNSPINNER_DLLS_DIR/Microsoft.CSharp.dll

rm -rf .build-tmp

git checkout  "*/AssemblyInfo.cs"

echo "Synced current working directory of Yarn Spinner from $YARNSPINNER_FOLDER"

popd
