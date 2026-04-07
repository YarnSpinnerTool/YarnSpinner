// Copyright Yarn Spinner Pty Ltd Licensed under the MIT License. See LICENSE.md
// in project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Compiler
{
    /// <summary>
    /// Provides methods for constructing <see cref="Yarn.EnumType"/> objects.
    /// </summary>
    /// <remarks>
    /// To use this class, create an instance of it, and call the
    /// <c>With</c>-prefixed methods to set properties. When you're done, access
    /// the <see cref="EnumType"/> property to get the final, constructed <see
    /// cref="Yarn.EnumType"/>.
    /// </remarks>
    public class EnumTypeBuilder
    {
        /// <summary>
        /// Creates a new <see cref="EnumType"/> given a CLR enum type.
        /// </summary>
        /// <typeparam name="TEnum">The type of the CLR enum.</typeparam>
        /// <param name="description">An optional description to apply to the
        /// new Yarn enum type.</param>
        /// <returns>A Yarn type representing the enum.</returns>
        public static EnumType FromEnum<TEnum>(string? description = null) where TEnum : System.Enum
        {
            var enumType = new EnumType(
                typeof(TEnum).Name,
                description ?? $"Imported from enum {typeof(TEnum).FullName}",
                (TypeBase)Types.Number
            );

            var nameAndValues = Enum.GetNames(typeof(TEnum)).Zip(
                Enum.GetValues(typeof(TEnum)) as int[],
                (name, value) => new KeyValuePair<string, int>(name, value)
            );

            foreach (var pair in nameAndValues)
            {
                var value = new ConstantTypeProperty(Types.Number, pair.Value, $"Imported from {typeof(TEnum).FullName}.{pair.Key}");
                enumType.AddMember(pair.Key, value);
            }

            return enumType;
        }

        /// <summary>
        /// Gets the constructed enum type.
        /// </summary>
        public EnumType EnumType { get; } = new EnumType("<none>", "<none>", (TypeBase)Types.Number);

        /// <summary>
        /// Sets the <see cref="EnumType.Name"/> property of the constructed
        /// enum type.
        /// </summary>
        /// <param name="name">The new name.</param>
        /// <returns>The <see cref="EnumTypeBuilder"/> instance that received
        /// this method call.</returns>
        public EnumTypeBuilder WithName(string name)
        {
            EnumType.SetName(name);
            return this;
        }

        /// <summary>
        /// Sets the <see cref="EnumType.Description"/> property of the
        /// constructed enum type.
        /// </summary>
        /// <param name="description">The new description.</param>
        /// <returns>The <see cref="EnumTypeBuilder"/> instance that received
        /// this method call.</returns>
        public EnumTypeBuilder WithDescription(string description)
        {
            EnumType.SetDescription(description);
            return this;
        }

        /// <summary>
        /// Sets the <see cref="EnumType.RawType"/> property of the constructed
        /// enum type.
        /// </summary>
        /// <param name="rawType">The new raw type.</param>
        /// <returns>The <see cref="EnumTypeBuilder"/> instance that received
        /// this method call.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the enum has
        /// any cases added to it.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref
        /// name="rawType"/> is neither a string type nor a number
        /// type.</exception>
        public EnumTypeBuilder WithRawType(IType rawType)
        {
            if (EnumType.EnumCases.Any())
            {
                throw new InvalidOperationException("Can't modify the type of an enum if it contains any cases");
            }

            if (rawType is NumberType numberType)
            {
                EnumType.SetRawType(numberType);
            }
            else if (rawType is StringType stringType)
            {
                EnumType.SetRawType(stringType);
            }
            else
            {
                throw new ArgumentException("Can't set raw type of an enum to " + rawType.Name + ": must be number or string");
            }

            return this;
        }


        /// <summary>
        /// Adds a new string case to the constructed enum type.
        /// </summary>
        /// <param name="caseName">The name of the case, as it appears in Yarn
        /// Spinner scripts.</param>
        /// <param name="rawValue">The raw value of the case.</param>
        /// <param name="description">The optional description for the
        /// case.</param>
        /// <inheritdoc cref="WithCase(string, IType, IConvertible, string?)"
        /// path="/exception"/>
        /// <returns>The <see cref="EnumTypeBuilder"/> instance that received
        /// this method call.</returns>
        public EnumTypeBuilder WithCase(string caseName, string rawValue, string? description)
        {
            return WithCase(caseName, Types.String, rawValue, description);
        }

        /// <summary>
        /// Adds a new string case to the constructed enum type.
        /// </summary>
        /// <inheritdoc cref="WithCase(string, IType, IConvertible, string?)"
        /// path="/param"/>
        /// <inheritdoc cref="WithCase(string, IType, IConvertible, string?)"
        /// path="/exception"/>
        /// <returns>The <see cref="EnumTypeBuilder"/> instance that received
        /// this method call.</returns>
        public EnumTypeBuilder WithCase(string caseName, float rawValue, string? description)
        {
            return WithCase(caseName, Types.Number, rawValue, description);
        }

        /// <summary>
        /// Adds a new case to the constructed enum type.
        /// </summary>
        /// <param name="caseName">The name of the case, as it appears in Yarn
        /// Spinner scripts.</param>
        /// <param name="rawValue">The raw value of the case.</param>
        /// <param name="description">The optional description for the
        /// case.</param>
        /// <param name="rawType">The type of the case.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if the raw type doesn't
        /// match the enum's type, if a case with the given name already exists,
        /// or if a case with the given value already exists.</exception>
        private EnumTypeBuilder WithCase(string caseName, IType rawType, IConvertible rawValue, string? description)
        {
            if (!EnumType.RawType.Equals(rawType))
            {
                throw new ArgumentException($"Can't add case to enum: raw value type must be {EnumType.RawType.Name}. Set it with {nameof(WithRawType)} first.");
            }
            if (EnumType.EnumCases.Any(c => c.Key == caseName))
            {
                throw new ArgumentException($"Can't case {caseName} to enum: a case with this name already exists");
            }
            if (EnumType.EnumCases.Any(c => c.Value.Equals(rawValue)))
            {
                throw new ArgumentException($"Can't case {caseName} with value {rawValue} to enum: a case with this value already exists");
            }
            EnumType.AddMember(caseName, new ConstantTypeProperty(EnumType.RawType, rawValue, description ?? ""));
            return this;
        }

    }
}
