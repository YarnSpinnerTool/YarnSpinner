/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System.Reflection;
using System.Runtime.CompilerServices;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[assembly: AssemblyTitle ("YarnSpinner")]
[assembly: AssemblyDescription ("A parser and runtime for the Yarn dialogue system.")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("Secret Lab Pty Ltd")]
[assembly: AssemblyProduct ("")]
[assembly: AssemblyCopyright ("© 2016-2017 Secret Lab Pty Ltd and Yarn Spinner contributors")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

[assembly: AssemblyVersion ("1.0.*")]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

// Make internals available to the localization tool
[assembly: InternalsVisibleTo("YarnSpinnerConsole")]
[assembly: InternalsVisibleTo("YarnSpinnerTests")]

