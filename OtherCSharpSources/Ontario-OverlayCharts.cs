// Windows .NET Framework 4.x. Build with build.ps1; no Python or NuGet required.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.VisualBasic.FileIO;

internal static class OverlayCharts
{
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;
    private static readonly string[] FrenchMonths = { "", "janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc." };
    private static readonly Color[] Palette = {
        Color.FromArgb(31,119,180), Color.FromArgb(255,127,14), Color.FromArgb(44,160,44),
        Color.FromArgb(214,39,40), Color.FromArgb(148,103,189), Color.FromArgb(140,86,75),
        Color.FromArgb(227,119,194), Color.FromArgb(127,127,127), Color.FromArgb(188,189,34), Color.FromArgb(23,190,207)
    };
    private static readonly DateTime Start = new DateTime(1999, 11, 1);
    private static readonly DateTime End = new DateTime(2000, 6, 21);
    private sealed class Row { public DateTime Date; public double?[] Values; }
    private sealed class Data
    {
        public string[] Headers;
        public int AverageIndex;
        public List<int> YearIndices;
        public List<Row> Rows = new List<Row>();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string csv = null, output = null;
            int lastN = 5;
            bool show = true;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help": case "-h":
                        Console.WriteLine("Ontario_OverlayCharts.exe [CSV] [--output-dir FOLDER] [--last-n 5] [--no-show]");
                        return 0;
                    case "--no-show": show = false; break;
                    case "--output-dir": output = Next(args, ref i); break;
                    case "--last-n":
                        if (!int.TryParse(Next(args, ref i), out lastN) || lastN < 1)
                            throw new ArgumentException("--last-n must be a positive integer.");
                        break;
                    default:
                        if (args[i].StartsWith("--") || csv != null)
                            throw new ArgumentException("Unrecognized argument: " + args[i]);
                        csv = args[i]; break;
                }
            }
            if (csv == null)
            {
                string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ontario_with_average.csv");
                Console.Write("Enter CSV path [" + fallback + "]: ");
                csv = (Console.ReadLine() ?? "").Trim().Trim('"');
                if (csv.Length == 0) csv = fallback;
            }
            csv = Path.GetFullPath(csv);
            output = Path.GetFullPath(output ?? Path.GetDirectoryName(csv));
            Data data = ReadCsv(csv);
            Directory.CreateDirectory(output);
            var paths = new List<string>();
            foreach (string language in new[] { "BILINGUAL", "EN", "FR" })
            {
                string path = Path.Combine(output, Path.GetFileNameWithoutExtension(csv) + "_chart_" + language + ".png");
                Render(data, language, lastN, path);
                paths.Add(path);
                Console.WriteLine("Saved " + language + " chart to: " + path);
            }
            if (show)
                foreach (string path in paths)
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                    catch (Exception ex) { Console.Error.WriteLine("Chart saved, but could not open viewer: " + ex.Message); }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("ERROR: " + ex.Message); return 1; }
    }

    private static string Next(string[] args, ref int i)
    {
        if (++i >= args.Length) throw new ArgumentException("Missing option value.");
        return args[i];
    }

    private static Data ReadCsv(string path)
    {
        using (var reader = new TextFieldParser(path, System.Text.Encoding.UTF8))
        {
            reader.SetDelimiters(",");
            reader.HasFieldsEnclosedInQuotes = true;
            if (reader.EndOfData) throw new FormatException("The CSV is empty.");
            var data = new Data { Headers = reader.ReadFields().Select(s => s.Trim()).ToArray() };
            if (data.Headers.Distinct().Count() != data.Headers.Length)
                throw new FormatException("CSV column names must be unique.");
            int dateIndex = Array.IndexOf(data.Headers, "Date");
            data.AverageIndex = Array.IndexOf(data.Headers, "Average");
            if (dateIndex < 0 || data.AverageIndex < 0)
                throw new FormatException("The CSV must contain Date and Average columns.");
            data.YearIndices = Enumerable.Range(0, data.Headers.Length)
                .Where(i => data.Headers[i].Length > 0 && data.Headers[i].All(c => c >= '0' && c <= '9'))
                .OrderBy(i => int.Parse(data.Headers[i], English)).ToList();
            if (data.YearIndices.Count == 0) throw new FormatException("No numeric year columns found.");
            while (!reader.EndOfData)
            {
                long line = reader.LineNumber;
                string[] fields = reader.ReadFields();
                if (fields.Length != data.Headers.Length)
                    throw new FormatException("Wrong number of fields on CSV line " + line);
                DateTime date;
                if (!DateTime.TryParseExact(fields[dateIndex].Trim() + "-2000",
                    new[] { "d-MMM-yyyy", "MMM-d-yyyy" }, English, DateTimeStyles.None, out date))
                    throw new FormatException("Invalid date on CSV line " + line + ": " + fields[dateIndex]);
                var row = new Row { Date = new DateTime(date.Month >= 7 ? 1999 : 2000, date.Month, date.Day), Values = new double?[fields.Length] };
                foreach (int index in data.YearIndices.Concat(new[] { data.AverageIndex }))
                {
                    string value = fields[index].Trim();
                    if (value == "" || new[] { "NA", "N/A", "NaN", "NULL", "None", "<NA>", "#N/A", "#NA", "-NaN" }.Contains(value, StringComparer.OrdinalIgnoreCase)) continue;
                    double number;
                    if (!double.TryParse(value, NumberStyles.Float, English, out number) || double.IsInfinity(number) || double.IsNaN(number))
                        throw new FormatException("Invalid number on CSV line " + line + ", column " + data.Headers[index]);
                    row.Values[index] = number;
                }
                data.Rows.Add(row);
            }
            data.Rows = data.Rows.OrderBy(r => r.Date).ToList();
            if (data.Rows.Count == 0) throw new FormatException("The CSV contains no data rows.");
            return data;
        }
    }

    private static Color Tint(Color c)
    {
        return Color.FromArgb(204, (int)(c.R + (255-c.R)*0.6), (int)(c.G + (255-c.G)*0.6), (int)(c.B + (255-c.B)*0.6));
    }

    private static void Render(Data data, string language, int lastN, string path)
    {
        bool french = language == "FR", bilingual = language == "BILINGUAL";
        string title = french ? "Couverture de glace du lac Ontario" : "Lake Ontario Ice Cover";
        if (bilingual) title += "\nCouverture de glace du lac Ontario";
        string ylabel = french ? "Couverture de glace (%)" : "Ice Cover (%)";
        if (bilingual) ylabel += " / Couverture de glace (%)";
        string average = bilingual ? "Average - Moyenne" : french ? "Moyenne" : "Average";
        long cutoff = (long)int.Parse(data.Headers[data.YearIndices.Last()], English) - lastN + 1;
        var older = data.YearIndices.Where(i => int.Parse(data.Headers[i], English) < cutoff).ToList();
        var recent = data.YearIndices.Except(older).ToList();
        using (var chart = new Chart())
        using (var titleFont = new Font("Arial", 16))
        using (var axisFont = new Font("Arial", 11))
        using (var tickFont = new Font("Arial", 6))
        using (var legendFont = new Font("Arial", 10))
        {
            chart.Width = 3600; chart.Height = 1800;
            chart.BackColor = Color.White;
            chart.AntiAliasing = AntiAliasingStyles.All;
            chart.TextAntiAliasingQuality = TextAntiAliasingQuality.High;
            // Draw at 300 DPI below: fonts are in points, matching a 12 x 6 inch chart.
            var area = new ChartArea("Overlay");
            chart.ChartAreas.Add(area);
            area.Position = new ElementPosition(1, bilingual ? 14 : 10, 98, bilingual ? 84 : 88);
            area.InnerPlotPosition = new ElementPosition(7, 2, 92, 82);
            area.AxisX.Minimum = Start.ToOADate(); area.AxisX.Maximum = End.ToOADate();
            area.AxisY.Minimum = 0;
            area.AxisY.Title = ylabel; area.AxisY.TitleFont = axisFont;
            area.AxisY.LabelStyle.Font = axisFont;
            area.AxisX.LabelStyle.Font = tickFont; area.AxisX.LabelStyle.Angle = -45;
            area.AxisX.IsLabelAutoFit = false;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.MajorTickMark.Enabled = false;
            foreach (var axis in new[] { area.AxisX, area.AxisY })
            {
                axis.LineColor = Color.LightGray;
                axis.MajorGrid.LineColor = Color.LightGray;
                axis.MajorGrid.LineWidth = 3;
            }
            for (DateTime date = Start; date <= End; date = date.AddDays(1))
                if (new[] { 1, 7, 14, 21 }.Contains(date.Day))
                {
                    double x = date.ToOADate();
                    area.AxisX.CustomLabels.Add(x - 0.4, x + 0.4,
                        french ? date.Day.ToString("00") + "-" + FrenchMonths[date.Month] : date.ToString("MMM-dd", English));
                    area.AxisX.StripLines.Add(new StripLine { IntervalOffset = x, StripWidth = 0,
                        BorderColor = Color.LightGray, BorderWidth = 3 });
                }
            chart.Titles.Add(new Title(title, Docking.Top, titleFont, Color.FromArgb(38,38,38)));
            var legend = new Legend("Legend") { Font = legendFont, BackColor = Color.FromArgb(220,255,255,255),
                BorderColor = Color.LightGray, BorderWidth = 2, LegendStyle = LegendStyle.Column,
                Position = new ElementPosition(10, bilingual ? 17 : 13, 22, 34) };
            chart.Legends.Add(legend);
            for (int n = 0; n < older.Count; n++)
            {
                string label = older.Count == 1 ? data.Headers[older[0]] : data.Headers[older[0]] + " - " + data.Headers[older.Last()];
                AddSeries(chart, data, older[n], Color.FromArgb(102,217,217,217), 4, label, n == 0);
            }
            for (int n = 0; n < recent.Count; n++)
            {
                Color color = Palette[n % Palette.Length];
                if (n != recent.Count - 1) color = Tint(color);
                AddSeries(chart, data, recent[n], color, 6, data.Headers[recent[n]], true);
            }
            AddSeries(chart, data, data.AverageIndex, Color.Lime, 4, average, true);
            using (var bitmap = new Bitmap(3600, 1800))
            {
                bitmap.SetResolution(300, 300);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    chart.Printing.PrintPaint(graphics, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                }
                bitmap.Save(path, ImageFormat.Png);
            }
        }
    }

    private static void AddSeries(Chart chart, Data data, int column, Color color, int width, string label, bool legend)
    {
        var series = new Series(data.Headers[column]) { ChartType = SeriesChartType.Line,
            XValueType = ChartValueType.DateTime, Color = color, BorderWidth = width,
            LegendText = label, IsVisibleInLegend = legend };
        foreach (Row row in data.Rows)
            if (row.Values[column].HasValue) series.Points.AddXY(row.Date.ToOADate(), row.Values[column].Value);
        chart.Series.Add(series);
    }
}


