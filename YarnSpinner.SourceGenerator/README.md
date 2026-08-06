This is the source code for the source code analyser and generator in Unity.
This is a normal incremental roslyn source generator and analyser and is intended to be built and deployed as a dll into Unity as `{Unity Package Root}/SourceGenerator/YarnSpinner.Unity.SourceCodeGenerator.dll`.
This should be built and repacked into a single DLL, this technically isn't necessary but it's just so much easier and more convenient to be bundled into one dll.

To make use of this build it like normal and then ILRepack it all into one assembly:

```bash
dotnet build

ilrepack --out=YarnSpinner.Unity.SourceCodeGenerator.dll bin/Debug/netstandard2.0/Analyser.dll bin/Debug/netstandard2.0/YarnSpinner.HostAnalysis.dll bin/Debug/netstandard2.0/YarnSpinner.Shared.dll --lib=bin/Debug/netstandard2.0/
```

and then move the generated dll into the `{Yarn Spinner for Unity Project Root}/SourceGenerator` folder.
