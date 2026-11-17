using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace GreatLakesCharts;

public sealed record LakeConfig(string CsvFile, string FilePrefix, string EnglishTitle, string FrenchTitle);
public sealed record IceRow(DateTime Date, Dictionary<int, double> Values);
public sealed record IceData(int[] Years, List<IceRow> Rows);
public sealed record Analysis(int StartYear, int CurrentYear, int[] HistoryYears, int[] Recent,
    int[] Heavier, int[] Lighter, bool HasLowerYears, Dictionary<int, double> Maxima, double[] Average);

public static class ChartGenerator
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(LakeConfig lake, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        bool interactive = !args.Contains("--defaults") && !Console.IsInputRedirected;
        try
        {
            var options = ParseOptions(args);
            if (options.ContainsKey("--help"))
            {
                Console.WriteLine("Generate four English and four French ice coverage PNG charts.\n" +
                    "Double-click the EXE and press Enter to accept each default.\n\n" +
                    "Options:\n  --defaults             Use default CSV and earliest start year, without prompts\n" +
                    "  --csv PATH             Read a different CSV\n  --start-year YEAR      First historical year to include\n" +
                    "  --output DIRECTORY     Override the output folder\n" +
                    "  --summary PATH         Also write calculation details as JSON\n  --help                 Show this help");
                return 0;
            }
            string csv = Path.Combine(AppContext.BaseDirectory, lake.CsvFile);
            if (options.TryGetValue("--csv", out string? suppliedCsv)) csv = suppliedCsv;
            else if (interactive)
            {
                Console.WriteLine($"{lake.EnglishTitle} / {lake.FrenchTitle}");
                Console.Write($"CSV file (Enter for default):\n[{csv}]: ");
                string answer = (Console.ReadLine() ?? "").Trim().Trim('"', '\'');
                if (answer.Length != 0) csv = answer;
            }
            Console.WriteLine($"Reading {csv}...");
            IceData data = ReadCsv(csv);
            int startYear = data.Years[0];
            if (options.TryGetValue("--start-year", out string? suppliedYear))
            {
                if (!int.TryParse(suppliedYear, out startYear)) throw new ArgumentException("Start year must be a whole number.");
            }
            else if (interactive)
            {
                Console.WriteLine($"Data available from {data.Years[0]} to {data.Years[^1]}.");
                while (true)
                {
                    Console.Write($"Enter Start Year for comparison [Default: {data.Years[0]}]: ");
                    string answer = (Console.ReadLine() ?? "").Trim();
                    if (answer.Length == 0) break;
                    if (int.TryParse(answer, out int year) && year >= data.Years[0] && year < data.Years[^1])
                    { startYear = year; break; }
                    Console.WriteLine($"Please enter a year from {data.Years[0]} through {data.Years[^1] - 1}; at least one historical year is required.");
                }
            }
            Analysis analysis = Analyze(data, startYear);
            string output = Path.GetFullPath(options.GetValueOrDefault("--output") ?? Path.Combine(AppContext.BaseDirectory, "output"));
            Directory.CreateDirectory(output);
            Console.WriteLine($"Calculating dynamic average ({startYear}-{analysis.HistoryYears[^1]})...");
            foreach (bool french in new[] { false, true })
            {
                string lang = french ? "FR" : "EN";
                string title = french ? lake.FrenchTitle : lake.EnglishTitle;
                var panels = new[]
                {
                    new Panel(analysis.Recent, Description("Recent", analysis, french), "Recent"),
                    new Panel(analysis.Heavier, Description("Heavier", analysis, french), "Heavier"),
                    new Panel(analysis.Lighter, Description("Lighter", analysis, french), "Lighter")
                };
                string combined = Path.Combine(output, $"{lake.FilePrefix}_{analysis.CurrentYear}_Combined_{lang}.png");
                Renderer.Save(combined, title, panels, data, analysis, french, true);
                Console.WriteLine($"Saved: {combined}");
                foreach (Panel panel in panels)
                {
                    string file = Path.Combine(output, $"{lake.FilePrefix}_{analysis.CurrentYear}_{panel.Suffix}_{lang}.png");
                    Renderer.Save(file, title, [panel], data, analysis, french, false);
                    Console.WriteLine($"Saved: {file}");
                }
            }
            if (options.TryGetValue("--summary", out string? summaryPath))
            {
                object? Finite(double value) => double.IsFinite(value) ? value : null;
                var summary = new
                {
                    analysis.StartYear, analysis.CurrentYear, analysis.HistoryYears,
                    analysis.Recent, analysis.Heavier, analysis.Lighter, analysis.HasLowerYears,
                    Maxima = analysis.Maxima.ToDictionary(p => p.Key, p => Finite(p.Value)),
                    Rows = data.Rows.Select((row, i) => new { Date = row.Date.ToString("yyyy-MM-dd", Invariant), Average = Finite(analysis.Average[i]) })
                };
                File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            }
            Console.WriteLine("\nAll eight charts generated successfully.");
            if (interactive) { Console.Write("Press Enter to close."); Console.ReadLine(); }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (interactive) { Console.Write("Press Enter to close."); Console.ReadLine(); }
            return 1;
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option is "--defaults" or "--help") result.Add(option, "");
            else if (option is "--csv" or "--start-year" or "--output" or "--summary")
            {
                if (++i == args.Length) throw new ArgumentException($"Missing value for {option}.");
                result.Add(option, args[i]);
            }
            else throw new ArgumentException($"Unknown option: {option}. Use --help for usage.");
        }
        return result;
    }

    public static IceData ReadCsv(string path)
    {
        using var parser = new TextFieldParser(path, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(",");
        string[] header = parser.ReadFields() ?? throw new InvalidDataException("CSV is empty.");
        int dateIndex = Array.IndexOf(header, "Date");
        if (dateIndex < 0) throw new InvalidDataException("CSV must contain a Date column.");
        var columns = header.Select((name, index) => (name, index))
            .Where(c => int.TryParse(c.name, out _)).ToDictionary(c => int.Parse(c.name, Invariant), c => c.index);
        int[] years = columns.Keys.Order().ToArray();
        if (years.Length < 2) throw new InvalidDataException("CSV must contain at least two year columns.");
        var rows = new List<IceRow>();
        while (!parser.EndOfData)
        {
            string[] fields = parser.ReadFields()!;
            if (fields.Length != header.Length) throw new InvalidDataException($"CSV row near line {parser.LineNumber} has {fields.Length} fields; expected {header.Length}.");
            if (!DateTime.TryParseExact(fields[dateIndex] + "-2000", ["d-MMM-yyyy", "dd-MMM-yyyy"], Invariant,
                    DateTimeStyles.None, out DateTime date)) continue;
            date = new DateTime(date.Month >= 8 ? 1999 : 2000, date.Month, date.Day);
            var values = new Dictionary<int, double>();
            foreach ((int year, int column) in columns)
            {
                string raw = fields[column];
                if (raw.Length == 0 || raw.Equals("NA", StringComparison.OrdinalIgnoreCase) || raw.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                    values[year] = double.NaN;
                else if (double.TryParse(raw, NumberStyles.Float, Invariant, out double number) && double.IsFinite(number)) values[year] = number;
                else throw new InvalidDataException($"Invalid ice coverage '{raw}' in year {year}, date {fields[dateIndex]}.");
            }
            rows.Add(new IceRow(date, values));
        }
        rows = rows.OrderBy(r => r.Date).ToList();
        if (rows.Count == 0) throw new InvalidDataException("CSV contains no valid dates.");
        return new IceData(years, rows);
    }

    public static Analysis Analyze(IceData data, int startYear)
    {
        int current = data.Years[^1];
        if (startYear < data.Years[0] || startYear >= current)
            throw new ArgumentException($"Start year must be between {data.Years[0]} and {current - 1}, leaving at least one historical year.");
        int[] history = data.Years.Where(y => y < current && y >= startYear).ToArray();
        if (history.Length == 0) throw new ArgumentException("No historical years are available for that start year.");
        var maxima = history.Append(current).ToDictionary(y => y,
            y => data.Rows.Select(r => r.Values[y]).Where(double.IsFinite).DefaultIfEmpty(double.NaN).Max());
        double[] average = data.Rows.Select(r =>
        {
            double[] values = history.Select(y => r.Values[y]).Where(double.IsFinite).ToArray();
            return values.Length == 0 ? double.NaN : values.Average();
        }).ToArray();
        int[] heavier = history.Where(y => maxima[y] > maxima[current]).OrderByDescending(y => maxima[y]).Take(5).ToArray();
        int[] lower = history.Where(y => maxima[y] < maxima[current]).OrderBy(y => maxima[y]).Take(5).ToArray();
        int[] lighter = lower.Length != 0 ? lower : history.OrderBy(y => double.IsNaN(maxima[y]) ? double.PositiveInfinity : maxima[y]).Take(5).ToArray();
        return new Analysis(startYear, current, history, history.TakeLast(5).ToArray(), heavier, lighter, lower.Length != 0, maxima, average);
    }

    private static string Description(string kind, Analysis a, bool french)
    {
        string range = french ? $"({a.StartYear} à {a.CurrentYear})" : $"({a.StartYear} to {a.CurrentYear})";
        return (kind, french) switch
        {
            ("Recent", false) => "Most recent Years\n" + range,
            ("Recent", true) => "Années les plus récentes\n" + range,
            ("Heavier", false) => "Years with Highest Max Coverage\n" + (a.Heavier.Length == 0 ? $"(None found in {a.StartYear} to {a.CurrentYear})" : range),
            ("Heavier", true) => "Années avec la couverture\nmaximale la plus élevée\n" + (a.Heavier.Length == 0 ? $"(Aucune trouvée de {a.StartYear} à {a.CurrentYear})" : range),
            ("Lighter", false) => "Years with Lowest Max Coverage\n" + (!a.HasLowerYears ? $"(Lowest in {a.StartYear} to {a.CurrentYear})" : range),
            _ => "Années avec la couverture\nmaximale la plus basse\n" + (!a.HasLowerYears ? $"(La plus basse de {a.StartYear} à {a.CurrentYear})" : range)
        };
    }
}

public sealed record Panel(int[] Years, string Description, string Suffix);

internal static class Renderer
{
    private static readonly DateTime FirstDate = new(1999, 11, 1);
    private static readonly DateTime LastDate = new(2000, 6, 30);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly string[] FrenchMonths = ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

    public static void Save(string path, string title, Panel[] panels, IceData data, Analysis analysis, bool french, bool combined)
    {
        using var image = new Bitmap(1800, combined ? 2700 : 1050);
        image.SetResolution(150, 150);
        using var g = Graphics.FromImage(image);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var titleFont = new Font("Arial", combined ? 32 : 29, FontStyle.Bold, GraphicsUnit.Pixel);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(title, titleFont, Brushes.Black, new RectangleF(20, 12, 1760, 65), centered);
        for (int i = 0; i < panels.Length; i++)
        {
            float top = combined ? 85 + i * 865 : 80;
            float height = combined ? 865 : 960;
            DrawPanel(g, new RectangleF(0, top, 1800, height), panels[i], data, analysis, french);
        }
        // Save through a stream so GDI+ never receives a Windows MAX_PATH-limited filename.
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        image.Save(stream, ImageFormat.Png);
    }

    private static void DrawPanel(Graphics g, RectangleF bounds, Panel panel, IceData data, Analysis a, bool french)
    {
        var plot = new RectangleF(140, bounds.Top + 28, 1610, bounds.Height - 170);
        using var labelFont = new Font("Arial", 22, FontStyle.Regular, GraphicsUnit.Pixel);
        using var axisFont = new Font("Arial", 24, FontStyle.Regular, GraphicsUnit.Pixel);
        using var legendFont = new Font("Arial", 20, FontStyle.Regular, GraphicsUnit.Pixel);
        using var descFont = new Font("Arial", 23, FontStyle.Bold, GraphicsUnit.Pixel);
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var grid = new Pen(Color.FromArgb(160, 190, 190, 190), 1) { DashStyle = DashStyle.Dot };
        float X(DateTime date) => plot.Left + (float)((date - FirstDate).TotalDays / (LastDate - FirstDate).TotalDays) * plot.Width;
        float Y(double value) => plot.Bottom - (float)value / 100 * plot.Height;
        for (int value = 0; value <= 100; value += 20)
        {
            float y = Y(value);
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            g.DrawString(value.ToString(Invariant), labelFont, Brushes.Black, new RectangleF(68, y - 18, 58, 36), right);
        }
        for (DateTime month = FirstDate; month <= LastDate; month = month.AddMonths(1))
        {
            foreach (int day in new[] { 1, 15 })
            {
                DateTime date = new(month.Year, month.Month, day);
                float x = X(date);
                g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                string label = french ? $"{FrenchMonths[date.Month - 1]}-{date.Day:00}" : date.ToString("MMM-dd", Invariant);
                var state = g.Save();
                g.TranslateTransform(x, plot.Bottom + 18);
                g.RotateTransform(55);
                g.DrawString(label, labelFont, Brushes.Black, 0, 0);
                g.Restore(state);
            }
        }
        var rotated = g.Save();
        g.TranslateTransform(32, plot.Top + plot.Height / 2);
        g.RotateTransform(-90);
        g.DrawString(french ? "Couverture de glace (%)" : "Ice Cover (%)", axisFont, Brushes.Black, 0, 0, center);
        g.Restore(rotated);

        var clipped = g.Save();
        g.SetClip(plot);
        void Line(IEnumerable<double> values, Color color, float width, bool dashed = false)
        {
            using var pen = new Pen(color, width) { DashStyle = dashed ? DashStyle.Dash : DashStyle.Solid, LineJoin = LineJoin.Round };
            PointF? previous = null;
            int i = 0;
            foreach (double value in values)
            {
                DateTime date = data.Rows[i++].Date;
                if (!double.IsFinite(value)) { previous = null; continue; }
                var point = new PointF(X(date), Y(value));
                if (previous.HasValue) g.DrawLine(pen, previous.Value, point);
                previous = point;
            }
        }
        foreach (int year in a.HistoryYears.Except(panel.Years))
            Line(data.Rows.Select(r => r.Values[year]), Color.FromArgb(77, 211, 211, 211), 1.2f);
        var legend = new List<(string Text, Color Color, float Width, bool Dashed)>();
        for (int i = 0; i < panel.Years.Length; i++)
        {
            int year = panel.Years[i];
            Color color = Palette(i, panel.Years.Length);
            Line(data.Rows.Select(r => r.Values[year]), Color.FromArgb(230, color), 2.25f);
            legend.Add(($"{year} (Max: {a.Maxima[year].ToString("F1", Invariant)}%)", color, 2.25f, false));
        }
        double avgMax = a.Average.Where(double.IsFinite).DefaultIfEmpty(double.NaN).Max();
        Line(a.Average, Color.Lime, 3, true);
        legend.Add(($"{(french ? "Moyenne" : "Average")} (Max: {avgMax.ToString("F1", Invariant)}%)", Color.Lime, 3, true));
        Line(data.Rows.Select(r => r.Values[a.CurrentYear]), Color.Red, 4.5f);
        legend.Add(($"{a.CurrentYear} (Max: {a.Maxima[a.CurrentYear].ToString("F1", Invariant)}%)", Color.Red, 4.5f, false));
        g.Restore(clipped);
        using var border = new Pen(Color.Gray, 1);
        g.DrawRectangle(border, plot.X, plot.Y, plot.Width, plot.Height);

        float legendWidth = legend.Max(entry => g.MeasureString(entry.Text, legendFont).Width) + 95;
        var legendBox = new RectangleF(plot.Left + 12, plot.Top + 12, legendWidth, legend.Count * 30 + 20);
        using var white = new SolidBrush(Color.FromArgb(235, Color.White));
        g.FillRectangle(white, legendBox);
        g.DrawRectangle(border, legendBox.X, legendBox.Y, legendBox.Width, legendBox.Height);
        for (int i = 0; i < legend.Count; i++)
        {
            var entry = legend[i];
            float y = legendBox.Top + 10 + i * 30;
            using var pen = new Pen(entry.Color, entry.Width) { DashStyle = entry.Dashed ? DashStyle.Dash : DashStyle.Solid };
            g.DrawLine(pen, legendBox.Left + 12, y + 13, legendBox.Left + 65, y + 13);
            g.DrawString(entry.Text, legendFont, Brushes.Black, legendBox.Left + 75, y);
        }
        SizeF descSize = g.MeasureString(panel.Description, descFont);
        var descBox = new RectangleF(plot.Right - descSize.Width - 40, plot.Top + 18, descSize.Width + 24, descSize.Height + 20);
        g.FillRectangle(white, descBox);
        g.DrawRectangle(border, descBox.X, descBox.Y, descBox.Width, descBox.Height);
        g.DrawString(panel.Description, descFont, Brushes.Black, descBox, center);
    }

    private static Color Palette(int index, int count)
    {
        // Evenly spaced hues keep the five comparison curves distinct.
        double hue = (0.97 + index / (double)Math.Max(1, count)) % 1 * 6;
        double x = 0.68 * (1 - Math.Abs(hue % 2 - 1));
        (double r, double g, double b) = hue switch
        {
            < 1 => (0.68, x, 0d), < 2 => (x, 0.68, 0d), < 3 => (0d, 0.68, x),
            < 4 => (0d, x, 0.68), < 5 => (x, 0d, 0.68), _ => (0.68, 0d, x)
        };
        return Color.FromArgb((int)((r + 0.18) * 255), (int)((g + 0.18) * 255), (int)((b + 0.18) * 255));
    }
}
