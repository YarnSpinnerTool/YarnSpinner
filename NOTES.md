* Nodes are chunks of dialogue.

* Nodes have names, which are strings.

TODO: Discuss blocks, which are indented groups of lines - nodes have by definition one block, and blocks are also used in shortcut options (they have an attached block that contains their lines)

* Comments are ignored by the parser.
	* Comments may be single line ("//" up to end of line) or multi-line ("/*" up to "*/").

* Nodes contain lines.
	* Lines are NOT the same as lines in fext files (i.e. runs of text that end in '\n')
	
	
	* Lines may be one of two types: Dialogue, and Commands.
		* Dialogue looks like this:
			"Character: Hey, look at my nice exposition"		
		* Commands look like this:
			<<set $foo to 1>>
			* Commands are always wrapped in << >>.
				
* Lines are evaluated one after another at runtime.
* Different types of lines are evaluated in different ways.
	* Dialogue is evaluated by giving it to the output system
		* The dialogue runner waits for the output system to signal that it's done presenting the line.
		* This can mean waiting for the text to finish displaying, or for maybe the recorded dialogue line to finish playing, or something.
	* Commands are evaluated immediately, unlike dialogue.

* Commands have 2 valid types:
	* <<set EXPRESSION>>
	* <<if EXPRESSION>> 
		(lines) 
	<<elseif EXPRESSION>> 
		(lines)
	<<else>>
		(lines)
	<<endif>>
* Any unrecognised commands are forwarded to the output system, for its own processing. For example, "<<tell Mae to dance>>"

* Available expressions are:

	Only available in "if" commands:
	=, ==, eq, is: Equal to
	>, gt: Greater than
	>=, gte: Greater than or equal to
	<, lt: Less than
	<=, lte: Less than or equal to
	!=, neq: Not equal
	
	Only available in "set" commands:
	a += b: Add A and B and assign value to A
	a -= b: Subtract B from A and assign value to A
	a *= b: Multiply A by B and assign value to A
	a /= b: Divide A by B and assign value to A
	a = b: Assign B to A
	a to b: Assign B to A

	* Note that '=' only means 'assign value' if evaluated within in a 'set' command. At all other times, it means 'check is equal to'.
	
* 'if' commands contain other lines (ie dialogue or other commands). These lines are only evaluated if the expression evaluates to true.
		
* Variable names begin with $ and must have at least 2 characters (including the '$').
* Variable names may contain any character except " ".

* Nodes contain options.
	* Options look like this: [[Text to present|NodeName]]
	* Or like this: [[NodeName]]
	* Options may appear anywhere in the node. When they are encountered while the node is running, they are added to a list of possible options, and presented when the node has finished running.
	* Options are not presented if there is only a single option; instead, flow moves to that option automatically.
	* When the user selects an option, the node attached to it is run.
	* Options are added to the list at run time, not at parse time. This means that this is allowed:
	
		[[Neat|DoAThing]]
		<<if $variable is 0>>
			[[Oh cool|DoASecondThing]]
		<<endif>>
			
		In this example, the second option will appear only when $variable == 0.
			
	* When run, if $variable is not equal to 0, only a single option will be presented.

* Shortcut options create temporary nodes.
	* Temporary nodes look like this:

		Mae: Testing out a new options system.
		-> This is option 1
		    Mae: Then my response dialogue would go here. 
		-> This is option 2
		    Mae: Then my response for option two would go here.
		Mae: And we rejoin the main node here.
		
	* Shortcut options split the current node at the end of the list of options. The second node has the same name as the main node, with ".Epilogue" appended.
	* For each option, a temporary node is created, and an option is added to the Prologue that points towards it.
	* The content of the temporary node is any line that is more indented than the line that had the shortcut option.
	* The temporary option has a single blank option, which is linked to the Epilogue.
	
	* The example dialogue would be parsed like this:

		(NODE1)
		Mae: Testing out a new options system.
		[[This is option 1|NODE1.Option1]]
		[[This is option 2|NODE1.Option2]]
		
		(NODE1.Option1)
		Mae: Then my response dialogue would go here. 
		[[NODE1.Epilogue]]
		
		(NODE1.Option2)
		Mae: Then my response for option two would go here.
		[[NODE1.Epilogue]]
		
		(NODE1.Epilogue)
		Mae: And we rejoin the main node here.
		
	* Shortcut options may have a conditional. Conditionals look like this:
	
		-> This middle option should not appear. <<if $option_available is 1>>
	        Molly: It sure didn't.
		
		* This has the same effect as this:
		
			<<if $option_available is 1>>
				[[This middle option should not appear.|NODE1.Option1]]
			<<endif>>
		
	
	* This syntax may be nested. The contents of a shortcut option's text may be itself split.
	
		Mae: Testing out a new options system.
		-> This is option 1
		    Mae: Then my response dialogue would go here. 
		    Mae: Pretty cool huh?
		    -> You can even nest options
		        Molly: Oh, rad.
		    -> Yeah, it's great.
		        Molly: I love it.
		-> This is option 2
			Mae: Nice.
		Mae: Anyway, that was cool.
		
	* This dialog would be result in these nodes:
	
		(NODE1)
		Mae: Testing out a new options system.
		[[This is option 1|NODE1.Option1]]
		[[This is option 2|NODE1.Option2]]
		
		(NODE1.Option1)
	    Mae: Then my response dialogue would go here. 
	    Mae: Pretty cool huh?		
		[[You can even nest options|NODE1.Option1.Option1]]
		[[Yeah, it's great.|NODE1.Option1.Option2]]
		
		(NODE1.Option1.Option1)
		Molly: Oh, rad.
		[[NODE1.Option1.Epilogue]]
		
		(NODE1.Option1.Option2)
		Molly: I love it.
		[[NODE1.Option1.Epilogue]]
		
		(NODE1.Option1.Epilogue) <--- Notice how this does nothing - it just forwards us to Node1.Epilogue; maybe it could be optimised out??
		[[NODE1.Epilogue]]
		
		(NODE1.Option2)
		Mae: Nice.
		[[NODE1.Epilogue]]
		
		(NODE1.Epilogue)
		Mae: Anyway, that was cool.

* TODO: Probably add the text of an option to the start of nodes, might help with debugging, eg

	(NODE1.Option1.Option1)
	// "You can even nest options"
	Molly: Oh, rad.
	[[NODE1.Option1.Epilogue]]
		

