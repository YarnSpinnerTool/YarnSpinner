#!/usr/bin/env bash


TESTCASES_DIR="Tests/TestCases"

returncode=0

for f in $TESTCASES_DIR/*.node; do
	echo "Testing $f"
	./parser.sh $f
	
	if [[ $? -ne 0 ]]; then
		echo "*** ERROR ***"
		returncode=1
	fi
done

exit $returncode