#!/usr/bin/env bash


TESTCASES_DIR="Tests/TestCases"

for f in $TESTCASES_DIR/*.node; do
	echo "Testing $f"
	./parser.sh $f
done