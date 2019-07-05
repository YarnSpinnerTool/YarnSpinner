
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "The exception constructors are by convention. It's nicer to have them (so they're available if necessary in the future), but it's not entirely required and won't hurt correctness.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "These should be properties, ideally, but I don't know if I can fix that easily right now without breaking anything.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Just a style guide thing.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "This SHOULD be fixed (to have more useful error messages), but it's out of scope for fixing locale issues.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "I think this rule is useless.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1820:Test for empty strings using string length", Justification = "SHOULD be fixed for performance, but it's not pressing.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "This would save a null check, but it's not that important.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1041:Provide ObsoleteAttribute message", Justification = "Should be fixed. It's currently unclear what changed or how to adapt usage.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Just style.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1715:Identifiers should have correct prefix", Justification = "Just style.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "Should be fixed eventually.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Only if they really are part of the public API.")]