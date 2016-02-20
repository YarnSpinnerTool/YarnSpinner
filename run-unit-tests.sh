#!/usr/bin/env bash

xbuild /verbosity:quiet /p:Configuration=Release YarnSpinner.sln
mono ./YarnSpinnerTests/bin/Release/YarnSpinnerTests.exe
