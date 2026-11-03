using System.Globalization;

namespace RaceDay.Mvc.Helpers;

public static class Format
{
    /// <summary>47 becomes "47th", 2 becomes "2nd" (used for finishing positions).</summary>
    public static string Ordinal(int number)
    {
        var abs = Math.Abs(number);
        var suffix = (abs % 100) is >= 11 and <= 13
            ? "th"
            : (abs % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{number}{suffix}";
    }

    public static string Time(TimeOnly? time) => time is null ? "–" : time.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string ShortTime(TimeOnly? time) => time is null ? "–" : time.Value.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Date(DateOnly date) => date.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) => amount == 0 ? "Free" : "R" + amount.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>Builds "?a=1&amp;b=2" from the pairs that have a value.</summary>
    public static string Query(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var query = string.Join("&", parts);
        return query.Length == 0 ? string.Empty : "?" + query;
    }
}
