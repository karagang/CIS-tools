using System.Globalization;
using System.Text;

namespace IceClimatology;

public static class Reports
{
    public static readonly CultureInfo English = CultureInfo.InvariantCulture;
    public static string Season(int year, bool weekly) => weekly ? $"{year}-{(year + 1) % 100:00}" : year.ToString(English);
    public static string Percent(double value) => value.ToString("F2", English);
    public static string Ordinal(int n) => n.ToString(English) +
        (n % 100 is >= 11 and <= 13 ? "th" : ((n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" }));

    public static string PeriodName(int period, bool weekly)
    {
        if (!weekly) return new DateTime(2000, period / 100, period % 100).ToString("MMM dd", English);
        var first = new DateTime(2001, 1, 1).AddDays(period * 7);
        var last = first.AddDays(6);
        if (last.Year > 2001) last = new DateTime(2001, 12, 31);
        return first.ToString("MMM dd", English) + " to " + last.ToString("MMM dd", English);
    }

    public static List<string> Rankings(IceData data, int target, bool includeLow)
    {
        var lines = new List<string>();
        foreach (var period in data.Observations.GroupBy(o => o.Period).OrderBy(g => g.Key))
        {
            var pairs = period.GroupBy(o => o.Year).Select(g => g.MaxBy(o => o.Value)!).ToList();
            var current = pairs.FirstOrDefault(o => o.Year == target);
            if (current is null) continue;
            var higher = pairs.Where(o => o.Value > current.Value).OrderByDescending(o => o.Value).ThenBy(o => o.Year).ToList();
            var lower = pairs.Where(o => o.Value < current.Value).OrderBy(o => o.Value).ThenBy(o => o.Year).ToList();
            Add("highest", higher, 10);
            if (includeLow && pairs.Count(o => o.Value == 0) <= 5 && pairs.Count(o => o.Value > 0) >= 5)
                Add("lowest", lower, 5);

            void Add(string direction, List<Observation> others, int limit)
            {
                int rank = others.Count + 1;
                if (rank > limit) return;
                bool tied = pairs.Any(o => o.Year != target && o.Value == current.Value);
                string label = PeriodName(period.Key, data.Weekly);
                if (data.Weekly) label += $" (observed {current.Observed:yyyy-MM-dd})";
                lines.Add($"{label}: {Percent(current.Value)}%, {(tied ? "tied " : "")}{Ordinal(rank)} {direction}, {Season(target, data.Weekly)}; {pairs.Count} years/seasons with data");
                if (others.Count > 0)
                {
                    lines.Add($"  {(direction == "highest" ? "Higher" : "Lower")} concentrations in this period:");
                    lines.AddRange(others.Select(o => $"    {Season(o.Year, data.Weekly)}: {Percent(o.Value)}%"));
                }
                lines.Add("");
            }
        }
        if (lines.Count == 0) lines.Add("No qualifying rankings for the target year/season.");
        return lines;
    }

    public static List<string> Monthly(IceData data)
    {
        if (!data.Weekly) throw new FormatException("The monthly seasonal-maximum report requires the weekly DATE/Season CSV format.");
        var lines = new List<string> { "Monthly seasonal maximum ice concentrations (November through May)", "" };
        foreach (int month in new[] { 11, 12, 1, 2, 3, 4, 5 })
        {
            var maxima = data.Observations.Where(o => o.Observed.Month == month)
                .GroupBy(o => o.Year).Select(g => g.MaxBy(o => o.Value)!).ToList();
            lines.Add(English.DateTimeFormat.GetMonthName(month));
            if (maxima.Count == 0) { lines.AddRange(new[] { "No observations.", "" }); continue; }
            foreach (bool highest in new[] { true, false })
            {
                lines.Add($"{(highest ? "Highest" : "Lowest")} 10 seasonal maxima:");
                var sorted = highest ? maxima.OrderByDescending(o => o.Value) : maxima.OrderBy(o => o.Value);
                foreach (var row in sorted.Take(10))
                    lines.Add($"  {row.Observed:yyyy-MM-dd}  {Season(row.Year, true)}  {Percent(row.Value)}%");
            }
            lines.Add("");
        }
        return lines;
    }

    public static string WriteText(string path, IceData data, int start, int target, string mode, string outputDir)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        var lines = new List<string> {
            $"{name}: Ice Concentration {English.TextInfo.ToTitleCase(mode)} Report",
            $"Target: {Season(target, data.Weekly)}; Comparison period: {Season(start, data.Weekly)} to {Season(target, data.Weekly)}",
            "Missing observations are excluded. Ties share rank: 1 + number of strictly higher/lower observations.",
            mode == "climatology" ? "Top 10 highest; bottom 5 lowest only when <=5 zero and >=5 positive years/seasons." : "", ""
        };
        if (data.Weekly)
            lines.AddRange(new[] {
                "Weekly method: fixed Jan 01-07, Jan 08-14, etc. calendar bins; Feb 29 shares Feb 28's bin.",
                "Each season contributes its maximum observation in each bin; missing bins are excluded.",
                "Season labels and start year use the FIRST year (1995 means 1995-96). Concentration fractions multiplied by 100.", ""
            });
        lines.AddRange(mode == "monthly" ? Monthly(data) : Rankings(data, target, mode != "topten"));
        foreach (string warning in data.Warnings) lines.Add("Warning: " + warning);
        lines.AddRange(new[] { "", "CSV file used: " + Path.GetFullPath(path) });
        return WriteUnique(outputDir, $"{name}_{mode}_{target}_from_{start}.txt", string.Join("\n", lines) + "\n");
    }

    public static string WriteUnique(string directory, string filename, string contents)
    {
        Directory.CreateDirectory(directory);
        string stem = Path.GetFileNameWithoutExtension(filename), extension = Path.GetExtension(filename);
        for (int suffix = 1; ; suffix++)
        {
            string path = Path.Combine(directory, suffix == 1 ? filename : $"{stem}_{suffix}{extension}");
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write); }
            catch (IOException) when (File.Exists(path)) { continue; }
            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(contents);
            return path;
        }
    }
}
