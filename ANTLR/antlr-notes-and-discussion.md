# Lets talk about the WIP ANTLR YarnSpinner parser

So this is a WIP parser/lexer for YarnSpinner written in ANTLR with that plan being that it will eventually replace the hand made one currently in use.
In its current form it is good for generating a Parse Tree of your Yarn files and that's about it!

**THIS IS IN NO WAY READY FOR USE! WHEN WE GET CLOSER TO THIS BEING READY WE WILL INTEGRATE THIS INTO THE PROJECT PROPERLY, STAY TUNED!**

For discussion on this jump into the #yarnspinner channel on the [Narrative Game Development](https://narrativegamedev.slack.com/) slack.

I thought it might be worth documenting what this WIP version does/doesn't do and why it is that way, should make moving forward a bit simpler.
Additionally because we are doing this I think it is a good time to open up discussion about the syntax design of YarnSpinner.

I am of the opinion that the syntax should be changed as it currently grew organically rather than by design.
That doesn't mean what it currently is is garbage or that is worth throwing out the design already done as there is a LOT of great stuff in there.
This is also NOT me saying how it should be done, this is just how I am doing it and ideally forms the beginning of a discussion around the future of YarnSpinner syntax and features.

## node structure

As it currently stands this can only parse the body nodes of yarn files, this is purely because that is all I have been focussing on for now as that is where the bulk of the work in YarnSpinner lies.

This however opens up the question of headers, what are allowed in headers.
What headers should be mandatory and which are optional?
Can you have custom headers?

## `=` is for assignment and assignment alone

In a language that doesn't have operator overloading, and pretty much always defines a new keyword for new functionality, we have this single case where `=` can be used for comparison and equality.
This is a peeve of mine because it complicates design and implementation for, what is in my mind, a tiny feature, so I just took that feature out.

### Reasons to keep old style

- Backwards compatibility
- Initially makes more sense than `==` (subjective)

### Reasons to change to my way

- easy enough to explain to people where to use `==` vs. `=`
- limit confusion for users if they see dialogue using `==` and `=`
- avoid arguments around "well we already have overloading on =" for future syntax
- closer to existing programming languages
- already have `is` keyword if people really don't like `==`

## The `->`  shortcut syntax

Shortcuts are now implemented using a two stage approach, where the Yarn files are run through a preprocessor to add in indents and dedents to build up the blocks for shortcuts.
Due to the nature of ANTRL and YarnSpinners rather unusual approach to whitespace this was found to be the simplest approach as it means there is no code in the grammar file itself.
There is now however an extra step and a file that will need to be ported to each language.
It was done this way as the preprocessor itself is quite straightforward compared to what the extra code in the grammar would be.

A side-effect of this is that a symbol had to be chosen to represent the indents/dedents.
I went with the `\a` or bell for indents (playing the role `{` normally does) and `\v` or vertical tab for dedent (`}` equivalent).
Both of these were chosen as they are invisible and unlikely (especially in the case of bell) to be used in existing Yarn files, additionally as control characters they don't limit the amount of available characters in text lines.
This does mean however if someone was using either of those two in their Yarn files there files won't parse correctly.

While this works this also allows for an opportunity to discuss the `->` syntax.
It currently has remarkably flexible rules around the whitespace, such that it allows structures that would break in almost any other whitespace programming language.
Is this something worth changing?

Options to change include:

- Locking down the syntax a bit more so that whitespace rules are enforced more than currently, Python offers a guideline for how this would look.
- Using the use of a `{ }` (or something similar) syntax for the `->` code blocks.

At this stage it isn't worth changing as it is currently working fine, but this is something to consider going forward.

### Reasons to keep to old style

- Backwards compatibility
- More obvious (subjective)
- easier to write

### Reasons to change to new style

- Easier to parse
- Allows for single line shortcuts eg ->etc {->etcetc}

## Comments can't go inline a command

For example the following doesn't work

```
<<set $foo = 2 + 1 *
	(2 + 1) // quick explanation why you would do this, seriously explain this, this seems weird to break this statement up...
>>
```

Purely an implementation issue, I see literally no reason this shouldn't happen, just saying that it is that case for now.

## Option parsing is far from ideal

Because the structure of the option syntax is `[[free text | nodeName]]`, when creating single structure option `[[nodeName]]` this is not being correctly identified as an option link but instead as an option text.
This isn't really a huge issue as I think it won't impact how it gets handled with it comes time to implement YarnSpinner from the generated code, but it is still not perfect.

Options are to change the syntax, or for me to put in more effort.
My suggestion to change the syntax so that it goes `[[nodeName | free text]]`, this makes it easier to parse.
This also makes more semantic sense to me as the link to the node is more important than the dialogue line that triggers it.

### Reasons to keep to the old style

- backwards compatibility

### Reasons to change to the new style

- easier to parse
- makes more sense start with the link (subjective)

## Keywords are allowed to be upper or lower case

Keywords like `else`, `endif`, `is`, etc etc are allowed in either all upper (eg `SET`) or all lower (eg `set`) case.
This is something I did quickly as it took almost zero effort to do and personally makes sense.

The question is, is it worth keeping this, picking a single case approach, extending it to allowed mixed case (eg `eNDiF`), extending to allow Titled case (eg `True`, `true`, `TRUE` all being valid but `tRue` not valid)?

## functions vs actions vs expressions

Currently actions work fine, functions do not but I think they'll work the same as the current YarnSpinner engine, the question is is this good?
As it currently stands the way the system determines if it is a function is if it matches as pattern of `<<bunchOfCharactersFromAnAllowedSet(some sort of expression)>>`, if it finds a space in there it determines it is an action instead.
Expressions on the other hand go `<<keyword expression>>`.
This is not only messy in my mind from a readability perspective it also makes it trickier to parse.
This means we have 3 uses of the `<<>>` syntax, one of which has keywords to control and two which are determined entirely on the text inside.
The expressions impact the dialogue, functions impact yarn spinner, actions impact the game.
In my mind these are completely unrelated to each other in functionality yet share a very common syntax, leading to confusion or small typos resulting in unexpected behaviour, eg:

- should <<if 5>> be an expression or an action?
- is <<hello there()>> a function or an action?
- how is <<assert(2 < 3)>> different from <<assert 2 < 3>>?

These are easily answered and understandable from my perspective but I believe the point should be to minimise the amount of specialised knowledge needed, and overloading syntax is the opposite of that.
While these can be fixed with warning and error messages it does feel a bit like something that should be investigated if this is the right way moving forward.
I would change this either so everything has a keyword to control its functionality, or change the '<<>>' so that the different capabilities use different syntax.

## Identifiers

The rules around what can be identifiers are effectively arbitrary, as it currently stands they can be any upper or lower variable, any numbers, and the _ symbol.
This also somewhat ties into almost everything else that can have generic text, i.e do we allow arbitrary text inside actions or does it have to conform to a pattern?
What should and shouldn't be allowed as identifiers and why?

## Headers

As it currently stands the allowed headers are `title` (required) and `tags`, `position`, and `colorID` all being optional.
Because of a combination of my hesitance to in line code, and ANTLR4 syntax there is nothing stopping you putting as many of the optional headers in as you want.

This is another area that needs to be discussed, not everything can be done in ANTLR, in places where it can be solved with in lining code we need to work out some rules for when we should and should not do this.
As it currently stands there are no code sections in the the grammar.
This is partially due to my experience with ANTLR and partially due to my unwillingness to add code, as each part of code is something that will have to be ported when the time comes, this feels messy and against the spirit of ANTLR to me.

With that said, some things will be MUCH easier to do with in line code and can likely be written in a way that is either applicable to multiple target languages or simple enough that tweaking it won't be a problem, this is all an area we need to look at as we head forward.
