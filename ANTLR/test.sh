#!/usr/bin/env bash

# stop if any of these causes an error
set -e

# required for the java compilation to work
export CLASSPATH=.:/usr/local/Cellar/antlr/4.7/antlr-4.7-complete.jar

# poor terminal
clear
# quitting preview
osascript -e 'tell application "Preview" to close (every window whose name is "Test.yarn.txt.pdf")'

# generate the grammar
antlr4 *.g4

# compile the source code
javac *.java

# run the result and generate a parse tree into a PostScript file
grun YarnSpinner dialogue -tokens Test.yarn.txt -ps Test.yarn.txt.ps

# open the ps
open -a "Preview" Test.yarn.txt.ps

# clear up the junk we don't need
rm *.class
rm *.java
rm *.tokens
