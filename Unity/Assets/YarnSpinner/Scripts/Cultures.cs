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

    public static string[] AvailableCulturesNames { get; private set; } = AvailableCultures
        .Select(c => $"{c.Name}")
        .ToArray();

    public static string[] AvailableCulturesDisplayNames { get; private set; } = AvailableCultures
        .Select(c => $"{c.DisplayName}")
        .ToArray();
    
    /// <summary>
    /// Return a DisplayName ("English") from a language ID/name (en)
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
    public static string[] LanguageNamesToDisplayNames (string[] languageNames) {
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
}