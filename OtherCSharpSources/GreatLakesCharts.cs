// Build with build.ps1. Windows .NET Framework; no third-party dependencies.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace GreatLakesCharts
{
    internal sealed class Lake
    {
        public string Name, Title;
        public string[] Files;
        public Lake(string name, string title, params string[] files)
        { Name = name; Title = title; Files = files; }
    }

    internal sealed class Observation
    {
        public DateTime Date;
        public Dictionary<string, double?> Values = new Dictionary<string, double?>();
    }

    internal sealed class SeasonData
    {
        public Lake Lake;
        public int Latest;
        public List<Observation> Rows;
    }

    internal static class Program
    {
        internal static readonly DateTime Start = new DateTime(1999, 11, 1);
        internal static readonly DateTime End = new DateTime(2000, 6, 21);
        internal static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        internal static readonly Lake[] Lakes = {
            new Lake("Lake Erie", "Lake Erie Ice Cover\nCouverture de glace du lac Érié", "eri_with_avg.csv"),
            new Lake("Lake Ontario", "Lake Ontario Ice Cover\nCouverture de glace du lac Ontario", "ont_with_avg.csv", "ontr_with_avg.csv"),
            new Lake("Lake Michigan", "Lake Michigan Ice Cover\nCouverture de glace du lac Michigan", "mic_with_avg.csv"),
            new Lake("Lake Huron", "Lake Huron and Georgian Bay Ice Cover\nCouverture de glace du lac Huron et de la baie Georgienne", "hur_with_avg.csv"),
            new Lake("Lake Superior", "Lake Superior Ice Cover\nCouverture de glace du lac Supérieur", "sup_with_avg.csv", "supr_with_avg.csv"),
            new Lake("Great Lakes", "Great Lakes Ice Cover\nCouverture de glace des Grands Lacs", "grtlks_with_avg.csv")
        };

        [STAThread]
        private static int Main(string[] args)
        {
            bool quiet = args.Contains("--quiet");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            string outputDir = Path.Combine(baseDir, "output");
            int? year = null;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string option = args[i];
                    if (option == "--quiet") continue;
                    if (option == "--help" || option == "-h")
                    {
                        Notify("GreatLakesCharts.exe [--data-dir PATH] [--output-dir PATH] [--year YEAR] [--quiet]\n\n" +
                            "Defaults: Data and output beside the executable; latest populated year per CSV.\n" +
                            "--quiet suppresses message boxes (exit code 0 = success, 1 = error).", quiet, false);
                        return 0;
                    }
                    if (option != "--data-dir" && option != "--output-dir" && option != "--year")
                        throw new ArgumentException("Unknown option: " + option);
                    if (++i >= args.Length) throw new ArgumentException("Missing value for " + option);
                    if (option == "--data-dir") dataDir = Path.GetFullPath(args[i]);
                    else if (option == "--output-dir") outputDir = Path.GetFullPath(args[i]);
                    else
                    {
                        int parsed;
                        if (!int.TryParse(args[i], NumberStyles.None, Culture, out parsed) || parsed < 1000 || parsed > 9999)
                            throw new ArgumentException("--year must be a four-digit year.");
                        year = parsed;
                    }
                }
                if (!Directory.Exists(dataDir)) throw new IOException("Data folder does not exist: " + dataDir);

                // Validate all six inputs before creating any chart.
                var prepared = new List<SeasonData>();
                var errors = new List<string>();
                foreach (Lake lake in Lakes)
                {
                    try { prepared.Add(Load(lake, FindCsv(dataDir, lake.Files), year)); }
                    catch (Exception ex) { errors.Add(lake.Name + ": " + ex.Message); }
                }
                if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));

                foreach (SeasonData season in prepared)
                {
                    string folder = Path.Combine(outputDir, season.Lake.Name);
                    Directory.CreateDirectory(folder);
                    foreach (bool two in new[] { false, true })
                    {
                        string filename = two
                            ? string.Format(Culture, "two_seasons_{0}_{1}.png", season.Latest - 1, season.Latest)
                            : string.Format(Culture, "single_season_{0}.png", season.Latest);
                        string destination = Path.Combine(folder, filename);
                        ChartRenderer.Save(season, two, destination);
                        Console.WriteLine("Saved: " + destination);
                    }
                }
                Notify("Done: 12 charts saved in:\n" + Path.GetFullPath(outputDir), quiet, false);
                return 0;
            }
            catch (Exception ex)
            {
                Notify("Unable to finish generating charts.\n\n" + ex.Message, quiet, true);
                return 1;
            }
        }

        private static void Notify(string message, bool quiet, bool error)
        {
            if (error) Console.Error.WriteLine(message); else Console.WriteLine(message);
            if (!quiet) MessageBox.Show(message, "Great Lakes Ice Cover", MessageBoxButtons.OK,
                error ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        }

        internal static string FindCsv(string folder, string[] names)
        {
            string[] files = Directory.GetFiles(folder);
            foreach (string name in names)
            {
                string file = files.FirstOrDefault(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase));
                if (file != null) return file;
            }
            throw new FileNotFoundException("Missing CSV: expected " + string.Join(" or ", names));
        }

        internal static SeasonData Load(Lake lake, string file, int? requestedYear)
        {
            var rows = new List<Observation>();
            var dates = new HashSet<DateTime>();
            List<string> years;
            using (var csv = new TextFieldParser(file, Encoding.UTF8, true))
            {
                csv.TextFieldType = FieldType.Delimited;
                csv.SetDelimiters(",");
                csv.HasFieldsEnclosedInQuotes = true;
                csv.TrimWhiteSpace = true;
                if (csv.EndOfData) throw new InvalidDataException("CSV is empty.");
                string[] header = csv.ReadFields().Select(h => h.Trim()).ToArray();
                if (header.Distinct().Count() != header.Length) throw new InvalidDataException("Duplicate CSV column names.");
                int dateIndex = Array.IndexOf(header, "Date");
                if (dateIndex < 0 || !header.Contains("Average")) throw new InvalidDataException("CSV must include Date and Average columns.");
                years = header.Where(h => h.Length == 4 && h.All(c => c >= '0' && c <= '9')).ToList();
                if (years.Count == 0) throw new InvalidDataException("CSV has no four-digit year columns.");
                var valueColumns = years.Concat(new[] { "Average" }).ToDictionary(h => h, h => Array.IndexOf(header, h));
                while (!csv.EndOfData)
                {
                    string[] fields = csv.ReadFields();
                    if (fields.Length != header.Length) throw new InvalidDataException("CSV row has the wrong number of fields near line " + csv.LineNumber + ".");
                    DateTime date;
                    if (!DateTime.TryParseExact(fields[dateIndex].Trim() + "-2000", "MMM-dd-yyyy", Culture, DateTimeStyles.None, out date))
                        throw new InvalidDataException("Invalid month/day: " + fields[dateIndex]);
                    if (date.Month >= 11) date = date.AddYears(-1);
                    if (!dates.Add(date)) throw new InvalidDataException("CSV contains duplicate month/day rows.");
                    var row = new Observation { Date = date };
                    foreach (var column in valueColumns)
                    {
                        string text = fields[column.Value].Trim();
                        double number;
                        // Common pandas CSV missing-value markers remain gaps.
                        if (text == "" || text.Equals("NaN", StringComparison.OrdinalIgnoreCase) ||
                            text == "NA" || text == "N/A" || text.Equals("null", StringComparison.OrdinalIgnoreCase) || text == "<NA>")
                            row.Values[column.Key] = null;
                        else if (!double.TryParse(text, NumberStyles.Float, Culture, out number) ||
                                 double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > 100)
                            throw new InvalidDataException("Column " + column.Key + " contains invalid ice cover: " + text + ". Expected 0–100%.");
                        else row.Values[column.Key] = number;
                    }
                    if (date >= Start && date <= End) rows.Add(row);
                }
            }
            rows = rows.OrderBy(r => r.Date).ToList();
            var populated = years.Where(y => rows.Any(r => r.Values[y].HasValue)).Select(y => int.Parse(y, Culture)).OrderBy(y => y).ToList();
            if (populated.Count == 0) throw new InvalidDataException("No seasonal data within November 1–June 21.");
            int latest = requestedYear ?? populated.Last();
            foreach (int y in new[] { latest - 1, latest })
                if (!populated.Contains(y)) throw new InvalidDataException("No data for " + y + "; both " + (latest - 1) + " and " + latest + " are required.");
            if (!rows.Any(r => r.Values["Average"].HasValue)) throw new InvalidDataException("Average has no values within November 1–June 21.");
            return new SeasonData { Lake = lake, Latest = latest, Rows = rows };
        }
    }

    internal static class ChartRenderer
    {
        // Match the Python PNG size (12 x 6 inches at 200 DPI).
        private const int Width = 2400, Height = 1200;
        private static readonly RectangleF Plot = new RectangleF(130, 120, 2230, 960);

        private static Font Font(float pixels) { return new Font("Arial", pixels, FontStyle.Regular, GraphicsUnit.Pixel); }
        private static float X(DateTime date) { return Plot.Left + (float)((date - Program.Start).TotalDays / (Program.End - Program.Start).TotalDays) * Plot.Width; }
        private static float Y(double value, double maximum) { return Plot.Bottom - (float)(value / maximum) * Plot.Height; }

        internal static void Save(SeasonData data, bool twoSeasons, string path)
        {
            string current = data.Latest.ToString(Program.Culture), previous = (data.Latest - 1).ToString(Program.Culture);
            var columns = new List<string>();
            if (twoSeasons) columns.Add(previous);
            columns.Add(current); columns.Add("Average");
            double peak = data.Rows.SelectMany(r => columns.Select(c => r.Values[c] ?? 0)).Max();
            double rawStep = Math.Max(peak * 1.05, 1) / 7;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double step = new[] { 1.0, 2.0, 2.5, 5.0, 10.0 }.First(n => n * magnitude >= rawStep) * magnitude;
            double maximum = Math.Ceiling(Math.Max(peak * 1.05, 1) / step) * step;

            using (var bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Font title = Font(34))
            using (Font tick = Font(25))
            using (Font day = Font(16))
            using (Font axis = Font(28))
            using (Font legend = Font(28))
            using (var border = new Pen(Color.Black, 2))
            using (var major = new Pen(Color.FromArgb(205, 205, 205), 1.5f))
            using (var minor = new Pen(Color.FromArgb(230, 230, 230), 1))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                bitmap.SetResolution(200, 200);
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(data.Lake.Title, title, Brushes.Black, new RectangleF(80, 14, Width - 160, 98), center);
                minor.DashStyle = DashStyle.Dot;
                for (DateTime date = Program.Start; date <= Program.End; date = date.AddDays(1))
                {
                    float x = X(date);
                    bool isMajor = date.Day == 1 || date.Day == 7 || date.Day == 14 || date.Day == 21;
                    g.DrawLine(isMajor ? major : minor, x, Plot.Top, x, Plot.Bottom);
                    g.DrawLine(border, x, Plot.Bottom, x, Plot.Bottom + (isMajor ? 9 : 5));
                    if (isMajor)
                    {
                        GraphicsState state = g.Save();
                        g.TranslateTransform(x, Plot.Bottom + 16);
                        g.RotateTransform(-45);
                        using (var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                            g.DrawString(date.ToString("MMM-dd", Program.Culture), day, Brushes.Black, 0, 0, format);
                        g.Restore(state);
                    }
                }
                for (int i = 0; i <= (int)Math.Round(maximum / step); i++)
                {
                    double value = i * step;
                    float y = Y(value, maximum);
                    g.DrawLine(major, Plot.Left, y, Plot.Right, y);
                    g.DrawLine(border, Plot.Left - 8, y, Plot.Left, y);
                    g.DrawString(value.ToString("0.###", Program.Culture), tick, Brushes.Black, Plot.Left - 17, y, right);
                }
                GraphicsState axisState = g.Save();
                g.TranslateTransform(40, Plot.Top + Plot.Height / 2);
                g.RotateTransform(-90);
                g.DrawString("Ice Cover (%) Couverture de glace", axis, Brushes.Black, 0, 0, center);
                g.Restore(axisState);

                GraphicsState clip = g.Save();
                g.SetClip(Plot);
                if (twoSeasons) DrawSeries(g, data.Rows, previous, Color.Red, 5, maximum);
                DrawSeries(g, data.Rows, current, Color.Blue, 5, maximum);
                DrawSeries(g, data.Rows, "Average", Color.Lime, 3.3f, maximum);
                g.Restore(clip);
                g.DrawRectangle(border, Plot.X, Plot.Y, Plot.Width, Plot.Height);

                string[] labels = twoSeasons ? new[] { previous, current, "Average - Moyenne" } : new[] { current, "Average - Moyenne" };
                Color[] colors = twoSeasons ? new[] { Color.Red, Color.Blue, Color.Lime } : new[] { Color.Blue, Color.Lime };
                float legendWidth = g.MeasureString("Average - Moyenne", legend).Width + 120;
                RectangleF box = new RectangleF(Plot.Left + 14, Plot.Top + 14, legendWidth, labels.Length * 42 + 16);
                using (var fill = new SolidBrush(Color.FromArgb(235, Color.White))) g.FillRectangle(fill, box);
                g.DrawRectangle(major, box.X, box.Y, box.Width, box.Height);
                for (int i = 0; i < labels.Length; i++)
                {
                    float y = box.Top + 28 + 42 * i;
                    using (var pen = new Pen(colors[i], labels[i] == "Average - Moyenne" ? 3.3f : 5))
                        g.DrawLine(pen, box.Left + 16, y, box.Left + 80, y);
                    g.DrawString(labels[i], legend, Brushes.Black, box.Left + 96, y - 18);
                }
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static void DrawSeries(Graphics g, List<Observation> rows, string column, Color color, float width, double maximum)
        {
            using (var pen = new Pen(color, width) { LineJoin = LineJoin.Round })
            {
                PointF? last = null;
                foreach (Observation row in rows)
                {
                    double? value = row.Values[column];
                    if (!value.HasValue) { last = null; continue; }
                    var point = new PointF(X(row.Date), Y(value.Value, maximum));
                    if (last.HasValue) g.DrawLine(pen, last.Value, point);
                    last = point;
                }
            }
        }
    }
}
