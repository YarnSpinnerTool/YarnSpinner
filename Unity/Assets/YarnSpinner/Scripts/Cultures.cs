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
}