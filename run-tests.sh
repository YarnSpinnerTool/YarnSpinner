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
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

returncode=0

TESTPATH="."
ONLY_VERIFY=0
INTERACTIVE=0

function show_help {
    echo "run-tests.sh: Run all Yarn scripts in a directory"
    echo
    echo "Usage: run-tests.sh [-d <path>] [-Vih]"
    echo "  -d <path>: Run the tests in <path> (defaults to '.')"
    echo "  -i: Run scripts interactively; when presented with options, wait for input."
    echo "  -V: Verify scripts only; do not run. (Ignores -i.)"
    echo "  -h: Show this text and exit"
}


while  getopts ":d:Vhi" opt; do
    case $opt  in
        d) TESTPATH="$OPTARG" ;;
        i) INTERACTIVE=1 ;;
        V) ONLY_VERIFY=1 ;;
        h) show_help ; exit 1; ;;
        \?) echo "Unknown option -$OPTARG"; exit 1 ;;
    esac
done

if [ $ONLY_VERIFY == 1 ]; then
    echo "Performing a verify-only run."
fi

IFS=$'\n';for f in $(find "$TESTPATH" -name "*.node" -or -name "*.json"); do
    echo "Testing $f"

    if [ $ONLY_VERIFY == 1 ]; then
        ./yarn verify "$f"
    else
        if [ $INTERACTIVE == 1 ]; then
            ./yarn run "$f"
        else
            ./yarn run --select-first-choice "$f"
        fi
    fi

    if [[ $? -ne 0 ]]; then
        echo "*** ERROR RUNNING $f ***"
        returncode=1
    fi
done

echo

if [ $returncode == 0 ]; then
    echo "All tests completed successfully."
else
    echo "Tests failed!"
fi

exit $returncode
