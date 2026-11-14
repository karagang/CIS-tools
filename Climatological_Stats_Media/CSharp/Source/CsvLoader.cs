using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace IceClimatology;

public sealed record Observation(int Period, int Year, double Value, DateTime Observed);
public sealed record IceData(List<Observation> Observations, bool Weekly, List<string> Warnings);

public static class CsvLoader
{
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public static IceData Read(string path)
    {
        using var parser = new TextFieldParser(path, System.Text.Encoding.UTF8, true);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        string[] headers = (parser.ReadFields() ?? throw new FormatException("The CSV is empty."))
            .Select(h => h.Trim().TrimStart('\uFEFF')).ToArray();
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new FormatException("Duplicate CSV column names.");
        var lookup = headers.Select((name, index) => (name, index))
            .ToDictionary(h => h.name, h => h.index, StringComparer.OrdinalIgnoreCase);
        var years = headers.Select((name, index) => (name, index))
            .Where(h => Regex.IsMatch(h.name, @"^\d{4}$"))
            .Select(h => (Year: int.Parse(h.name, English), Index: h.index)).OrderBy(h => h.Year).ToArray();
        bool daily = years.Length > 0 && lookup.ContainsKey("Date");
        bool weekly = !daily && new[] { "DATE", "Season", "Concentration" }.All(lookup.ContainsKey);
        if (!daily && !weekly)
            throw new FormatException("Expected Date plus year columns, or DATE, Season, Concentration. Sorted export CSVs are not source data.");
        var observations = new List<Observation>();
        var warnings = new List<string>();
        var warnedSeasons = new HashSet<string>();
        var seen = new HashSet<(int Year, DateTime Date)>();
        while (!parser.EndOfData)
        {
            long line = parser.LineNumber;
            var row = parser.ReadFields();
            if (row == null || row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Length != headers.Length)
                throw new FormatException($"Line {line}: CSV row has the wrong number of fields.");
            string rawDate = row[lookup["Date"]].Trim();
            if (daily)
            {
                if (rawDate.Length == 0) continue;
                string[] formats = { "d-MMM yyyy", "MMM-d yyyy", "M/d yyyy", "MMM d yyyy", "M-d yyyy" };
                if (!DateTime.TryParseExact(rawDate + " 2000", formats, English, DateTimeStyles.None, out var observed))
                    throw new FormatException($"Line {line}: unsupported date '{rawDate}'; use dd-Mon or mm/dd.");
                foreach (var column in years)
                {
                    var value = Number(row[column.Index], line);
                    if (value is null) continue;
                    if (value < 0 || value > 100)
                        throw new FormatException($"Line {line}: concentration outside 0 to 100%.");
                    Add(new Observation(observed.Month * 100 + observed.Day, column.Year, value.Value, observed));
                }
            }
            else
            {
                string[] formats = { "yyyyMMdd", "yyyy-MM-dd", "M/d/yyyy" };
                if (!DateTime.TryParseExact(rawDate, formats, English, DateTimeStyles.None, out var observed))
                    throw new FormatException($"Line {line}: invalid DATE '{rawDate}'.");
                string season = row[lookup["Season"]].Trim();
                var match = Regex.Match(season, @"^(\d{4})-(\d{2}|\d{4})$");
                if (!match.Success)
                    throw new FormatException($"Line {line}: invalid Season '{season}'.");
                int start = int.Parse(match.Groups[1].Value, English);
                int end = int.Parse(match.Groups[2].Value, English);
                int expectedEnd = match.Groups[2].Length == 2 ? (start + 1) % 100 : start + 1;
                if (end != expectedEnd && warnedSeasons.Add(season))
                    warnings.Add($"Season '{season}' has an inconsistent ending year; using its starting year {start}. Source CSV is unchanged.");
                if (observed.Year != start && observed.Year != start + 1)
                    throw new FormatException($"Line {line}: DATE falls outside its Season.");
                var value = Number(row[lookup["Concentration"]], line);
                if (value is null) continue;
                if (value < 0 || value > 1)
                    throw new FormatException($"Line {line}: weekly Concentration must be a fraction from 0 to 1.");
                var reference = new DateTime(2001, observed.Month, observed.Month == 2 ? Math.Min(observed.Day, 28) : observed.Day);
                Add(new Observation((reference.DayOfYear - 1) / 7, start, value.Value * 100, observed));
            }
        }
        if (observations.Count == 0) throw new FormatException("No usable ice concentration observations found.");
        return new IceData(observations, weekly, warnings);

        void Add(Observation observation)
        {
            if (!seen.Add((observation.Year, observation.Observed)))
                throw new FormatException($"Duplicate observation for {observation.Year}, {observation.Observed:yyyy-MM-dd}.");
            observations.Add(observation);
        }
    }

    private static double? Number(string text, long line)
    {
        text = text.Trim();
        if (new[] { "", "na", "nan", "n/a", "null" }.Contains(text.ToLowerInvariant())) return null;
        if (!double.TryParse(text, NumberStyles.Float, English, out double result))
            throw new FormatException($"Line {line}: invalid concentration '{text}'.");
        return double.IsFinite(result) ? result : null;
    }
}
