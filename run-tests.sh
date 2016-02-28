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
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

returncode=0

TESTPATH="Tests"
ONLY_VERIFY=0

while  getopts ":d:V" opt; do
	case $opt  in
		d) TESTPATH="$OPTARG" ;;
		V) ONLY_VERIFY=1 ;;
		\?) echo "Unknown option -$OPTARG"; exit 1 ;;
	esac
done

if [ $ONLY_VERIFY == 1 ]; then
	echo "Performing a verify-only run."
fi

IFS=$'\n';for f in $(find $TESTPATH -name "*.node" -or -name "*.json"); do
	echo "Testing $f"
	
	if [ $ONLY_VERIFY == 1 ]; then
		./parser.sh -V "$f" 
	else
		./parser.sh "$f" 
	fi
		
	
	if [[ $? -ne 0 ]]; then
		echo "*** ERROR RUNNING $f ***"
		returncode=1
	fi
done

exit $returncode