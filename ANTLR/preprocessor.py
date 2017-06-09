# this does a few things
# first it replaces all \r\n with \n
# it replaces all tabs with 4 spaces
# it works out the indent/dedent levels and adds in symbols as necessary
# it saves it back out as a new file
# this isn't a clever tool, it is designed to make it easy for ANTLR to do its job afterwards
# as such it does no processing other than the minimum required to get ANTLR going
import sys

# this is probably over-documented and under-pythonic but I want it this way to make future porting simple
# at this point I think that's more useful

#opening the yarn file in a mode that I think strips out \r\n and uses \n in its place
with open(sys.argv[1], 'rU') as yarn:
    # for holding the finalised lines
    output_lines = []
    # A stack to keep info on how deep in we currently are
    # is made up of Tuples of the indentation depth and whether we emitted an indent token
    # has a default value of (0,False) so we can't fall off the end of the stack
    indents = [(0,False)]
    # var for determing if we are in a mode where we need to track indentation
    shouldTrackNextIndentation = False

    # our INDENT and DEDENT symbols
    # bell and vertical tab
    # these fill the role of { and } in other languages
    INDENT = '\a'
    DEDENT = '\v'
    # for debugging purposes, it is REALLY hard to see \a and \v
    if len(sys.argv) == 3 and sys.argv[2] == "debug":
        INDENT = '{'
        DEDENT = '}'

    # the option symbol, just to avoid typos
    OPTION = "->"

    # tracker for which line we are currently on
    line_count = 1
    # reading in each line (delineated by a \n) of the yarn file
    for line in yarn:
        # replacing \t with 4 spaces
        tweaked_line = line.replace('\t','    ')
        # stripping off the newline because it makes things a bit cleaner when it comes time to add in INDENT/DEDENT
        tweaked_line = tweaked_line.rstrip('\n')

        # counting the number of spaces at the start of the current line
        thisIndentation = len(tweaked_line) - len(tweaked_line.lstrip(' '))

        # checking that the line starts with an ->
        is_option = tweaked_line.strip().startswith(OPTION)
        # the top of the indent stack
        previousIndentation = indents[-1]

        # if we are in a state where we need to worry about indentation (eg recently emited a token or saw an ->)
        # and our indentation is deeper than the previous level
        if shouldTrackNextIndentation and thisIndentation > previousIndentation[0]:
            indents.append((thisIndentation,True))

            # adding the indent to the line above (personal preference really...)
            if len(output_lines) == 0:
                tweaked_line = INDENT + tweaked_line
            else:
                output_lines[-1] += INDENT
            # turning off tracking as we have emitted the indent
            shouldTrackNextIndentation = False
        # if our indentation is less than the previous
        # we have finished with the current block
        elif thisIndentation < previousIndentation[0]:
            while thisIndentation < indents[-1][0]:
                topLevel = indents.pop()
                if topLevel[1]:
                    if len(output_lines) == 0:
                        tweaked_line = DEDENT + tweaked_line
                    else:
                        output_lines[-1] += DEDENT
        # otherwise we just disable tracking
        else:
            shouldTrackNextIndentation = False

        # if we are an option we need to enable tracking
        if is_option:
            shouldTrackNextIndentation = True
            if indents[-1][0] < thisIndentation:
                indents.append((thisIndentation,False))

        # debugging string
        #print(str(line_count) + ": stack " + str(indents))
        output_lines.append(tweaked_line)
        line_count += 1

# saving the processed Yarn file back out and joining the lines back together with \n's
output = open('processed.yarn', 'w')
output.write('\n'.join(output_lines))
