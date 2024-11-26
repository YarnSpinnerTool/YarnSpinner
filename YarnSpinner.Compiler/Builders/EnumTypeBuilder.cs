// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Compiler
{
    /// <summary>
    /// Provides methods for constructing <see cref="Yarn.EnumType"/>
    /// objects.
    /// </summary>
    public class EnumTypeBuilder
    {
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
    }
}
