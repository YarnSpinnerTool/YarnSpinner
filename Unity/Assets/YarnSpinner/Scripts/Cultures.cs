using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// All <see cref="Culture"/>s supported by YarnSpinner.
/// </summary>
public static class Cultures {
    /// <summary>
    /// Get all <see cref="Culture"/>s supported by YarnSpinner.
    /// </summary>
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

    /// <summary>
    /// Get all <see cref="Culture.Name"/>s supported by YarnSpinner, e.g. "de-CH" for "German (Switzerland)".
    /// </summary>
    public static string[] AvailableCulturesNames { get; private set; } = CulturesToNames(AvailableCultures);

    /// <summary>
    /// Get all <see cref="Culture.DisplayName"/>s supported by YarnSpinner, e.g. "German (Switzerland)" for "de-CH".
    /// </summary>
    public static string[] AvailableCulturesDisplayNames { get; private set; } = CulturesToDisplayNames(AvailableCultures);

    /// <summary>
    /// Get all <see cref="Culture.NativeName"/>s supported by YarnSpinner, e.g. "Deutsch (Schweiz)" for "German (Switzerland)".
    /// </summary>
    public static string[] AvailableCulturesNativeNames { get; private set; } = CulturesToNativeNames(AvailableCultures);

    /// <summary>
    /// Get a <see cref="Culture.DisplayName"/> from a <see cref="Culture.Name"/>.
    /// </summary>
    /// <param name="languageName">The language ID (<see cref="Culture.Name"/>) to retrieve its <see cref="Culture.DisplayName"/></param>
    public static string LanguageNamesToDisplayNames(string languageName) {
        return LanguageNamesToDisplayNames(new string[] { languageName })[0];
    }

    /// <summary>
    /// Get the <see cref="Culture.DisplayName"/>s associated with the given <see cref="Culture.Name"/>s.
    /// </summary>
    /// <param name="languageNames">Array of language IDs to be converted to DisplayNames</param>
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
    /// Get all <see cref="Culture.NativeName"/>s from an array of <see cref="Culture.Name"/>s.
    /// </summary>
    /// <param name="languageName">A string representing the <see cref="Culture.Name"/>.</param>
    public static string LanguageNamesToNativeNames(string languageName) {
        return LanguageNamesToNativeNames(new string[] { languageName })[0];
    }

    /// <summary>
    /// Get the <see cref="Culture.NativeName"/>s from an array of <see cref="Culture.Name"/>s.
    /// </summary>
    /// <param name="languageNames">Array of <see cref="Culture.Name"/>.</param>
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
    /// Get the <see cref="Culture"/> associated with a <see cref="Culture.Name"/>.
    /// </summary>
    /// <param name="languageName">A string representing the <see cref="Culture.Name"/>.</param>
    public static Culture LanguageNamesToCultures(string languageName) {
        return LanguageNamesToCultures(new string[] { languageName })[0];
    }

    /// <summary>
    /// Get all <see cref="Culture"/>s from an array of <see cref="Culture.Name"/>s.
    /// </summary>
    /// <param name="languageNames">Array of <see cref="Culture.Name"/>.</param>
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

    /// <summary>
    /// Get the <see cref="Culture.DisplayName"/>s from an array of <see cref="Culture"/>s.
    /// </summary>
    /// <param name="cultures">Array of <see cref="Culture"/>.</param>
    public static string[] CulturesToDisplayNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.DisplayName}").ToArray();
    }

    /// <summary>
    /// Get the <see cref="Culture.NativeName"/>s from an array of <see cref="Culture"/>s.
    /// </summary>
    /// <param name="cultures">Array of <see cref="Culture"/>.</param>
    public static string[] CulturesToNativeNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.NativeName}").ToArray();
    }

    /// <summary>
    /// Get the <see cref="Culture.Name"/>s from an array of <see cref="Culture"/>s.
    /// </summary>
    /// <param name="cultures">Array of <see cref="Culture"/>.</param>
    public static string[] CulturesToNames(Culture[] cultures) {
        return cultures.Select(c => $"{c.Name}").ToArray();
    }
}