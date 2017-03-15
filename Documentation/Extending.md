# Extending Yarn Spinner

This is a quick-and-dirty document that shows how to add new functions so that they can be called from inside Yarn.


First, get your `Dialogue` object:
 
	// some_implementation is the object that handles variable storage - 
	// it's not important in this example
	var dialogue = new Dialogue(some_implementation);

Next, register your new function. For example, let's make a function that takes 1 parameter, which is a number, and returns `true` if it's even:

 
	dialogue.library.RegisterFunction ("is_even", 1, delegate(Value[] parameters) {
		return (int)parameters[0].AsNumber % 2 == 0;
	});
	
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
