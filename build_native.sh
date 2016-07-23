#!/usr/bin/env bash

# Builds the Yarn Spinner command-line tool as a native binary.
# This script currently works on OS X only.

# Build the product
xbuild /verbosity:quiet /p:Configuration=Release YarnSpinner.sln

# Ensure it can find pkg-config:
export PKG_CONFIG_PATH=$PKG_CONFIG_PATH:/usr/lib/pkgconfig:/Library/Frameworks/Mono.framework/Versions/3.4.0/lib/pkgconfig

# Manually set some clang linker properties:
export AS="as "
export CC="cc -lobjc -liconv -framework Foundation"

# Build:
mkbundle YarnSpinnerConsole/bin/Release/YarnSpinnerConsole.exe  YarnSpinnerConsole/bin/Release/*.dll --static --deps -o yarn_native
