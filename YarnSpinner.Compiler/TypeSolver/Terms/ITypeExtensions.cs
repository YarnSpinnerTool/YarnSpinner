using System.Linq;
using Yarn;
namespace TypeChecker
{
    public static class ITypeExtensions
    {
        public static IType Substitute(this IType term, Substitution s)
        {
            if (term is TypeVariable variable)
            {
                if (s.ContainsKey(variable))
                {
                    // The substitution contains a match for this term, but the
                    // substitute item might ITSELF need substituting. Get the
                    // substite, and then apply the substitution again.
                    return s[variable].Substitute(s);
                }
                else
                {
                    return term;
                }
            }
            else if (term is TypeFunction function)
            {
                // Functions are substituted by applying the substitution to
                // their return types and to each of their argument types.
                return new TypeFunction(function.ReturnType.Substitute(s), function.ArgumentTypes.Select(a => a.Substitute(s)).ToArray());
            }
            else
            {
                // No subsitution to apply here. Return the original type.
                return term;
            }
        }
    }
}
