// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Shared
{
    using System;
    using System.Collections.Generic;

    public class DefinitionsFile
    {
        public int Version { get; set; } = 0;

        public List<Command> Commands { get; set; } = new();

        public List<Function> Functions { get; set; } = new();

        public enum Type
        {
            Unknown,
            Any,
            Number,
            String,
            Bool,
            Instance,
            Node,
            Enum,
        }

        public abstract class Action
        {
            public string YarnName { get; set; } = string.Empty;
            public string? DefinitionName { get; set; } = null;
            public string? FileName { get; set; } = null;
            public string? Language { get; set; } = null;
            public string? Documentation { get; set; } = null;
            public List<Parameter> Parameters { get; set; } = new();
            public bool Async { get; set; } = false;
            public Location? Location { get; set; } = null;
            public bool ContainsErrors { get; set; } = false;
            public List<string>? ErrorCodes { get; set; } = null;
        }

        public class Command : Action { }

        public class Function : Action
        {
            public Return Return { get; set; } = new();

            [Obsolete("Use " + nameof(Return))]
            public Return ReturnType { set => Return = value; }
        }

        public class Parameter
        {
            public string Name { get; set; } = string.Empty;
            public Type Type { get; set; }
            public string? Subtype { get; set; } = null;
            public string? Documentation { get; set; } = null;
            public string? DefaultValue { get; set; } = null;
            public bool IsParamsArray { get; set; } = false;

        }
        public class Return
        {
            public Type Type { get; set; }
            public string? Subtype { get; set; }
            public string? Description { get; set; }
        }

        public class Location
        {
            public Position Start { get; set; } = new();
            public Position End { get; set; } = new();
        }

        public class Position
        {
            public int Line { get; set; }
            public int Character { get; set; }
        }
    }
}
