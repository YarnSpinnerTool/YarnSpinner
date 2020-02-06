using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class Cultures {
    public static Culture[] AvailableCultures { get; private set; } = CultureInfo.GetCultures(CultureTypes.AllCultures)
        .Where(c => c.Name != "")
        .Select(c => new Culture {
            Name = c.Name,
            DisplayName = c.DisplayName,
            NativeName = c.NativeName
        })
        .Append(new Culture { Name = "mi", DisplayName = "Maori", NativeName = "Māori" })
        .OrderBy(c => c.DisplayName)
        .ToArray();

    public static string[] AvailableCulturesNames { get; private set; } = CulturesToNames(AvailableCultures);

    public static string[] AvailableCulturesDisplayNames { get; private set; } = CulturesToDisplayNames(AvailableCultures);

    public static string[] AvailableCulturesNativeNames { get; private set; } = CulturesToNativeNames(AvailableCultures);

    /// <summary>
    /// Return a DisplayName ("English") from a language ID/name ("en")
    /// </summary>
    /// <param name="languageName">The language ID to retrieve its DisplayName</param>
    /// <returns></returns>
    public static string LanguageNamesToDisplayNames(string languageName) {
        return LanguageNamesToDisplayNames(new string[] { languageName })[0];
    }

    /// <summary>
    /// Returns an array of DisplayNames ("English") from an array of language IDs/names ("en")
    /// </summary>
    /// <param name="languageNames">Array of language IDs to be converted to DisplayNames</param>
    /// <returns></returns>
    public static string[] LanguageNamesToDisplayNames(string[] languageNames) {
        List<string> languageDisplayNames = new List<string>();
        foreach (var languageName in languageNames) {
            if (AvailableCulturesNames.Contains(languageName)) {
                languageDisplayNames.Add(AvailableCulturesDisplayNames[Array.IndexOf(AvailableCulturesNames, languageName)]);
            } else {
                languageDisplayNames.Add("No valid language ID");
            }
        }
        return languageDisplayNames.ToArray();
    }

    /// <summary>
    /// Returns an array of native language names ("Deutsch" for German) from an array of language IDs/names ("de")
    /// </summary>
    /// <param name="languageNames">Array of language IDs to be converted to NativeName</param>
    /// <returns></returns>
    public static string[] LanguageNamesToNativeNames(string[] languageNames) {
        List<string> languageNativeNames = new List<string>();
        foreach (var languageName in languageNames) {
            if (AvailableCulturesNames.Contains(languageName)) {
                languageNativeNames.Add(AvailableCulturesNativeNames[Array.IndexOf(AvailableCulturesNames, languageName)]);
            } else {
                languageNativeNames.Add("No valid language ID");
            }
        }
        return languageNativeNames.ToArray();
    }

    /// <summary>
    /// Return a Culture from a language ID/name ("en")
    /// </summary>
    /// <param name="languageName">The language ID to retrieve its Culture</param>
    /// <returns></returns>
    public static Culture LanguageNamesToCultures(string languageName) {
        return LanguageNamesToCultures(new string[] { languageName })[0];
    }

    /// <summary>
    /// Returns an array of Cultures from an array of language IDs/names ("en")
    /// </summary>
    /// <param name="languageNames">Array of language IDs to be converted to DisplayNames</param>
    /// <returns></returns>
    public static Culture[] LanguageNamesToCultures(string[] languageNames) {
        List<Culture> cultures = new List<Culture>();
        var displayNames = LanguageNamesToDisplayNames(languageNames);
        var nativeNames = LanguageNamesToNativeNames(languageNames);

        for (int i = 0; i < languageNames.Length; i++) {
            cultures.Add(new Culture {
                Name = languageNames[i],
                DisplayName = displayNames[i],
                NativeName = nativeNames[i]
            });
        }

        return cultures.ToArray();
    }

    public static string[] CulturesToDisplayNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.DisplayName}").ToArray();
    }

    public static string[] CulturesToNativeNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.NativeName}").ToArray();
    }

    public static string[] CulturesToNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.Name}").ToArray();
    }
}