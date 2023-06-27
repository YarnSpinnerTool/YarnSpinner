// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;

    /// <summary>
    /// Contains utility methods for working with types and their methods.
    /// </summary>
    internal static class TypeUtil
    {
        // Helper functions that allow us to easily cast method groups of
        // certain types to System.Delegate - i.e. we can say:
        // 
        // int DoCoolThing(Value a, Value b) { ... }
        // var doCoolThingDelegate = TypeUtil.GetMethod(DoCoolThing);
        internal static System.Delegate GetMethod<TResult>(System.Func<Value, Value, TResult> f) => f;

        internal static System.Delegate GetMethod<T>(System.Func<Value, T> f) => f;

        internal static System.Delegate GetMethod<T>(System.Func<T> f) => f;

        /// <summary>
        /// Returns the type that contains the actual implementation of the
        /// method indicated by <paramref name="methodName"/>, given a type or
        /// sub-type.
        /// </summary>
        /// <remarks>
        /// This method checks to see if <paramref name="type"/> contains a
        /// concrete implementation of a method named <paramref
        /// name="methodName"/>. If it does, <paramref name="type"/> is
        /// returned; otherwise, the parent of <paramref name="type"/> is
        /// checked, and so on up the hierarchy. If no type in <paramref
        /// name="type"/>'s parent chain implements a method named <paramref
        /// name="methodName"/>, this method returns <see langword="null"/>.
        /// </remarks>
        /// <param name="type">The type, or sub-type, to start searching for an
        /// implementation for the method.</param>
        /// <param name="methodName">The name of the method to search
        /// for.</param>
        /// <returns>The <see cref="IType"/> object that contains an
        /// implementation of <paramref name="methodName"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref
        /// name="type"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown if methodName is <see
        /// langword="null"/> or empty.</exception>
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

        /// <summary>
        /// Checks to see if <paramref name="subType"/> is equal to
        /// <paramref name="parentType"/>, or if <paramref
        /// name="parentType"/> exists in <paramref name="subType"/>'s type
        /// hierarchy.
        /// </summary>
        /// <param name="parentType">The parent type to check
        /// against.</param>
        /// <param name="subType">The type to check if it's a subtype of
        /// <paramref name="parentType"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="subType"/>
        /// is the same or a subtype of <paramref name="parentType"/>; <see
        /// langword="false"/> otherwise.</returns>
        internal static bool IsSubType(IType parentType, IType subType)
        {
            if (subType == BuiltinTypes.Undefined && parentType == BuiltinTypes.Any)
            {
                // Special case: the undefined type is always a subtype of
                // the Any type, because ALL types are a subtype of the Any
                // type.
                return true;
            }

            if (subType == BuiltinTypes.Undefined)
            {
                // The subtype is undefined. Assume that it is not a
                // subtype of parentType.
                return false;
            }

            var currentType = subType;

            while (currentType != null)
            {
                // TODO: this is a strict object comparison; a more
                // sophisticated type unification might be better
                if (currentType == parentType)
                {
                    return true;
                }

                currentType = currentType.Parent;
            }

            // We reached the top of the type hierarchy, and didn't find
            // parentType. subType is not a subtype of parentType.
            return false;
        }
    }
}
