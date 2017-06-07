#!/usr/bin/env bash

# stop if any of these causes an error
set -e

# required for the java compilation to work
export CLASSPATH=.:/usr/local/Cellar/antlr/4.7/antlr-4.7-complete.jar

# poor terminal
clear

# running the script through the preprocessor
python preprocessor.py Test.yarn.txt

# generate the grammar
antlr4 *.g4

# compile the source code
javac *.java

# run the result and run the ANTLR gui testrig
grun YarnSpinner dialogue -tokens processed.yarn -gui

# clear up the junk we don't need
rm *.class
rm *.java
rm *.tokens
rm processed.yarn
