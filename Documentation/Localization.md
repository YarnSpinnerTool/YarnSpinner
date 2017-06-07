# LOCALIZATION
Yarn Spinner provides tools to help convert your game in to any language you like. This is achieved by usage of the YarnSpinnerConsole tool.

## Localisation procedure

1. We first use the tool to place a unique **tagline** on each line of text that the end user will see.  `YarnSpinnerConsole.exe taglines MyYarnFile.yarn.txt`.

2. Next, we use the tool to generate a file of strings (**genstrings**) in 'comma separated value' (csv) format.  `YarnSpinnerConsole.exe genstrings MyYarnFile.yarn.txt` This generates a file, in this example MyYarnFIle.yarn_lines.csv, that can then distributed to translators. It is recommended that this file first be renamed using a language identifier before distribution, eg
    ```
    MyYarnFIle.yarn_lines.enAU.csv
    MyYarnFIle.yarn_lines.ptTL.csv
    ```
3. When the file is returned from translators, we then use Yarn Spinner's DialogueRunner inside Unity to set String Groups for the required language.
<!-- Placeholder for image of DialogueRunner -->
## Footnotes
* While not essential that all files follow a standard, but highly recommended that consistency of format be used across all localisation files within a project eg [ISO-15897](https://www.iso.org/obp/ui/#iso:std:iso-iec:15897:ed-2:v1:en)
* Originally, Yarn Spinner used .json format. The Yarn Spinner Console can convert this json format to the new improved text format:
`YarnSpinnerConsole.exe convert --yarn Ship.json`


