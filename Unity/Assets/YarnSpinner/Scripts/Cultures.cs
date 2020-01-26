using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class Cultures {
    public static Culture[] AvailableCultures { get; private set; } = CultureInfo.GetCultures(CultureTypes.AllCultures)
        .Where(c => c.Name != "")
        .Select(c => new Culture {
            Name = c.Name,
            DisplayName = c.DisplayName
        })
        .Append(new Culture { Name = "mi", DisplayName = "Maori" })
        .OrderBy(c => c.DisplayName)
        .ToArray();

    public static string[] AvailableCulturesNames { get; private set; } = CulturesToNames(AvailableCultures);

    public static string[] AvailableCulturesDisplayNames { get; private set; } = CulturesToDisplayNames(AvailableCultures);

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
        //return AvailableCultures.Where(culture => languageNames.Contains(culture.Name)).Select(culture => culture.DisplayName).ToArray();
        return languageDisplayNames.ToArray();
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
        var DisplayNames = LanguageNamesToDisplayNames(languageNames);

        for (int i = 0; i < languageNames.Length; i++) {
            Culture addToCultures;
            addToCultures.Name = languageNames[i];
            addToCultures.DisplayName = DisplayNames[i];
            cultures.Add(addToCultures);
        }

        return cultures.ToArray();
    }

    public static string[] CulturesToDisplayNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.DisplayName}").ToArray();
    }

    public static string[] CulturesToNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.Name}").ToArray();
    }
}