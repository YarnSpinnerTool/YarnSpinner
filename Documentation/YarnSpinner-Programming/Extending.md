# Extending Yarn Spinner

## Style Guide

`Inline Code` contain short snippets of code for your project

    Code Blocks contain segments or chunks of code for your project

**Bold indicate actions (Select menu item, copying file, etc.)**

***Bold italic text indicates emphasis***

> Blockquotes contain essential information

## Introduction

This document intends to demonstrate to a developer how they can add new functions so that they can be called from inside Yarn.

### Programming style
* It is recommended that you follow our programming style so that in the case of bug or patch submission, it makes it easier for us to read. As such, indents should be four spaces and not tab stops. 
* We use the Doxygen format for documentation, thus three forward slashes `///` indicate the head of a documentation comment and details for documentation comments are contained within the `/* .... */` structure. Please refer to the Doxygen site for more information. 
* Other comments should use two forward slashes. This will ensure proper code commentary without it appearing in generated API documentation.


## Creating a custom command
Creating a custom command is a simple two step procedure.i

1. We need to get the dialogue object that's in use, then regiser a new function for it.
```
    // get the Dialogue object 
	// varstorage_implementation is the object that handles variable storage - it's not important in this example
	var dialogue = new Dialogue(varstorage_implementation);
```
2. Next, register your new function. For example, let's make a function that takes 1 parameter, which is a number, and returns `true` if it's even:
```
    // RegisterFunction(name, parameterCount, implementation)
	dialogue.library.RegisterFunction ("is_even", 1, delegate(Value[] parameters) {
		return (int)parameters[0].AsNumber % 2 == 0;
	});
```	
When the function is called, the delegate you provide will be run.

Some notes:

* You don't have to return a value from your function.
* The parameters passed to your function are of type Yarn.Value. You can get their value as numbers, bools or strings by using the `AsNumber`, `AsString` and `AsBool` properties.
* You can only return values of the following types:
	* String
	* Float
	* Double
	* Integer
	* Yarn.Value

* Yarn Spinner will make sure that the correct number of parameters is passed to your method. If you specify `-1` as your parameter count, the function may have any number of parameters.
