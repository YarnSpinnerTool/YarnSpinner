# Building Yarn Spinner

> ***Important:*** This document only matters to you if you want to build Yarn Spinner from source. In almost all cases, you'll be totally fine with [downloading a build](https://github.com/thesecretlab/YarnSpinner/releases), and using that in your project.

## Windows, Mac and Linux using MonoDevelop

To build Yarn Spinner on Windows or Mac, you'll need MonoDevelop. You can [download MonoDevelop for your OS from the MonoDevelop site](http://www.monodevelop.com/download/).
To build Yarn Spinner on Linux, you can either use mono or MonoDevelop's Flatpak.
* If you're going to use mono, you'll need [xbuild](http://www.mono-project.com/docs/tools+libraries/tools/xbuild/) and also [nuget](http://www.nuget.org). Then, ensure you have the correct packages installed by nuget, issuing `nuget restore YarnSpinner.sln`
* Once you have installed [Flatpak for your distribution](http://flatpak.org/getting.html), you can [download MonoDevelop from the MonoDevelop site](http://www.monodevelop.com/download/linux/). Open **YarnSpinner.sln**. Open the **Build menu**, and choose **Build All**. Open the **Unity/Assets/Yarn Spinner** folder. You'll find a copy of **YarnSpinner.dll** there. You can now copy that DLL file wherever you need it.

## Linux
As well as using MonoDevelop, you can use xbuild to build on Linux. At this stage, see the [build.sh](../build.sh) script for information on how to do this.

## Building Documentation

Yarn Spinner uses [Doxygen](https://www.stack.nl/~dimitri/doxygen) to generate [DocBook](http://docbook.org/), [HTML](https://en.wikipedia.org/wiki/HTML), [LaTeX](https://www.latex-project.org/help/documentation/), [RTF](https://en.wikipedia.org/wiki/Rich_Text_Format), and [XML](https://en.wikipedia.org/wiki/XML) documentation. [GNU GLOBAL](https://www.gnu.org/software/global/) is also used.

Basic steps to clean out existing documentation, generate new documentation and check the new documentation for generation errors. Note that some ocurrences of the word 'error' will be due to classes/methods etc. of YarnSpinner itself and not an actual error in the documentation.

```rm -fr Documentation/{docbook,html,latex,rtf,xml}
doxygen Documentation/Doxyfile > doxyoutput.txt 2>&1
grep -i error doxyoutput.txt```

[MarkDown](https://daringfireball.net/projects/markdown/) documentation is available via conversion of the XML output to Markdown using third party tools such as [Pandoc](http://pandoc.org) or [doxygen2md](https://github.com/pferdinand/doxygen2md)
