// #define VERBOSE_SOLVER

using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{
    /// <summary>
    /// Contains methods for solving systems of type equations.
    /// </summary>
    /// <remarks>
    /// This class contains the central algorithms used for solving the system
    /// of type equations produced by <see
    /// cref="Yarn.Compiler.TypeCheckerListener"/>.
    /// </remarks>
    internal static class Solver
    {
        private static void VerboseLog(int depth, string message) {
#if VERBOSE_SOLVER
            System.Console.Write(new string('.', depth));
            System.Console.Write(" ");
            System.Console.WriteLine(message);
#endif
        }
        /// <summary>
        /// Solves a collection of type constraints, and produces a
        /// substitution.
        /// </summary>
        /// <remarks>
        /// If the constraints cannot be solved, the returned substitution's
        /// <see cref="Substitution.IsFailed"/> will be <see langword="true"/>.
        /// </remarks>
        /// <param name="typeConstraints">The collection of type constraints to
        /// solve.</param>
        /// <param name="knownTypes">The list of types known to the
        /// solver.</param>
        /// <param name="diagnostics">The list of diagnostics. This list will be
        /// added to during operation.</param>
        /// <param name="partialSolution">A Substitution object containing a
        /// partial solution to the solver's list of type constraints, or <see
        /// langword="null"/>.</param>
        /// <param name="failuresAreErrors">If true, a constraint's failure to
        /// unify will result in an error Diagnostic being added to <paramref
        /// name="diagnostics"/>.</param>
        /// <returns>A Substitution containing either the solution, or a reason
        /// why the equations cannot be solved.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when
        /// <paramref name="typeConstraints"/> contains a type of constraint
        /// that could not be handled.</exception>
        internal static Substitution Solve(IEnumerable<TypeConstraint> typeConstraints, IEnumerable<TypeBase> knownTypes, ref List<Yarn.Compiler.Diagnostic> diagnostics, Substitution partialSolution = null, bool failuresAreErrors = true, int depth = 0)
        {
            var subst = partialSolution ?? new TypeChecker.Substitution();
            var remainingConstraints = new HashSet<TypeConstraint>(typeConstraints.WithoutTautologies());

            bool TryGetConstraint<T>(out T constraint, params System.Type[] hintTypes) where T : TypeConstraint
            {
                foreach (var hintType in hintTypes) {
                    if (typeof(T).IsAssignableFrom(hintType) == false) {
                        throw new System.ArgumentException($"{hintType} cannot be cast to {typeof(T)}");
                    }
                    constraint = (T)remainingConstraints?.FirstOrDefault(c => c.GetType() == hintType);
                    if (constraint != null) {
                        return true;
                    }
                }
                constraint = remainingConstraints?.OfType<T>().FirstOrDefault();
                return constraint != null;
            }

            while (remainingConstraints.Count > 0)
            {
                // Any constraint that depends upon the current contents of
                // partialSolution should be deferred as late as possible, so
                // that the substitution can have the most information. So,
                // we'll solve the constraints, one at a time, solving the
                // equalities first, then conjunctions, then disjunctions, then
                // any other constraint (since they simplify into equalities and
                // disjunctions).

                TypeConstraint currentConstraint = null;

                if (TryGetConstraint<TypeEqualityConstraint>(out var equalityConstraint))
                {
                    VerboseLog(depth, $"Solving {equalityConstraint.ToString()}");
                    currentConstraint = equalityConstraint;
                    subst = Unify(equalityConstraint.Left, equalityConstraint.Right, subst);
                    remainingConstraints.Remove(equalityConstraint);
                }
                else if (TryGetConstraint<ConjunctionConstraint>(out var conjunctionConstraint))
                {
                    VerboseLog(depth, $"Solving {conjunctionConstraint.ToString()}");
                    currentConstraint = conjunctionConstraint;

                    // All of these constraints must resolve, so simply add them to the list
                    foreach (var constraint in conjunctionConstraint)
                    {
                        remainingConstraints.Add(constraint);
                    }
                    remainingConstraints.Remove(conjunctionConstraint);
                }
                else if (TryGetConstraint<DisjunctionConstraint>(out var disjunctionConstraint))
                {
                    VerboseLog(depth, $"Solving {disjunctionConstraint.ToString()}");

                    currentConstraint = disjunctionConstraint;

                    // Try each of the constraints in the disjunction, attempting to
                    // solve for it.
                    foreach (var constraint in disjunctionConstraint)
                    {
                        VerboseLog(depth, $"Trying... {constraint.ToString()}");
                        var clonedSubst = subst.Clone();

                        // Create a new list of constraints where the disjunction is
                        // replaced with one of its terms
                        var potentialConstraintSet = new HashSet<TypeConstraint>(remainingConstraints);
                        potentialConstraintSet.Remove(disjunctionConstraint);
                        potentialConstraintSet.Add(constraint);

                        // Attempt to solve with this new list
                        var substitution = Solve(potentialConstraintSet, knownTypes, ref diagnostics, clonedSubst, false, depth + 1);

                        if (substitution.IsFailed == false)
                        {
                            // This solution works! Return it!
                            return substitution;
                        }
                        else
                        {
                            VerboseLog(depth+1, $"{constraint.ToString()} didn't work: {substitution.FailureReason}");
                        }
                    }
                    // If we got here, then none of our options worked.
                    subst.Fail($"No solution found for any option of: {disjunctionConstraint.ToString()}");
                    remainingConstraints.Remove(disjunctionConstraint);
                }
                else if (TryGetConstraint<TypeConstraint>(out var otherConstraint))
                {
                    // If it's any other type of constraint, then we'll simplify it,
                    // which turns it into equalities and/or disjunctions, which we
                    // can solve using the above procedures.

                    currentConstraint = otherConstraint;

                    VerboseLog(depth, $"Solving {otherConstraint.ToString()} (need to simplify it)");

                    // Simplify the constraint, and replace it with its simplified
                    // version
                    var simplifiedConstraint = otherConstraint.Simplify(subst, knownTypes);


                    if (simplifiedConstraint == null)
                    {
                        // Nothing to do!
                    }
                    else
                    {
                        VerboseLog(depth, $"Simplified it to {simplifiedConstraint.ToString()}");
                        remainingConstraints.Add(simplifiedConstraint);
                    }

                    remainingConstraints.Remove(otherConstraint);
                }

                if (subst.IsFailed)
                {
                    // Is this failure something we need to produce an error
                    // for?
                    if (failuresAreErrors)
                    {
                        VerboseLog(depth, $"Fatal: Constraint {currentConstraint} failed: {subst.FailureReason}");
                        // We want to log a diagnostic for this constraint's
                        // failure to apply.
                        if (currentConstraint == null)
                        {
                            // We have an error, but we don't know what
                            // constraint caused it? Internal error.
                            throw new System.InvalidOperationException($"Unexpected null value for {nameof(currentConstraint)}");
                        }
                        else
                        {
                            var failureMessage = currentConstraint.GetFailureMessage(subst);
                            diagnostics.Add(new Yarn.Compiler.Diagnostic(currentConstraint.SourceFileName, currentConstraint.SourceRange, failureMessage));
                        }

                        // We're going to continue evaluating other constraints,
                        // but we already know that the variables involved in
                        // this constraint can't be correctly resolved. Remove
                        // all constraints that involve a variable involved in
                        // this one.
                        IEnumerable<TypeVariable> failedConstraintVariables = currentConstraint.AllVariables;
                        var constraintsToDiscard = new HashSet<TypeConstraint>(
                            remainingConstraints.Where(c => c.AllVariables.Any(v => failedConstraintVariables.Contains(v))));

                        foreach (var c in constraintsToDiscard) {
                            remainingConstraints.Remove(c);
                        }
                    }
                    else
                    {
                        // We've failed, but we're not in a state where we need
                        // to report this error (because we're testing one part
                        // of a disjunction). Return the failed subst silently.
                        return subst;
                    }
                } else {
                    VerboseLog(depth + 1, "Success.");
                }
            }

            return subst;
        }


        /// <summary>Attempts to unify <paramref name="x"/> with <paramref
        /// name="y"/>, producing a new <see cref="Substitution"/>.</summary>
        /// <inheritdoc cref="Unify(IType, IType, Substitution)" path="/param"/>
        /// <returns>A new <see cref="Substitution"/>.</returns>
        internal static Substitution Unify(IType x, IType y)
        {
            var subst = new Substitution();
            Unify(x, y, subst);
            return subst;
        }

        /// <summary>
        /// Attempts to unify <paramref name="x"/> with <paramref name="y"/>,
        /// updating <paramref name="subst"/>.
        /// </summary>
        /// <remarks>
        /// <para>This method modifies <paramref name="subst"/> in place.</para>
        /// <para>
        /// If unifying <paramref name="x"/> with <paramref name="y"/> fails,
        /// <paramref name="subst"/>'s <see cref="Substitution.FailureReason"/> is
        /// updated.
        /// </para>
        /// </remarks>
        /// <param name="x">The first term to unify.</param>
        /// <param name="y">The second term to unify.</param>
        /// <param name="subst">The <see cref="Substitution"/> to use and
        /// update.</param>
        /// <returns>The updated <see cref="Substitution"/>.</returns>
        internal static Substitution Unify(IType x, IType y, Substitution subst)
        {
            if (subst.IsFailed)
            {
                // If we have received a failed substitution (which indicates that
                // unification is impossible), do no further work, pass it back up
                // the chain.

                // Add the context from this part of the call chain.
                subst.Fail($"{x} and {y} can't be unified");
                return subst;
            }

            if (x.Equals(y))
            {
                // If the two terms are already identical, no unification is
                // necessary. Return the original substitution, unmodified.
                return subst;
            }

            if (x is TypeVariable varX)
            {
                // If x is a variable, unify it with y.
                return UnifyVariable(varX, y, subst);
            }

            if (y is TypeVariable varY)
            {
                // Otherwise, if y is a variable, unify it with x.
                return UnifyVariable(varY, x, subst);
            }

            if (x is FunctionType xFunc && y is FunctionType yFunc)
            {
                // If they're both function applications, attempt to unify them.

                // We cannot unify two function applications if they don't have the
                // same number of parameters.
                if (xFunc.Parameters.Count() != yFunc.Parameters.Count())
                {
                    subst.Fail($"{xFunc} and {yFunc} have different parameters");
                    return subst;
                }

                // For each argument in the function applications, unify them.
                for (int i = 0; i < xFunc.Parameters.Count(); i++)
                {
                    subst = Unify(xFunc.Parameters.ElementAt(i), yFunc.Parameters.ElementAt(i), subst);
                }

                // Unify the return types, too.
                subst = Unify(xFunc.ReturnType, yFunc.ReturnType, subst);

                // All done. Return the updated substitution;
                return subst;
            }
            subst.Fail($"{x} and {y} can't be unified");
            return subst;
        }

        /// <summary>
        /// Unifies a variable with a term, by creating a substitution from the
        /// variable to the value represented by the term, taking into account the
        /// existing subsitution.
        /// </summary>
        /// <param name="var">The variable to unify.</param>
        /// <param name="term">The term to unify.</param>
        /// <param name="subst">The current substitution.</param>
        /// <returns>The updated substitution.</returns>
        private static Substitution UnifyVariable(TypeVariable var, IType term, Substitution subst)
        {
            // If we already have a unifier for var, then unify term with it.
            if (subst.ContainsKey(var))
            {
                return Unify(subst[var], term, subst);
            }

            // If term is a variable, and we have a unifier for it, then unify var
            // with whatever we've already unified term with.
            if (term is TypeVariable xVar && subst.ContainsKey(xVar))
            {
                return Unify(var, subst[xVar], subst);
            }

            // If term contains var in it, we cannot unify it, because that would
            // result in a cycle.
            if (OccursCheck(var, term, subst))
            {
                subst.Fail($"{var} appears in {term}");
                return subst;
            }

            // var is not yet in subst, and we can't simplify term. Extend 'subst'.
            subst.Add(var, term);

            return subst;
        }

        /// <summary>
        /// Recursively determines if <paramref name="var"/> exists as a sub-term in
        /// <paramref name="term"/>, taking into account any existing substitutions
        /// in <paramref name="subst"/>.
        /// </summary>
        /// <remarks>
        /// This function is used to prevent the creation of cyclical substitutions
        /// (i.e. <c>X: Y, Y: X</c>).
        /// </remarks>
        /// <param name="var">The variable to test for use inside <paramref
        /// name="term"/>.</param>
        /// <param name="term">The term to test to see if <paramref name="var"/> is
        /// used inside it.</param>
        /// <param name="subst">The current substitution.</param>
        /// <returns><see langword="true"/> if <paramref name="var"/> exists in
        /// <paramref name="term"/>, <see langword="false"/> otherwise.</returns>
        private static bool OccursCheck(TypeVariable var, IType term, Substitution subst)
        {
            // Does the variable 'var' occur anywhere inside 'term'?

            // Variables in 'term' are looked up in 'subst' and the check is applied
            // recursively.

            if (var.Equals(term))
            {
                // If var is term, then var exists in term by definition.
                return true;
            }
            else if (term is TypeVariable termVar && subst.ContainsKey(termVar))
            {
                // If term is itself a variable, and we already have a substitution
                // for it, then check var against whatever we're substituting term
                // for.
                return OccursCheck(var, subst[termVar], subst);
            }
            else if (term is FunctionType app)
            {
                // If term is a function application, then check var against each of
                // the function's arguments, and its return type.
                foreach (var arg in app.Parameters)
                {
                    if (OccursCheck(var, arg, subst))
                    {
                        return true;
                    }
                }
                return OccursCheck(var, app.ReturnType, subst);
            }
            else
            {
                return false;
            }
        }
    }
}
