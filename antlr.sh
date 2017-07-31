#!/usr/bin/env bash

set -e

curl http://www.antlr.org/download/antlr-4.7-complete.jar -o $HOME/antlr4.jar
export CLASSPATH=".:$HOME/antlr.jar:$CLASSPATH"
alias antlr4='java -Xmx500M -cp "$HOME/antrl.jar:$CLASSPATH" org.antlr.v4.Tool'