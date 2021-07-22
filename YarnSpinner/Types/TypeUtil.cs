namespace Yarn
{
    /// <summary>
    /// Contains utility methods for working with types and their methods.
    /// </summary>
    internal static class TypeUtil
    {
        internal static System.Delegate GetMethod<TResult>(System.Func<Value, Value, TResult> f) => f;

        internal static System.Delegate GetMethod<T>(System.Func<Value, T> f) => f;

        internal static System.Delegate GetMethod<T>(System.Func<T> f) => f;

        internal static IType FindImplementingTypeForMethod(IType type, string methodName)
        {
            if (type is null)
            {
                throw new System.ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new System.ArgumentException($"'{nameof(methodName)}' cannot be null or empty.", nameof(methodName));
            }

            var currentType = type;

            // Walk up the type hierarchy, looking for a type that
            // implements a method by this name
            while (currentType != null)
            {

                if (currentType.Methods != null && currentType.Methods.ContainsKey(methodName))
                {
                    return currentType;
                }

                currentType = currentType.Parent;
            }

            return null;
        }

        internal static string GetCanonicalNameForMethod(IType implementingType, string methodName)
        {
            if (implementingType is null)
            {
                throw new System.ArgumentNullException(nameof(implementingType));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new System.ArgumentException($"'{nameof(methodName)}' cannot be null or empty.", nameof(methodName));
            }

            return $"{implementingType.Name}.{methodName}";
        }

        internal static void GetNamesFromCanonicalName(string canonicalName, out string typeName, out string methodName)
        {
            if (string.IsNullOrEmpty(canonicalName))
            {
                throw new System.ArgumentException($"'{nameof(canonicalName)}' cannot be null or empty.", nameof(canonicalName));
            }

            var components = canonicalName.Split(new[] { '.' }, 2);

            if (components.Length != 2)
            {
                throw new System.ArgumentException($"Invalid canonical method name {canonicalName}");
            }

            typeName = components[0];
            methodName = components[1];
        }
    }
}
