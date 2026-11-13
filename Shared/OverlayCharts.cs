using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace EastCoastIce
{
    public sealed class IceRow
    {
        public DateTime Date;
        public string Season;
        public double Concentration;
        public double Average;
    }

    // Shared by six independent executable entry points. All plotting and CSV
    // processing is C#; no Python installation or NuGet packages are required.
    public static class OverlayCharts
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Color[] PreviousColors = {
            Color.FromArgb(31,119,180), Color.FromArgb(44,160,44),
            Color.FromArgb(140,86,75), Color.FromArgb(127,127,127),
            Color.FromArgb(23,190,207)
        };

        public static int Run(string[] args, string defaultCsv, string english, string french, bool from1990)
        {
            try
            {
                if (args.Any(a => a != "--no-show"))
                    throw new ArgumentException("Only optional argument: --no-show (save without opening windows).");
                Application.EnableVisualStyles();
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                string csv = Path.Combine(directory, defaultCsv);
                int firstYear = 1990;
                DateTime cutoff = DateTime.MaxValue;
                if (!from1990)
                {
                    Console.Write("Enter the starting season (YYYY-YY): ");
                    firstYear = ParseStartingSeason(Console.ReadLine());
                    Console.Write("Enter the end date (YYYYMMDD): ");
                    cutoff = DateTime.ParseExact((Console.ReadLine() ?? "").Trim(), "yyyyMMdd", Invariant);
                    Console.Write("CSV file (press Enter for " + defaultCsv + "): ");
                    string entered = (Console.ReadLine() ?? "").Trim().Trim('"');
                    if (entered.Length > 0)
                    {
                        csv = entered;
                        if (!Path.IsPathRooted(csv) && !File.Exists(csv))
                            csv = Path.Combine(directory, csv);
                    }
                }

                List<IceRow> rows = LoadData(csv, firstYear, cutoff);
                string[] seasons = GetSeasons(rows);
                string range = (from1990 ? "1990/91" : seasons[0].Replace('-', '/'))
                    + " - " + seasons[seasons.Length - 1].Replace('-', '/');
                string stem = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
                string output = Path.Combine(directory, "output");
                Directory.CreateDirectory(output);
                using (Chart single = CreateChart(rows, english, french, range, false))
                using (Chart recent = CreateChart(rows, english, french, range, true))
                {
                    Save(single, Path.Combine(output, stem + "-single.png"));
                    Save(recent, Path.Combine(output, stem + "-lastFive.png"));
                    Console.WriteLine("Plotted " + rows.Count + " records across " + seasons.Length + " seasons: " + range);
                    if (!args.Contains("--no-show"))
                    {
                        using (Form first = CreateWindow(single, english + " - single season", 40))
                        using (Form second = CreateWindow(recent, english + " - latest + five previous seasons", 100))
                        {
                            // Keep either window alive when the other is closed.
                            using (ApplicationContext context = new ApplicationContext())
                            {
                                int open = 2;
                                FormClosedEventHandler onClose = delegate { if (--open == 0) context.ExitThread(); };
                                first.FormClosed += onClose;
                                second.FormClosed += onClose;
                                first.Show();
                                second.Show();
                                Application.Run(context);
                            }
                        }
                    }
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Cannot create charts: " + error.Message);
                return 1;
            }
        }

        public static int ParseStartingSeason(string text)
        {
            Match match = Regex.Match((text ?? "").Trim(), @"^(\d{4})[-/](\d{2})$");
            if (!match.Success || int.Parse(match.Groups[2].Value, Invariant)
                != (int.Parse(match.Groups[1].Value, Invariant) + 1) % 100)
                throw new ArgumentException("Starting season must be consecutive years, e.g. 1990-91.");
            return int.Parse(match.Groups[1].Value, Invariant);
        }

        public static DateTime Normalize(DateTime date)
        {
            return new DateTime(date.Month >= 11 ? 2023 : 2024, date.Month, date.Day);
        }

        public static string[] GetSeasons(List<IceRow> rows)
        {
            return rows.Select(r => r.Season).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }

        public static List<IceRow> LoadData(string csv, int firstYear, DateTime cutoff)
        {
            List<IceRow> rows = new List<IceRow>();
            using (StreamReader reader = new StreamReader(csv, Encoding.UTF8, true))
            {
                string header = reader.ReadLine();
                if (header == null) throw new InvalidDataException("The CSV is empty.");
                List<string> names = ParseCsvLine(header).Select(s => s.Trim()).ToList();
                string[] required = { "DATE", "Season", "Concentration", "Average" };
                int[] columns = required.Select(n => names.IndexOf(n)).ToArray();
                if (columns.Any(n => n < 0))
                    throw new InvalidDataException("CSV requires DATE, Season, Concentration and Average columns.");
                string line;
                int lineNumber = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        List<string> values = ParseCsvLine(line);
                        DateTime date = DateTime.ParseExact(values[columns[0]].Trim(), "yyyyMMdd", Invariant);
                        string season = values[columns[1]].Trim().Replace('/', '-');
                        // Preserve existing CSV labels, including historical
                        // typos, exactly as the Python programs do. Only the
                        // first four digits are used to select the start year.
                        int year = int.Parse(season.Substring(0, 4), Invariant);
                        if (year < firstYear || date > cutoff || (date.Month > 6 && date.Month < 11)) continue;
                        rows.Add(new IceRow { Date = date, Season = season,
                            Concentration = ParseValue(values[columns[2]]), Average = ParseValue(values[columns[3]]) });
                    }
                    catch (Exception error)
                    {
                        throw new InvalidDataException("Invalid CSV row " + lineNumber + ": " + error.Message, error);
                    }
                }
            }
            if (rows.Count == 0) throw new InvalidDataException("No November-June data matches the selected period.");
            return rows.OrderBy(r => r.Date).ToList();
        }

        private static double ParseValue(string text)
        {
            text = text.Trim();
            return text.Length == 0 || text.Equals("NaN", StringComparison.OrdinalIgnoreCase)
                ? Double.NaN : Double.Parse(text, NumberStyles.Float, Invariant);
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (ch == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
                else value.Append(ch);
            }
            if (quoted) throw new InvalidDataException("Unclosed CSV quote.");
            fields.Add(value.ToString());
            return fields;
        }

        public static Chart CreateChart(List<IceRow> rows, string english, string french, string range, bool recent)
        {
            Chart chart = new Chart { Width = 1800, Height = 1200, BackColor = Color.White,
                AntiAliasing = AntiAliasingStyles.All, TextAntiAliasingQuality = TextAntiAliasingQuality.High };
            ChartArea area = new ChartArea("Ice");
            area.Position = new ElementPosition(2, 13, 96, 85);
            area.BackColor = Color.White;
            area.AxisX.Minimum = new DateTime(2023,11,1).ToOADate();
            area.AxisX.Maximum = new DateTime(2024,6,30).ToOADate();
            area.AxisX.IntervalType = DateTimeIntervalType.Months;
            area.AxisX.Interval = 1;
            area.AxisX.IsLabelAutoFit = false;
            area.AxisX.LabelStyle.Angle = 0;
            area.AxisX.LabelStyle.Font = new Font("Arial", 14);
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.MinorGrid.Enabled = true;
            area.AxisX.MinorGrid.IntervalType = DateTimeIntervalType.Days;
            area.AxisX.MinorGrid.Interval = 7;
            area.AxisX.MinorGrid.LineColor = Color.FromArgb(245,245,245);
            string[] labels = { "Nov/Nov", "Dec/Déc", "Jan/Jan", "Feb/Fév", "Mar/Mar", "Apr/Avr", "May/Mai", "Jun/Jun" };
            for (int i = 0; i < labels.Length; i++)
            {
                DateTime month = new DateTime(2023,11,1).AddMonths(i);
                double middle = month.ToOADate();
                area.AxisX.CustomLabels.Add(middle - 14, middle + 14, labels[i]);
            }
            area.AxisY.Minimum = 0;
            double maximum = rows.Where(r => !Double.IsNaN(r.Concentration)).Select(r => r.Concentration * 110).DefaultIfEmpty(0).Max();
            area.AxisY.Maximum = Math.Max(maximum, 10);
            double step = Math.Pow(10, Math.Floor(Math.Log10(area.AxisY.Maximum / 7)));
            area.AxisY.Interval = new double[] { 1, 2, 2.5, 5, 10 }
                .Select(n => n * step).First(n => n >= area.AxisY.Maximum / 7);
            area.AxisY.LabelStyle.Format = "0.##";
            area.AxisY.IsLabelAutoFit = false;
            area.AxisY.MajorGrid.Enabled = false;
            area.AxisY.LabelStyle.Font = new Font("Arial", 12);
            area.AxisX.Title = "Month/Mois";
            area.AxisY.Title = "% Ice Coverage\nCouverture de glace (%)";
            area.AxisX.TitleFont = new Font("Arial", 13);
            area.AxisY.TitleFont = new Font("Arial", 15);
            chart.ChartAreas.Add(area);
            chart.Titles.Add(new Title(english + " Ice Concentration by Season\nConcentration des glaces " + french + "\n" + range,
                Docking.Top, new Font("Arial", 19), Color.Black));
            Legend legend = new Legend("Seasons") { DockedToChartArea = "Ice", IsDockedInsideChartArea = true,
                Docking = Docking.Top, Alignment = StringAlignment.Far, LegendStyle = LegendStyle.Column,
                Font = new Font("Arial", 12), BackColor = Color.FromArgb(240,255,255,255), BorderColor = Color.LightGray };
            chart.Legends.Add(legend);
            string[] seasons = GetSeasons(rows);
            int previousStart = Math.Max(0, seasons.Length - 6);
            for (int i = 0; i < seasons.Length - 1; i++)
            {
                bool highlighted = recent && i >= previousStart;
                Color color = highlighted ? Color.FromArgb(153, PreviousColors[i - previousStart])
                    : Color.FromArgb(recent ? 51 : 77, Color.LightGray);
                AddSeries(chart, seasons[i], rows.Where(r => r.Season == seasons[i]).Select(r => Tuple.Create(Normalize(r.Date), r.Concentration * 100)),
                    color, highlighted ? 2 : 1, highlighted);
            }
            string latest = seasons[seasons.Length - 1];
            AddSeries(chart, latest, rows.Where(r => r.Season == latest).Select(r => Tuple.Create(Normalize(r.Date), r.Concentration * 100)),
                recent ? Color.Red : Color.OrangeRed, 4, true);
            // Average the provided Average column at each normalized date, as
            // the Python programs do; this is not a new climatology calculation.
            AddSeries(chart, "Average - Moyenne", rows.GroupBy(r => Normalize(r.Date)).OrderBy(g => g.Key)
                .Select(g => Tuple.Create(g.Key, g.Where(r => !Double.IsNaN(r.Average)).Select(r => r.Average * 100).DefaultIfEmpty(Double.NaN).Average())),
                Color.Lime, 4, true);
            return chart;
        }

        private static void AddSeries(Chart chart, string name, IEnumerable<Tuple<DateTime,double>> points, Color color, int width, bool legend)
        {
            Series series = new Series(name) { ChartArea = "Ice", ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.DateTime, Color = color, BorderWidth = width, IsVisibleInLegend = legend };
            foreach (var point in points)
            {
                DataPoint value = new DataPoint(point.Item1.ToOADate(), Double.IsNaN(point.Item2) ? 0 : point.Item2);
                value.IsEmpty = Double.IsNaN(point.Item2);
                series.Points.Add(value);
            }
            series.EmptyPointStyle.Color = Color.Transparent;
            chart.Series.Add(series);
        }

        private static void Save(Chart chart, string path)
        {
            chart.SaveImage(path, ChartImageFormat.Png);
            Console.WriteLine("Saved: " + path);
        }

        private static Form CreateWindow(Chart chart, string title, int offset)
        {
            Form form = new Form { Text = title, ClientSize = new Size(1200,800),
                StartPosition = FormStartPosition.Manual, Location = new Point(offset,offset) };
            chart.Dock = DockStyle.Fill;
            form.Controls.Add(chart);
            return form;
        }
    }
}
