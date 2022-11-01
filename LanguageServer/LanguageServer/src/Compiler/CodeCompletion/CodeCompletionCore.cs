using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

namespace Antlr4CodeCompletion.Core.CodeCompletion
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// Port of antlr-c3 javascript library to c#
    /// The c3 engine is able to provide code completion candidates useful for
    /// editors with ANTLR generated parsers, independent of the actual
    /// language/grammar used for the generation.
    /// https://github.com/mike-lischke/antlr4-c3
    /// </remarks>
    public class CodeCompletionCore
    {
        #region Fields

        private readonly ISet<int> ignoredTokens = new HashSet<int>();
        private readonly ISet<int> preferredRules = new HashSet<int>();

        private readonly Parser parser;
        private readonly ATN atn;
        private IList<IToken> tokens;

        private int tokenStartIndex = 0;
        private int statesProcessed = 0;

        // A mapping of rule index to token stream position to end token positions.
        // A rule which has been visited before with the same input position will always produce the same output positions.
        private readonly IDictionary<int, IDictionary<int, ISet<int>>> shortcutMap =
            new Dictionary<int, IDictionary<int, ISet<int>>>();

        // The collected candidates (rules and tokens).
        private readonly CandidatesCollection candidates = new CandidatesCollection();

        private readonly IDictionary<string, FollowSetsPerState> followSetsByATN =
            new Dictionary<string, FollowSetsPerState>();

        #endregion Fields

        #region Ctor

        public CodeCompletionCore(Parser parser, ISet<int> preferredRules, ISet<int> ignoredTokens)
        {
            this.parser = parser;
            this.atn = parser.Atn;

            this.ignoredTokens = ignoredTokens ?? new HashSet<int>();
            this.preferredRules = preferredRules ?? new HashSet<int>();
        }

        #endregion Ctor

        #region Public methods

        public CandidatesCollection GetCandidates(int caretTokenIndex = 0, ParserRuleContext context = null) => this.CollectCandidates(caretTokenIndex, context);

        /// <summary>
        /// This is the main entry point. The caret token index specifies the token stream index for the token which currently
        /// covers the caret (or any other position you want to get code completion candidates for).
        /// Optionally you can pass in a parser rule context which limits the ATN walk to only that or called rules. This can significantly
        /// speed up the retrieval process but might miss some candidates (if they are outside of the given context).
        /// </summary>
        public CandidatesCollection CollectCandidates(int caretTokenIndex, ParserRuleContext context)
        {
            this.shortcutMap.Clear();
            this.candidates.Rules.Clear();
            this.candidates.Tokens.Clear();
            this.statesProcessed = 0;

            this.tokenStartIndex = context != null ? context.Start.TokenIndex : 0;
            var tokenStream = this.parser.InputStream as ITokenStream;

            var currentIndex = tokenStream.Index;
            tokenStream.Seek(this.tokenStartIndex);
            this.tokens = new List<IToken>();
            var offset = 1;

            while (true)
            {
                var token = tokenStream.LT(offset++);
                this.tokens.Add(token);

                if (token.TokenIndex >= caretTokenIndex || token.Type == TokenConstants.EOF)
                {
                    break;
                }
            }

            tokenStream.Seek(currentIndex);

            var callStack = new LinkedList<int>();
            var startRule = context != null ? context.RuleIndex : 0;
            this.ProcessRule(this.atn.ruleToStartState[startRule], 0, callStack, "\n");

            tokenStream.Seek(currentIndex);

            // now post-process the rule candidates and find the last occurrences
            // of each preferred rule and extract its start and end in the input stream
            foreach (var ruleId in this.preferredRules)
            {
                var shortcut = this.shortcutMap.ContainsKey(ruleId) ? this.shortcutMap[ruleId] : null;
                if (shortcut == null || shortcut.Count <= 0)
                {
                    continue;
                }

                // select the right-most occurrence
                var startToken = shortcut.Max(item => item.Key);
                var endSet = shortcut[startToken];
                int endToken;

                if (endSet.Count <= 0)
                {
                    endToken = this.tokens.Count - 1;
                }
                else
                {
                    endToken = shortcut[startToken].Max();
                }

                var startOffset = this.tokens[startToken].StartIndex;
                int endOffset;

                if (this.tokens[endToken].Type == TokenConstants.EOF)
                {
                    // if last token is EOF, include trailing whitespace
                    endOffset = this.tokens[endToken].StartIndex;
                }
                else
                {
                    // if last token is not EOF, limit to matching tokens which excludes trailing whitespace
                    endOffset = this.tokens[endToken - 1].StopIndex + 1;
                }

                var ruleStartStop = new[] { startOffset, endOffset };
                this.candidates.RulePositions[ruleId] = ruleStartStop;
            }

            return this.candidates;
        }

        #endregion Public methods

        #region Private methods

        /// <summary>
        /// Check if the predicate associated with the given transition evaluates to true.
        /// </summary>
        private bool CheckPredicate(PredicateTransition transition) => transition.Predicate.Eval(this.parser, ParserRuleContext.EmptyContext);

        /// <summary>
        /// Walks the rule chain upwards to see if that matches any of the preferred rules.
        /// If found, that rule is added to the collection candidates and true is returned.
        /// </summary>
        private bool TranslateToRuleIndex(List<int> ruleStack)
        {
            if (this.preferredRules.Count <= 0)
            {
                return false;
            }

            // Loop over the rule stack from highest to lowest rule level. This way we properly handle the higher rule
            // if it contains a lower one that is also a preferred rule.
            for (var i = 0; i < ruleStack.Count; ++i)
            {
                if (this.preferredRules.Contains(ruleStack[i]))
                {
                    // Add the rule to our candidates list along with the current rule path,
                    // but only if there isn't already an entry like that.
                    var path = ruleStack.GetRange(0, i);
                    var addNew = true;
                    foreach (var entry in this.candidates.Rules)
                    {
                        if (!entry.Key.Equals(ruleStack[i]) || entry.Value.Count != path.Count)
                        {
                            continue;
                        }

                        // Found an entry for this rule. Same path? If so don't add a new (duplicate) entry.
                        if (path.SequenceEqual(entry.Value))
                        {
                            addNew = false;
                            break;
                        }
                    }

                    if (addNew)
                    {
                        this.candidates.Rules[ruleStack[i]] = path;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This method follows the given transition and collects all symbols within the same rule that directly follow it
        /// without intermediate transitions to other rules and only if there is a single symbol for a transition.
        /// </summary>
        private IList<int> GetFollowingTokens(Transition initialTransition)
        {
            var result = new LinkedList<int>();
            var seen = new LinkedList<ATNState>();
            var pipeline = new LinkedList<ATNState>();
            pipeline.AddLast(initialTransition.target);

            while (pipeline.Count > 0)
            {
                var state = pipeline.Last();
                pipeline.RemoveLast();

                foreach (var transition in state.TransitionsArray)
                {
                    if (transition.TransitionType == TransitionType.ATOM)
                    {
                        if (!transition.IsEpsilon)
                        {
                            var list = transition.Label.ToList();
                            if (list.Count == 1 && !this.ignoredTokens.Contains(list[0]))
                            {
                                result.AddLast(list[0]);
                                pipeline.AddLast(transition.target);
                            }
                        }
                        else
                        {
                            pipeline.AddLast(transition.target);
                        }
                    }
                }
            }

            return result.ToList();
        }

        /// <summary>
        /// Entry point for the recursive follow set collection function.
        /// </summary>
        private LinkedList<FollowSetWithPath> DetermineFollowSets(ATNState start, ATNState stop)
        {
            var result = new LinkedList<FollowSetWithPath>();
            var seen = new HashSet<ATNState>();
            var ruleStack = new LinkedList<int>();

            this.CollectFollowSets(start, stop, result, seen, ruleStack);

            return result;
        }

        /// <summary>
        /// Collects possible tokens which could be matched following the given ATN state. This is essentially the same
        /// algorithm as used in the LL1Analyzer class, but here we consider predicates also and use no parser rule context.
        /// </summary>
        private void CollectFollowSets(ATNState startState, ATNState stopState, LinkedList<FollowSetWithPath> followSets,
            ISet<ATNState> seen, LinkedList<int> ruleStack)
        {
            if (seen.Contains(startState))
            {
                return;
            }

            seen.Add(startState);

            if (startState.Equals(stopState) || startState.StateType == StateType.RuleStop)
            {
                var set = new FollowSetWithPath
                {
                    Intervals = IntervalSet.Of(TokenConstants.EPSILON),
                    Path = new List<int>(ruleStack),
                };
                followSets.AddLast(set);
                return;
            }

            foreach (var transition in startState.TransitionsArray)
            {
                if (transition.TransitionType == TransitionType.RULE)
                {
                    var ruleTransition = (RuleTransition)transition;
                    if (ruleStack.Find(ruleTransition.target.ruleIndex) != null)
                    {
                        continue;
                    }

                    ruleStack.AddLast(ruleTransition.target.ruleIndex);
                    this.CollectFollowSets(transition.target, stopState, followSets, seen, ruleStack);
                    ruleStack.RemoveLast();
                }
                else if (transition.TransitionType == TransitionType.PREDICATE)
                {
                    if (this.CheckPredicate((PredicateTransition)transition))
                    {
                        this.CollectFollowSets(transition.target, stopState, followSets, seen, ruleStack);
                    }
                }
                else if (transition.IsEpsilon)
                {
                    this.CollectFollowSets(transition.target, stopState, followSets, seen, ruleStack);
                }
                else if (transition.TransitionType == TransitionType.WILDCARD)
                {
                    var set = new FollowSetWithPath
                    {
                        Intervals = IntervalSet.Of(TokenConstants.MinUserTokenType, this.atn.maxTokenType),
                        Path = new List<int>(ruleStack),
                    };
                    followSets.AddLast(set);
                }
                else
                {
                    var label = transition.Label;
                    if (label != null && label.Count > 0)
                    {
                        if (transition.TransitionType == TransitionType.NOT_SET)
                        {
                            label = label.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, this.atn.maxTokenType));
                        }

                        var set = new FollowSetWithPath
                        {
                            Intervals = label,
                            Path = new List<int>(ruleStack),
                            Following = this.GetFollowingTokens(transition),
                        };
                        followSets.AddLast(set);
                    }
                }
            }
        }

        /// <summary>
        /// Walks the ATN for a single rule only. It returns the token stream position for each path that could be matched in this rule.
        /// The result can be empty in case we hit only non-epsilon transitions that didn't match the current input or if we
        /// hit the caret position.
        /// </summary>
        private ISet<int> ProcessRule(ATNState startState, int tokenIndex, LinkedList<int> callStack, string indentation)
        {
            // Start with rule specific handling before going into the ATN walk.

            // Check first if we've taken this path with the same input before.
            if (!this.shortcutMap.TryGetValue(startState.ruleIndex, out var positionMap))
            {
                positionMap = new Dictionary<int, ISet<int>>();
                this.shortcutMap[startState.ruleIndex] = positionMap;
            }
            else
            {
                if (positionMap.ContainsKey(tokenIndex))
                {
                    return positionMap[tokenIndex];
                }
            }

            var result = new HashSet<int>();

            // For rule start states we determine and cache the follow set, which gives us 3 advantages:
            // 1) We can quickly check if a symbol would be matched when we follow that rule. We can so check in advance
            //    and can save us all the intermediate steps if there is no match.
            // 2) We'll have all symbols that are collectable already together when we are at the caret when entering a rule.
            // 3) We get this lookup for free with any 2nd or further visit of the same rule, which often happens
            //    in non trivial grammars, especially with (recursive) expressions and of course when invoking code completion
            //    multiple times.
            if (!this.followSetsByATN.TryGetValue(this.parser.GetType().Name, out var setsPerState))
            {
                setsPerState = new FollowSetsPerState();
                this.followSetsByATN[this.parser.GetType().Name] = setsPerState;
            }

            if (!setsPerState.TryGetValue(startState.stateNumber, out var followSets))
            {
                followSets = new FollowSetsHolder();
                setsPerState[startState.stateNumber] = followSets;

                var stop = this.atn.ruleToStopState[startState.ruleIndex];
                followSets.Sets = this.DetermineFollowSets(startState, stop).ToList();

                // Sets are split by path to allow translating them to preferred rules. But for quick hit tests
                // it is also useful to have a set with all symbols combined.
                var combined = new IntervalSet();
                foreach (var set in followSets.Sets)
                {
                    combined.AddAll(set.Intervals);
                }

                followSets.Combined = combined;
            }

            callStack.AddLast(startState.ruleIndex);
            var currentSymbol = this.tokens[tokenIndex].Type;

            if (tokenIndex >= this.tokens.Count - 1)
            {
                // At caret?
                if (this.preferredRules.Contains(startState.ruleIndex))
                {
                    // No need to go deeper when collecting entries and we reach a rule that we want to collect anyway.
                    this.TranslateToRuleIndex(callStack.ToList());
                }
                else
                {
                    // Convert all follow sets to either single symbols or their associated preferred rule and add
                    // the result to our candidates list.
                    foreach (var set in followSets.Sets)
                    {
                        var fullPath = new LinkedList<int>(callStack);
                        foreach (var item in set.Path)
                        {
                            fullPath.AddLast(item);
                        }

                        if (!this.TranslateToRuleIndex(fullPath.ToList()))
                        {
                            foreach (var symbol in set.Intervals.ToList())
                            {
                                if (!this.ignoredTokens.Contains(symbol))
                                {
                                    if (!this.candidates.Tokens.ContainsKey(symbol))
                                    {
                                        // Following is empty if there is more than one entry in the set.
                                        this.candidates.Tokens[symbol] = set.Following;
                                    }
                                    else
                                    {
                                        // More than one following list for the same symbol.
                                        if (!this.candidates.Tokens[symbol].SequenceEqual(set.Following))
                                        {
                                            this.candidates.Tokens[symbol] = new List<int>();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                callStack.RemoveLast();
                return result;
            }
            else
            {
                // Process the rule if we either could pass it without consuming anything (epsilon transition)
                // or if the current input symbol will be matched somewhere after this entry point.
                // Otherwise stop here.
                if (!followSets.Combined.Contains(TokenConstants.EPSILON) && !followSets.Combined.Contains(currentSymbol))
                {
                    callStack.RemoveLast();
                    return result;
                }
            }

            // The current state execution pipeline contains all yet-to-be-processed ATN states in this rule.
            // For each such state we store the token index + a list of rules that lead to it.
            var statePipeline = new LinkedList<PipelineEntry>();

            // Bootstrap the pipeline.
            statePipeline.AddLast(new PipelineEntry(startState, tokenIndex));

            PipelineEntry currentEntry;
            while (statePipeline.Count() > 0)
            {
                currentEntry = statePipeline.Last();
                statePipeline.RemoveLast();
                ++this.statesProcessed;

                currentSymbol = this.tokens[currentEntry.TokenIndex].Type;

                var atCaret = currentEntry.TokenIndex >= this.tokens.Count - 1;

                switch (currentEntry.State.StateType)
                {
                    // Happens only for the first state in this rule, not subrules.
                    case StateType.RuleStart:
                        indentation += "  ";
                        break;

                    // Record the token index we are at, to report it to the caller.
                    case StateType.RuleStop:
                        result.Add(currentEntry.TokenIndex);
                        continue;

                    default:
                        break;
                }

                var transitions = currentEntry.State.TransitionsArray;
                foreach (var transition in transitions)
                {
                    switch (transition.TransitionType)
                    {
                        case TransitionType.RULE:
                            {
                                var endStatus = this.ProcessRule(transition.target, currentEntry.TokenIndex, callStack, indentation);
                                foreach (var position in endStatus)
                                {
                                    statePipeline.AddLast(new PipelineEntry(((RuleTransition)transition).followState, position));
                                }

                                break;
                            }

                        case TransitionType.PREDICATE:
                            {
                                if (this.CheckPredicate((PredicateTransition)transition))
                                {
                                    statePipeline.AddLast(new PipelineEntry(transition.target, currentEntry.TokenIndex));
                                }

                                break;
                            }

                        case TransitionType.WILDCARD:
                            {
                                if (atCaret)
                                {
                                    if (!this.TranslateToRuleIndex(callStack.ToList()))
                                    {
                                        foreach (var token in IntervalSet.Of(TokenConstants.MinUserTokenType, this.atn.maxTokenType).ToList())
                                        {
                                            if (!this.ignoredTokens.Contains(token))
                                            {
                                                this.candidates.Tokens[token] = new List<int>();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    statePipeline.AddLast(new PipelineEntry(transition.target, currentEntry.TokenIndex + 1));
                                }

                                break;
                            }

                        default:
                            {
                                if (transition.IsEpsilon)
                                {
                                    // Jump over simple states with a single outgoing epsilon transition.
                                    statePipeline.AddLast(new PipelineEntry(transition.target, currentEntry.TokenIndex));
                                    continue;
                                }

                                var set = transition.Label;
                                if (set != null && set.Count > 0)
                                {
                                    if (transition.TransitionType == TransitionType.NOT_SET)
                                    {
                                        set = set.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, this.atn.maxTokenType));
                                    }

                                    if (atCaret)
                                    {
                                        if (!this.TranslateToRuleIndex(callStack.ToList()))
                                        {
                                            var list = set.ToList();
                                            var isAddFollowing = list.Count == 1;

                                            foreach (var symbol in list)
                                            {
                                                if (!this.ignoredTokens.Contains(symbol))
                                                {
                                                    if (isAddFollowing)
                                                    {
                                                        this.candidates.Tokens[symbol] = this.GetFollowingTokens(transition);
                                                    }
                                                    else
                                                    {
                                                        this.candidates.Tokens[symbol] = new List<int>();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (set.Contains(currentSymbol))
                                        {
                                            statePipeline.AddLast(new PipelineEntry(transition.target, currentEntry.TokenIndex + 1));
                                        }
                                    }
                                }
                            }

                            break;
                    }
                }
            }

            callStack.RemoveLast();

            // Cache the result, for later lookup to avoid duplicate walks.
            positionMap[tokenIndex] = result;

            return result;
        }

        #endregion Private methods
    }
}