using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;

namespace AnomalyCharts
{
    public sealed class Region
    {
        public string Name, Folder, Csv;
        public Region(string name, string folder, string csv) { Name = name; Folder = folder; Csv = csv; }
        public override string ToString() { return Name; }
        public static readonly Region[] All = {
            new Region("Gulf of St Lawrence", "Gulf", "Gulf.csv"),
            new Region("NE Newfoundland", "NE Newfoundland", "weekly_NE-NFLND.csv"),
            new Region("South Labrador Coast", "South_Labrador_Coast", "weekly-SLab-Coast-1970-2025.csv"),
            new Region("Western Arctic", "Western Arctic", "Western_Arctic.csv"),
            new Region("Northern Canadian Waters", "Northern Canadian Waters", "Northern_CDN_Waters.csv")
        };
    }

    public sealed class Observation
    {
        public DateTime Date;
        public double Concentration;
        public double? Area;
    }
    public sealed class Dataset
    {
        public List<Observation> Rows = new List<Observation>();
        public int Skipped;
        public DateTime Latest { get { return Rows.Max(r => r.Date); } }
    }
    public sealed class Annual
    {
        public int Year;
        public double Concentration, Anomaly;
    }
    public sealed class Analysis
    {
        public List<Observation> Selected;
        public List<Annual> Years;
        public double Median;
        public DateTime Latest;
    }
    public sealed class RunResult
    {
        public string Chart, Summary;
        public List<string> Files = new List<string>();
        public Analysis Analysis;
    }

    public static class Engine
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static int Week(int day) { return Math.Min((day - 1) / 7 + 1, 4); }
        public static string Ordinal(int n) { return n.ToString(Inv) + (n % 100 >= 10 && n % 100 <= 20 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th"); }
        public static string F(double value, string format)
        {
            // Framework numeric formatting otherwise rounds midpoint values away
            // from zero, while Python uses ties-to-even for these display values.
            int digits;
            if (format.StartsWith("F", StringComparison.Ordinal) && int.TryParse(format.Substring(1), out digits))
                value = Math.Round(value, digits, MidpointRounding.ToEven);
            else if (format == "0.0") value = Math.Round(value, 1, MidpointRounding.ToEven);
            return value.ToString(format, Inv);
        }
        public static string Month(int month) { return Inv.DateTimeFormat.GetMonthName(month); }
        public static string Date(DateTime date) { return date.ToString("yyyy-MM-dd", Inv); }
        public static double Median(IEnumerable<double> values)
        {
            double[] sorted = values.OrderBy(v => v).ToArray();
            if (sorted.Length == 0) throw new ArgumentException("No values for median.");
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
        public static Dataset Read(string path)
        {
            var data = new Dataset();
            using (var csv = new TextFieldParser(path, Encoding.UTF8, true))
            {
                csv.TextFieldType = FieldType.Delimited;
                csv.SetDelimiters(",");
                csv.HasFieldsEnclosedInQuotes = true;
                csv.TrimWhiteSpace = true;
                if (csv.EndOfData) throw new ArgumentException("The CSV is empty.");
                string[] header = csv.ReadFields();
                int dateColumn = Array.IndexOf(header, "DATE"), concentrationColumn = Array.IndexOf(header, "Concentration"), areaColumn = Array.IndexOf(header, "Area");
                if (dateColumn < 0 || concentrationColumn < 0) throw new ArgumentException("CSV must contain DATE and Concentration columns.");
                while (!csv.EndOfData)
                {
                    string[] fields;
                    try { fields = csv.ReadFields(); }
                    catch (MalformedLineException ex) { throw new ArgumentException("Malformed CSV near line " + csv.ErrorLineNumber + ": " + ex.Message); }
                    DateTime date; double value;
                    if (fields == null || fields.Length <= Math.Max(dateColumn, concentrationColumn) ||
                        !DateTime.TryParseExact(fields[dateColumn], "yyyyMMdd", Inv, DateTimeStyles.None, out date) ||
                        !double.TryParse(fields[concentrationColumn], NumberStyles.Float, Inv, out value) || double.IsNaN(value) || double.IsInfinity(value))
                    { data.Skipped++; continue; }
                    if (value < 0 || value > 1) throw new ArgumentException("Concentration must be a fraction from 0 to 1, as in the original CSVs. Date: " + Date(date));
                    double area;
                    double? parsedArea = areaColumn >= 0 && fields.Length > areaColumn && double.TryParse(fields[areaColumn], NumberStyles.Float, Inv, out area) && !double.IsNaN(area) && !double.IsInfinity(area) && area > 0 ? (double?)area : null;
                    data.Rows.Add(new Observation { Date = date, Concentration = value, Area = parsedArea });
                }
            }
            if (data.Rows.Count == 0) throw new ArgumentException("CSV has no valid observations.");
            data.Rows = data.Rows.OrderBy(r => r.Date).ToList();
            return data;
        }
        public static Analysis Select(Dataset data, int startYear, int month, int week)
        {
            if (startYear < 1 || startYear > 9999 || month < 1 || month > 12 || week < 1 || week > 4)
                throw new ArgumentException("Year must be 1–9999, month 1–12, and week 1–4.");
            var selected = data.Rows.Where(r => r.Date.Year >= startYear && r.Date.Month == month && Week(r.Date.Day) == week).ToList();
            if (selected.Count == 0) throw new ArgumentException("No data for " + Ordinal(week) + " week of " + Month(month) + " from " + startYear + " onward.");
            var annual = selected.GroupBy(r => r.Date.Year).OrderBy(g => g.Key).Select(g => new Annual { Year = g.Key, Concentration = g.Average(r => r.Concentration) }).ToList();
            double median = Median(annual.Select(a => a.Concentration));
            foreach (var year in annual) year.Anomaly = (year.Concentration - median) * 100;
            return new Analysis { Selected = selected, Years = annual, Median = median, Latest = selected.Max(r => r.Date) };
        }
        public static RunResult Run(Dataset data, Region region, string csvPath, int startYear, int month, int week, string output, bool northernExtras)
        {
            var analysis = Select(data, startYear, month, week);
            if (northernExtras && region.Name == "Northern Canadian Waters" && !data.Rows[0].Area.HasValue)
                throw new ArgumentException("Northern seasonal overlays require a positive numeric Area in the first valid CSV row. Uncheck the additional Northern outputs to generate only the anomaly chart.");
            Directory.CreateDirectory(output);
            string stem = Ordinal(week) + "_Week_" + Month(month) + "_from_" + startYear + "_through_" + analysis.Latest.ToString("yyyyMMdd", Inv);
            var result = new RunResult { Chart = Path.Combine(output, "Ice_Anomaly_" + stem + ".png"), Summary = Path.Combine(output, "Summary_" + stem + ".txt"), Analysis = analysis };
            Renderer.Anomaly(analysis, region.Name, month, week, result.Chart);
            File.WriteAllText(result.Summary, Summary(data, analysis, region.Name, startYear, month, week), new UTF8Encoding(false));
            result.Files.Add(result.Chart); result.Files.Add(result.Summary);
            if (northernExtras && region.Name == "Northern Canadian Waters")
            {
                foreach (string path in Renderer.NorthernOverlays(data, startYear, csvPath, output)) result.Files.Add(path);
                foreach (string language in new[] { "EN", "FR" })
                {
                    string file = Path.Combine(output, "Climatology_" + Path.GetFileNameWithoutExtension(csvPath) + "_" + analysis.Latest.ToString("yyyyMMdd", Inv) + "_" + language + "_from_" + startYear + ".txt");
                    File.WriteAllText(file, Climatology(data, analysis, startYear, month, week, language == "FR"), new UTF8Encoding(false));
                    result.Files.Add(file);
                }
            }
            return result;
        }
        public static string Summary(Dataset data, Analysis a, string region, int startYear, int month, int week)
        {
            var text = new StringBuilder();
            text.AppendLine("=== Anomaly Summary for " + region + " ===").AppendLine();
            text.AppendLine("Week analyzed: " + Ordinal(week) + " Week " + Month(month));
            text.AppendLine("Week definition: days 1–7=1st, 8–14=2nd, 15–21=3rd, 22–31=4th");
            text.AppendLine("Requested start year: " + startYear);
            text.AppendLine("Latest data date in CSV: " + Date(data.Latest));
            text.AppendLine("Latest selected-week observation: " + Date(a.Latest));
            text.AppendLine("Years in analysis: " + a.Years.First().Year + " to " + a.Years.Last().Year);
            text.AppendLine("Number of years: " + a.Years.Count);
            text.AppendLine("Median concentration: " + F(a.Median * 100, "F2") + "%");
            text.AppendLine("Baseline: median of yearly mean concentrations for the selected month/week and years.");
            text.AppendLine("Anomalies are percentage-point differences from that median.");
            if (data.Skipped > 0) text.AppendLine("Invalid CSV rows skipped: " + data.Skipped);
            foreach (bool positive in new[] { true, false })
            {
                text.AppendLine().AppendLine(positive ? "Top 5 positive anomalies:" : "Top 5 negative anomalies:");
                var ranked = (positive ? a.Years.Where(y => y.Anomaly > 0).OrderByDescending(y => y.Anomaly) : a.Years.Where(y => y.Anomaly < 0).OrderBy(y => y.Anomaly)).Take(5).ToList();
                if (ranked.Count == 0) text.AppendLine("  None");
                foreach (var year in ranked) text.AppendLine("  " + year.Year + ": " + F(year.Concentration * 100, "F2") + "% (anomaly: " + (year.Anomaly >= 0 ? "+" : "") + F(year.Anomaly, "F1") + "%)");
            }
            return text.ToString();
        }
        public static string Climatology(Dataset data, Analysis a, int startYear, int month, int week, bool french)
        {
            double area = data.Rows[0].Area.Value;
            var latest = a.Years.Last();
            double median = a.Median * 100, average = a.Years.Average(y => y.Concentration) * 100, value = latest.Concentration * 100;
            int rank = 1 + a.Years.Count(y => y.Concentration < latest.Concentration);
            var text = new StringBuilder();
            text.AppendLine(french ? "Résumé de climatologie hebdomadaire pour les eaux canadiennes du Nord" : "Weekly Climatology Summary for Northern Canadian Waters");
            text.AppendLine("Analysis / Analyse: " + startYear + " - " + Date(data.Latest)).AppendLine();
            text.AppendLine((french ? "Dernière date observée : " : "Latest date observed: ") + Date(a.Latest));
            text.AppendLine(french ? "Définition de la semaine : jours 1–7=1ère, 8–14=2e, 15–21=3e, 22–31=4e" : "Week definition: days 1–7=1st, 8–14=2nd, 15–21=3rd, 22–31=4th");
            string period = french ? week + (week == 1 ? "ère" : "e") + " semaine de " + CultureInfo.GetCultureInfo("fr-CA").DateTimeFormat.GetMonthName(month) : Ordinal(week) + " week of " + Month(month);
            text.AppendLine((french ? "Période sélectionnée : " : "Selected period: ") + period);
            text.AppendLine((french ? "Ensemble de comparaison : années depuis " : "Comparison set: years from ") + startYear);
            text.AppendLine((french ? "Nombre d'années en comparaison : " : "Number of years in comparison: ") + a.Years.Count);
            text.AppendLine((french ? "Valeur de l'année la plus récente " : "Latest year's value ") + "(" + latest.Year + "): " + F(value, "F2") + "%");
            text.AppendLine((french ? "Classement (par le plus bas) : " : "Rank (by lowest): ") + rank + (french ? " sur " : " of ") + a.Years.Count);
            text.AppendLine((french ? "Médiane : " : "Median: ") + F(median, "F2") + "%");
            text.AppendLine((french ? "Moyenne : " : "Average: ") + F(average, "F2") + "%");
            text.AppendLine((french ? "Superficie de glace couverte la plus récente : " : "Latest ice area covered: ") + F(value * area / 100, "F2") + " km²");
            text.AppendLine((french ? "Superficie de glace médiane de la semaine : " : "Median week ice area: ") + F(median * area / 100, "F2") + " km²");
            text.AppendLine((french ? "Différence par rapport à la médiane : " : "Difference from median: ") + F((value - median) * area / 100, "F2") + " km²");
            text.AppendLine((french ? "Superficie de glace moyenne de la semaine : " : "Average week ice area: ") + F(average * area / 100, "F2") + " km²");
            text.AppendLine((french ? "Différence par rapport à la moyenne : " : "Difference from average: ") + F((value - average) * area / 100, "F2") + " km²");
            foreach (bool lowest in new[] { true, false })
            {
                text.AppendLine().AppendLine(french ? (lowest ? "Sept années les plus basses (cette semaine) :" : "Sept années les plus hautes (cette semaine) :") : (lowest ? "Seven lowest years (this week):" : "Seven highest years (this week):"));
                foreach (var year in (lowest ? a.Years.OrderBy(y => y.Concentration) : a.Years.OrderByDescending(y => y.Concentration)).Take(7))
                    text.AppendLine("  - " + year.Year + ": " + F(year.Concentration * 100, "F2") + "% | " + F(year.Concentration * area, "F2") + " km²");
            }
            return text.ToString();
        }
    }

    public static class Renderer
    {
        static readonly Color Blue = Color.FromArgb(65, 85, 220), Red = Color.FromArgb(225, 65, 65);
        static Font Font(float size, bool bold) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        static void Text(Graphics g, string text, float x, float y, float size, Color color, bool bold, StringAlignment align)
        {
            using (var font = Font(size, bold)) using (var brush = new SolidBrush(color)) using (var format = new StringFormat { Alignment = align })
                g.DrawString(text, font, brush, new PointF(x, y), format);
        }
        static void Setup(Graphics g) { g.Clear(Color.White); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; }
        static void Line(Graphics g, Color color, float width, float x1, float y1, float x2, float y2)
        { using (var pen = new Pen(color, width)) g.DrawLine(pen, x1, y1, x2, y2); }
        static void Legend(Graphics g, float x, float y, Color color, string label, bool dashed)
        {
            using (var pen = new Pen(color, 6)) { if (dashed) pen.DashStyle = DashStyle.Dash; g.DrawLine(pen, x, y + 17, x + 55, y + 17); }
            Text(g, label, x + 70, y, 27, Color.FromArgb(40, 45, 55), false, StringAlignment.Near);
        }
        public static void Anomaly(Analysis a, string region, int month, int week, string output)
        {
            using (var bitmap = new Bitmap(2800, 1400))
            {
                bitmap.SetResolution(200, 200);
                using (var g = Graphics.FromImage(bitmap))
                {
                    Setup(g);
                    Text(g, region + " Sea Ice Extent Anomalies", 1400, 25, 43, Color.Black, true, StringAlignment.Center);
                    Text(g, Engine.Ordinal(week) + " Week " + Engine.Month(month) + "  |  " + a.Years.First().Year + "–" + a.Years.Last().Year, 1400, 85, 31, Color.DimGray, false, StringAlignment.Center);
                    Legend(g, 190, 153, Blue, "Positive Anomaly", false);
                    Legend(g, 600, 153, Red, "Negative Anomaly", false);
                    Legend(g, 1035, 153, Color.Black, "Median", false);
                    var latest = a.Years.Last();
                    Text(g, "Median concentration: " + Engine.F(a.Median * 100, "F1") + "%    Latest year (" + latest.Year + "): " + Engine.F(latest.Concentration * 100, "F1") + "%", 2645, 155, 27, Color.Black, false, StringAlignment.Far);
                    float left = 185, top = 265, width = 2460, height = 930;
                    double lo = a.Years.Min(y => y.Anomaly), hi = a.Years.Max(y => y.Anomaly), span = Math.Max(1, hi - lo);
                    double min = Math.Min(-1, lo - Math.Max(1, span * .08)), max = Math.Max(1, hi + Math.Max(1, span * .12));
                    Func<double, float> yPos = v => top + height - (float)((v - min) / (max - min) * height);
                    int first = a.Years.First().Year, last = a.Years.Last().Year;
                    Func<double, float> xPos = y => left + (float)((y - first + 1) / (last - first + 2) * width);
                    for (int tick = 0; tick <= 8; tick++)
                    {
                        double value = min + (max - min) * tick / 8;
                        float py = yPos(value);
                        Line(g, Color.FromArgb(225, 229, 234), 2, left, py, left + width, py);
                        Text(g, Engine.F(value, "0.0"), left - 20, py - 18, 26, Color.DimGray, false, StringAlignment.Far);
                    }
                    float zero = yPos(0), barWidth = Math.Min(90, width / (last - first + 2) * .78f);
                    foreach (var annual in a.Years)
                    {
                        float x = xPos(annual.Year), y = yPos(annual.Anomaly);
                        var rect = new RectangleF(x - barWidth / 2, Math.Min(y, zero), barWidth, Math.Max(1, Math.Abs(y - zero)));
                        Color color = annual.Anomaly >= 0 ? Blue : Red;
                        using (var brush = new SolidBrush(color)) g.FillRectangle(brush, rect);
                        using (var pen = new Pen(Color.FromArgb(60, 65, 75), 1)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                        float labelSize = Math.Min(23, Math.Max(12, barWidth * .65f));
                        Text(g, Engine.F(annual.Anomaly, "F1"), x, annual.Anomaly >= 0 ? y - labelSize - 7 : y + 6, labelSize, color, false, StringAlignment.Center);
                    }
                    Line(g, Color.Black, 3, left, zero, left + width, zero);
                    Line(g, Color.Black, 2, left, top, left, top + height);
                    Line(g, Color.Black, 2, left, top + height, left + width, top + height);
                    for (int index = 0; index < a.Years.Count; index += a.Years.Count > 40 ? 3 : 2)
                    {
                        float x = xPos(a.Years[index].Year);
                        Line(g, Color.Black, 2, x, top + height, x, top + height + 12);
                        var saved = g.Save(); g.TranslateTransform(x - 5, top + height + 28); g.RotateTransform(-45);
                        Text(g, a.Years[index].Year.ToString(), 0, 0, 26, Color.Black, false, StringAlignment.Far); g.Restore(saved);
                    }
                    var state = g.Save(); g.TranslateTransform(40, top + height / 2); g.RotateTransform(-90);
                    Text(g, "Anomaly %", 0, 0, 32, Color.Black, true, StringAlignment.Center); g.Restore(state);
                    Text(g, "Anomalies are percentage-point differences from the median of the selected years.", 1400, 1350, 23, Color.DimGray, false, StringAlignment.Center);
                }
                bitmap.Save(output, ImageFormat.Png);
            }
        }
        sealed class Point { public double X, Y; }
        static void DrawSeries(Graphics g, List<Point> points, Color color, float thickness, bool dashed, Func<double, float> x, Func<double, float> y)
        {
            using (var pen = new Pen(color, thickness))
            {
                if (dashed) pen.DashStyle = DashStyle.Dash;
                if (points.Count > 1) g.DrawLines(pen, points.OrderBy(p => p.X).Select(p => new PointF(x(p.X), y(p.Y))).ToArray());
                else if (points.Count == 1) using (var brush = new SolidBrush(color)) g.FillEllipse(brush, x(points[0].X) - 4, y(points[0].Y) - 4, 8, 8);
            }
        }
        public static List<string> NorthernOverlays(Dataset data, int year, string csvPath, string output)
        {
            var paths = new List<string>();
            string[] suffixes = { "", "_Jul-Dec", "_Oct-Dec", "_Oct-Jan" };
            string[] labels = { "April–December", "July–December", "October–December", "October–January" };
            int[] starts = { 4, 7, 10, 10 };
            for (int window = 0; window < 4; window++)
            {
                bool wrap = window == 3;
                int start = starts[window];
                var rows = data.Rows.Where(r => r.Date.Year >= year && (r.Date.Month >= start || (wrap && r.Date.Month == 1))).ToList();
                if (rows.Count == 0) continue;
                string file = Path.Combine(output, "Ice_Concentration_" + Path.GetFileNameWithoutExtension(csvPath) + suffixes[window] + "_from_" + year + "_through_" + data.Latest.ToString("yyyyMMdd", Engine.Inv) + ".png");
                Overlay(rows, start, wrap, data.Rows[0].Area.Value, year, data.Latest.Year, labels[window], file);
                paths.Add(file);
            }
            return paths;
        }
        static void Overlay(List<Observation> rows, int startMonth, bool wrap, double area, int startYear, int endYear, string label, string output)
        {
            Func<Observation, int> season = r => wrap && r.Date.Month == 1 ? r.Date.Year - 1 : r.Date.Year;
            // Match Python's original DayOfYear and October–January +365 convention.
            Func<Observation, int> day = r => r.Date.DayOfYear + (wrap && r.Date.Month == 1 ? 365 : 0);
            var seasons = rows.GroupBy(season).OrderBy(s => s.Key).ToList();
            int latestSeason = seasons.Last().Key;
            Color[] recentColors = { Color.Blue, Color.Purple, Color.DarkOrange, Color.Brown, Color.DarkCyan };
            using (var bitmap = new Bitmap(2800, 1600))
            {
                bitmap.SetResolution(200, 200);
                using (var g = Graphics.FromImage(bitmap))
                {
                    Setup(g);
                    Text(g, "Northern Canadian Waters — Eaux du nord canadien", 1400, 25, 43, Color.Black, true, StringAlignment.Center);
                    Text(g, label + "  |  " + startYear + "–" + endYear, 1400, 90, 32, Color.DimGray, false, StringAlignment.Center);
                    float left = 195, top = 270, width = 2390, height = 1120;
                    int minDay = new DateTime(2001, startMonth, 1).DayOfYear, maxDay = wrap ? 396 : 366;
                    Func<double, float> x = v => left + (float)((v - minDay) / (maxDay - minDay) * width);
                    Func<double, float> y = v => top + height - (float)(v / 100 * height);
                    for (int i = 0; i <= 10; i++)
                    {
                        Line(g, Color.FromArgb(227, 231, 235), 2, left, y(i * 10), left + width, y(i * 10));
                        Text(g, (i * 10).ToString(), left - 20, y(i * 10) - 18, 27, Color.Black, false, StringAlignment.Far);
                        Text(g, Engine.F(i / 10.0 * area / 1e6, "F2"), left + width + 20, y(i * 10) - 18, 27, Color.DarkGreen, false, StringAlignment.Near);
                    }
                    float legendX = left;
                    Legend(g, legendX, 165, Color.LightGray, "Older years", false); legendX += 355;
                    foreach (var group in seasons)
                    {
                        bool current = group.Key == latestSeason, recent = group.Key >= latestSeason - 5;
                        Color color = current ? Color.Red : recent ? recentColors[group.Key - (latestSeason - 5)] : Color.FromArgb(90, Color.LightGray);
                        DrawSeries(g, group.Select(r => new Point { X = day(r), Y = r.Concentration * 100 }).ToList(), color, current ? 5 : recent ? 4 : 2, false, x, y);
                        if (recent) { Legend(g, legendX, 165, color, group.Key.ToString(), false); legendX += 240; }
                    }
                    var medians = rows.GroupBy(day).OrderBy(s => s.Key).Select(s => new Point { X = s.Key, Y = Engine.Median(s.Select(r => r.Concentration)) * 100 }).ToList();
                    DrawSeries(g, medians, Color.Black, 5, true, x, y);
                    Legend(g, legendX, 165, Color.Black, "Median", true);
                    Line(g, Color.Black, 2, left, top, left, top + height);
                    Line(g, Color.Black, 2, left, top + height, left + width, top + height);
                    for (int month = startMonth; month <= (wrap ? 13 : 12); month++)
                    {
                        int actual = month == 13 ? 1 : month;
                        foreach (int date in new[] { 7, 15, 21 })
                        {
                            double position = new DateTime(2001, actual, date).DayOfYear + (month == 13 ? 365 : 0);
                            float px = x(position);
                            Line(g, Color.Black, 2, px, top + height, px, top + height + 12);
                            Text(g, date == 15 ? Engine.Inv.DateTimeFormat.GetAbbreviatedMonthName(actual) : date.ToString(), px, top + height + 25, date == 15 ? 29 : 23, date == 15 ? Color.Black : Color.Gray, date == 15, StringAlignment.Center);
                        }
                    }
                    var state = g.Save(); g.TranslateTransform(42, top + height / 2); g.RotateTransform(-90);
                    Text(g, "Ice Coverage (%) / Couverture de glace", 0, 0, 29, Color.Black, true, StringAlignment.Center); g.Restore(state);
                    state = g.Save(); g.TranslateTransform(2760, top + height / 2); g.RotateTransform(-90);
                    Text(g, "Ice area / Superficie de glace (×10⁶ km²)", 0, 0, 29, Color.DarkGreen, true, StringAlignment.Center); g.Restore(state);
                    Text(g, "Median is calculated by day of year across the selected years, matching the Python overlays.", 1400, 1530, 24, Color.DimGray, false, StringAlignment.Center);
                }
                bitmap.Save(output, ImageFormat.Png);
            }
        }
    }

    public sealed class MainForm : Form
    {
        readonly ComboBox region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly TextBox csv = new TextBox { Dock = DockStyle.Fill }, output = new TextBox { Dock = DockStyle.Fill };
        readonly NumericUpDown year = new NumericUpDown { Minimum = 1, Maximum = 9999, Dock = DockStyle.Fill };
        readonly ComboBox month = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }, week = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly CheckBox extras = new CheckBox { Text = "Include Northern seasonal charts and English/French summaries", AutoSize = true };
        readonly Button generate = new Button { Text = "Generate charts and summary", AutoSize = true, Padding = new Padding(16, 6, 16, 6) }, open = new Button { Text = "Open output folder", AutoSize = true, Enabled = false };
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(1000, 0), Text = "Choose a region and CSV file to begin.", Padding = new Padding(0, 8, 0, 8) };
        readonly PictureBox preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        readonly TextBox summary = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
        readonly ComboBox chartList = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly Panel inputs = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20) };
        string lastOutput;
        public MainForm()
        {
            Text = "Regional Anomaly Charts"; Width = 1180; Height = 920; MinimumSize = new Size(850, 700);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            var heading = new Label { Text = "Regional Anomaly Charts", Font = new Font("Segoe UI", 21, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            layout.Controls.Add(heading, 0, 0); layout.SetColumnSpan(heading, 3);
            var description = new Label { Text = "Compare one month/week across years, from your chosen start year through the latest available data.", AutoSize = true, Margin = new Padding(0, 0, 0, 15) };
            layout.Controls.Add(description, 0, 1); layout.SetColumnSpan(description, 3);
            AddRow(layout, 2, "Region", region, null);
            var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill }; browse.Click += delegate { BrowseCsv(); };
            AddRow(layout, 3, "CSV data file", csv, browse);
            var useFile = new Button { Text = "Load CSV", Dock = DockStyle.Fill }; useFile.Click += delegate { LoadCsvDefaults(); };
            var dates = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 6 };
            dates.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); dates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); dates.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); dates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); dates.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); dates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            dates.Controls.Add(new Label { Text = "Year", AutoSize = true }, 0, 0); dates.Controls.Add(year, 1, 0);
            dates.Controls.Add(new Label { Text = "Month", AutoSize = true }, 2, 0); dates.Controls.Add(month, 3, 0);
            dates.Controls.Add(new Label { Text = "Week", AutoSize = true }, 4, 0); dates.Controls.Add(week, 5, 0);
            AddRow(layout, 4, "Comparison", dates, useFile);
            var browseOutput = new Button { Text = "Browse…", Dock = DockStyle.Fill }; browseOutput.Click += delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose the output folder", SelectedPath = output.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) output.Text = dialog.SelectedPath; };
            AddRow(layout, 5, "Output folder", output, browseOutput);
            layout.Controls.Add(extras, 1, 6); layout.SetColumnSpan(extras, 2);
            var explanation = new Label { Text = "Weeks: 1 = days 1–7   •   2 = 8–14   •   3 = 15–21   •   4 = 22–31\nThe median uses the selected years. Missing years are omitted; no end date is needed.", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 10, 0, 10) };
            layout.Controls.Add(explanation, 1, 7); layout.SetColumnSpan(explanation, 2);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            actions.Controls.Add(generate); actions.Controls.Add(open);
            layout.Controls.Add(actions, 1, 8); layout.SetColumnSpan(actions, 2);
            layout.Controls.Add(status, 0, 9); layout.SetColumnSpan(status, 3);
            inputs.Controls.Add(layout);
            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
            var chartTab = new TabPage("Charts"); chartTab.Controls.Add(preview); chartTab.Controls.Add(chartList); tabs.TabPages.Add(chartTab);
            var textTab = new TabPage("Anomaly summary"); textTab.Controls.Add(summary); tabs.TabPages.Add(textTab);
            Controls.Add(tabs); Controls.Add(inputs);
            for (int i = 1; i <= 12; i++) month.Items.Add(Engine.Month(i));
            week.Items.AddRange(new object[] { "1 (days 1–7)", "2 (days 8–14)", "3 (days 15–21)", "4 (days 22–31)" });
            month.SelectedIndex = DateTime.Today.Month - 1; week.SelectedIndex = 0; year.Value = 1991;
            region.Items.AddRange(AnomalyCharts.Region.All);
            region.SelectedIndexChanged += delegate { SelectRegion(); };
            generate.Click += async delegate { await Generate(); };
            open.Click += delegate { if (Directory.Exists(lastOutput)) System.Diagnostics.Process.Start("explorer.exe", lastOutput); };
            chartList.SelectedIndexChanged += delegate { if (chartList.SelectedItem != null) ShowChart((string)chartList.SelectedItem); };
            csv.Leave += delegate { string path = csv.Text.Trim().Trim('"'); if (File.Exists(path)) output.Text = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "output"); };
            region.SelectedIndex = 0;
        }
        static void AddRow(TableLayoutPanel layout, int row, string label, Control control, Control button)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 10, 8) }, 0, row);
            control.Margin = new Padding(0, 5, 10, 5); layout.Controls.Add(control, 1, row);
            if (button != null) layout.Controls.Add(button, 2, row); else layout.SetColumnSpan(control, 2);
        }
        void SelectRegion()
        {
            var selected = (Region)region.SelectedItem;
            string dataRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            csv.Text = Path.Combine(dataRoot, selected.Folder, selected.Csv);
            output.Text = Path.Combine(dataRoot, selected.Folder, "output");
            extras.Enabled = selected.Name == "Northern Canadian Waters"; extras.Checked = extras.Enabled;
            if (File.Exists(csv.Text)) LoadCsvDefaults(); else status.Text = "Browse to the CSV file for " + selected.Name + ".";
        }
        void BrowseCsv()
        {
            using (var dialog = new OpenFileDialog { Filter = "CSV data files (*.csv)|*.csv|All files (*.*)|*.*", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                csv.Text = dialog.FileName; output.Text = Path.Combine(Path.GetDirectoryName(dialog.FileName), "output"); LoadCsvDefaults();
            }
        }
        void LoadCsvDefaults()
        {
            try
            {
                var data = Engine.Read(csv.Text.Trim().Trim('"'));
                year.Value = data.Rows.Min(r => r.Date.Year); month.SelectedIndex = data.Latest.Month - 1; week.SelectedIndex = Engine.Week(data.Latest.Day) - 1;
                status.Text = "Data available: " + Engine.Date(data.Rows[0].Date) + " to " + Engine.Date(data.Latest) + ". " + data.Rows.Count + " observations." + (data.Skipped > 0 ? " Invalid rows skipped: " + data.Skipped : "");
                status.ForeColor = Color.FromArgb(35, 75, 105);
            }
            catch (Exception ex) { status.Text = ex.Message; status.ForeColor = Color.Firebrick; }
        }
        async Task Generate()
        {
            string csvPath = csv.Text.Trim().Trim('"'), destination = output.Text.Trim().Trim('"');
            if (String.IsNullOrWhiteSpace(destination)) { MessageBox.Show(this, "Choose an output folder.", "Output folder required"); return; }
            int start = (int)year.Value, m = month.SelectedIndex + 1, w = week.SelectedIndex + 1;
            var selected = (Region)region.SelectedItem; bool include = extras.Checked;
            inputs.Enabled = false; status.ForeColor = Color.DimGray; status.Text = "Generating charts and summary…"; UseWaitCursor = true;
            try
            {
                var result = await Task.Run(() => Engine.Run(Engine.Read(csvPath), selected, csvPath, start, m, w, destination, include));
                summary.Text = File.ReadAllText(result.Summary, Encoding.UTF8);
                chartList.Items.Clear(); foreach (string path in result.Files.Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))) chartList.Items.Add(path);
                chartList.SelectedIndex = 0; lastOutput = Path.GetFullPath(destination); open.Enabled = true;
                status.Text = "Saved " + result.Files.Count + " files to " + lastOutput + ". Latest selected-week data: " + Engine.Date(result.Analysis.Latest) + ".";
                status.ForeColor = Color.FromArgb(20, 110, 60);
            }
            catch (Exception ex)
            {
                status.Text = "Unable to complete: " + ex.Message; status.ForeColor = Color.Firebrick;
                MessageBox.Show(this, ex.Message, "Unable to generate charts", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { inputs.Enabled = true; UseWaitCursor = false; }
        }
        void ShowChart(string path)
        {
            Image next;
            using (var image = Image.FromFile(path)) next = new Bitmap(image);
            Image previous = preview.Image; preview.Image = next; if (previous != null) previous.Dispose();
        }
        internal Task PreviewRun(string regionName, string csvPath, int start, int selectedMonth, int selectedWeek, string destination)
        {
            region.SelectedIndex = Array.FindIndex(AnomalyCharts.Region.All, r => r.Name == regionName);
            csv.Text = csvPath; output.Text = destination; year.Value = start;
            month.SelectedIndex = selectedMonth - 1; week.SelectedIndex = selectedWeek - 1;
            return Generate();
        }
        protected override void Dispose(bool disposing)
        { if (disposing && preview.Image != null) { preview.Image.Dispose(); preview.Image = null; } base.Dispose(disposing); }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0)
            {
                // Batch mode supports automated verification without opening a window.
                string report = null;
                try
                {
                    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < args.Length; i += 2)
                    {
                        if (i + 1 >= args.Length) throw new ArgumentException("Every command-line option requires a value.");
                        options.Add(args[i], args[i + 1]);
                    }
                    if (options.ContainsKey("--report")) report = options["--report"];
                    if (options.ContainsKey("--ui-preview"))
                    {
                        using (var form = new MainForm())
                        {
                            form.Show(); Application.DoEvents();
                            if (options.ContainsKey("--csv"))
                            {
                                Task run = form.PreviewRun(options["--region"], options["--csv"], int.Parse(options["--start-year"], Engine.Inv), int.Parse(options["--month"], Engine.Inv), int.Parse(options["--week"], Engine.Inv), options["--output"]);
                                while (!run.IsCompleted) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                                run.GetAwaiter().GetResult(); Application.DoEvents();
                            }
                            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(options["--ui-preview"], ImageFormat.Png); }
                            form.Close();
                        }
                        return 0;
                    }
                    Region selected = Region.All.FirstOrDefault(r => r.Name == options["--region"] || r.Folder == options["--region"]);
                    if (selected == null) throw new ArgumentException("Unknown region.");
                    var data = Engine.Read(options["--csv"]);
                    var result = Engine.Run(data, selected, options["--csv"], int.Parse(options["--start-year"], Engine.Inv), int.Parse(options["--month"], Engine.Inv), int.Parse(options["--week"], Engine.Inv), options["--output"], options.ContainsKey("--northern-extras") && bool.Parse(options["--northern-extras"]));
                    if (report != null) File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { success = true, median = result.Analysis.Median, latest = Engine.Date(result.Analysis.Latest), years = result.Analysis.Years, files = result.Files, skippedRows = data.Skipped }), Encoding.UTF8);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (report != null) File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { success = false, error = ex.Message }), Encoding.UTF8);
                    return 1;
                }
            }
            Application.Run(new MainForm()); return 0;
        }
    }
}
