using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;

namespace ArcticOverlay
{
    public sealed class Window
    {
        public string Label;
        public int First, Last;
        public Window(string label, int first, int last) { Label = label; First = first; Last = last; }
    }
    public sealed class CalendarPoint
    {
        public int Season, X;
        public double Y;
    }
    public static class Charts
    {
        static void Text(Graphics g, string text, float x, float y, float size, Color color, bool bold, StringAlignment align)
        {
            using (var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = align }) g.DrawString(text, font, brush, x, y, format);
        }
        static void Line(Graphics g, Color color, float width, float x1, float y1, float x2, float y2)
        { using (var pen = new Pen(color, width)) g.DrawLine(pen, x1, y1, x2, y2); }
        static void Setup(Graphics g)
        { g.Clear(Color.White); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; }
        static void Series(Graphics g, IEnumerable<CalendarPoint> data, Color color, float width, bool dashed, Func<double, float> x, Func<double, float> y)
        {
            var points = data.OrderBy(p => p.X).Select(p => new PointF(x(p.X), y(p.Y))).ToArray();
            using (var pen = new Pen(color, width))
            {
                if (dashed) pen.DashStyle = DashStyle.Dash;
                if (points.Length > 1) g.DrawLines(pen, points);
                else if (points.Length == 1) using (var brush = new SolidBrush(color)) g.FillEllipse(brush, points[0].X - 4, points[0].Y - 4, 8, 8);
            }
        }
        public static List<CalendarPoint> Calendar(List<Observation> rows, int first, int last)
        {
            bool wrap = first > last;
            return rows.Where(r => { int md = r.Date.Month * 100 + r.Date.Day; return wrap ? md >= first || md <= last : md >= first && md <= last; })
                .Select(r => { bool january = wrap && r.Date.Month * 100 + r.Date.Day <= last;
                    return new CalendarPoint { Season = r.Date.Year - (january ? 1 : 0), X = new DateTime(2000, r.Date.Month, r.Date.Day).DayOfYear + (january ? 366 : 0), Y = r.Concentration * 100 }; }).ToList();
        }
        public static List<string> Draw(List<Observation> rows, Options options, Result result, string stem)
        {
            var paths = new List<string>();
            var windows = new List<Window> { new Window("Apr-Dec", 401, 1231), new Window("Jul-Dec", 701, 1231), new Window("Oct-Dec", 1001, 1231) };
            if (options.Region.English == "Eastern Arctic" || options.Region.English == "Northern Canadian Waters") windows.Add(new Window("Oct-Jan", 1001, 131));
            if (options.Region.English == "Northern Canadian Waters") windows.Add(new Window("Jan-Jun", 101, 630));
            if (options.CustomStart != 0) windows.Add(new Window("Custom_" + options.CustomStart.ToString("0000") + "-" + options.CustomEnd.ToString("0000"), options.CustomStart, options.CustomEnd));
            foreach (var window in windows)
            {
                var data = Calendar(rows, window.First, window.Last);
                if (data.Count == 0) continue;
                string path = Path.Combine(options.Output, "Ice_Concentration_" + stem + "_" + window.Label + ".png");
                Overlay(data, rows, options, result.Area, window, path); paths.Add(path);
            }
            string anomaly = Path.Combine(options.Output, "Ice_Anomaly_" + stem + ".png");
            Anomaly(options, result, anomaly); paths.Add(anomaly);
            return paths;
        }
        static void Overlay(List<CalendarPoint> data, List<Observation> rows, Options o, double area, Window window, string output)
        {
            using (var bitmap = new Bitmap(2400, 1500))
            {
                bitmap.SetResolution(180, 180);
                using (var g = Graphics.FromImage(bitmap))
                {
                    Setup(g);
                    Text(g, o.Region.ToString(), 1200, 25, 38, Color.Black, true, StringAlignment.Center);
                    Text(g, Engine.Date(rows.First().Date) + " – " + Engine.Date(rows.Last().Date) + " | " + window.Label, 1200, 80, 29, Color.DimGray, false, StringAlignment.Center);
                    float left = 180, top = 250, width = 1990, height = 1020;
                    int minimum = Engine.CalendarDay(window.First), maximum = Engine.CalendarDay(window.Last) + (window.First > window.Last ? 366 : 0);
                    // A single-day custom window still needs a non-zero axis span.
                    double span = Math.Max(1, maximum - minimum);
                    Func<double, float> x = v => left + (float)((v - minimum) / span * width);
                    Func<double, float> y = v => top + height - (float)(v / 100 * height);
                    for (int i = 0; i <= 10; i++)
                    {
                        Line(g, Color.FromArgb(225, 230, 235), 1, left, y(i * 10), left + width, y(i * 10));
                        Text(g, (i * 10).ToString(), left - 20, y(i * 10) - 15, 26, Color.Black, false, StringAlignment.Far);
                        Text(g, Engine.F(i / 10.0 * area / 1e6), left + width + 20, y(i * 10) - 15, 26, Color.DarkGreen, false, StringAlignment.Near);
                    }
                    int recent = data.Max(p => p.Season);
                    Color[] colors = { Color.Blue, Color.Purple, Color.DarkOrange, Color.Brown, Color.DarkCyan, Color.Red };
                    int legend = 0;
                    foreach (var season in data.GroupBy(p => p.Season).OrderBy(s => s.Key))
                    {
                        int age = recent - season.Key;
                        Color color = age <= 5 ? colors[5 - age] : Color.FromArgb(80, Color.LightGray);
                        Series(g, season, color, age <= 5 ? 4 : 2, false, x, y);
                        if (age <= 5)
                        {
                            float lx = 190 + legend++ * 270;
                            Line(g, color, 4, lx, 177, lx + 45, 177);
                            Text(g, season.Key.ToString() + (window.First > window.Last ? "–" + (season.Key + 1) : ""), lx + 55, 158, 25, Color.Black, false, StringAlignment.Near);
                        }
                    }
                    var median = data.GroupBy(p => new { p.Season, p.X }).Select(s => new CalendarPoint { X = s.Key.X, Y = s.Average(p => p.Y) })
                        .GroupBy(p => p.X).Select(s => new CalendarPoint { X = s.Key, Y = Engine.Median(s.Select(p => p.Y)) });
                    Series(g, median, Color.Black, 5, true, x, y);
                    Text(g, "Median / Médiane ( - - - )", 2180, 158, 25, Color.Black, false, StringAlignment.Far);
                    Line(g, Color.Black, 2, left, top, left, top + height);
                    Line(g, Color.Black, 2, left, top + height, left + width, top + height);
                    int ticks = 0;
                    for (int offset = 0; offset <= 366; offset += 366)
                        for (int month = 1; month <= 12; month++)
                        {
                            int day = new DateTime(2000, month, 15).DayOfYear + offset;
                            if (day < minimum || day > maximum) continue;
                            float px = x(day); ticks++;
                            Line(g, Color.Black, 2, px, top + height, px, top + height + 12);
                            string name = Engine.Inv.DateTimeFormat.GetAbbreviatedMonthName(month) + "\n" + Engine.FrenchMonths[month].Substring(0, Math.Min(4, Engine.FrenchMonths[month].Length));
                            Text(g, name, px, top + height + 22, 25, Color.Black, false, StringAlignment.Center);
                        }
                    if (ticks == 0) Text(g, window.First.ToString("0000") + " – " + window.Last.ToString("0000") + " (MMDD)", left + width / 2, top + height + 22, 25, Color.Black, false, StringAlignment.Center);
                    var state = g.Save(); g.TranslateTransform(35, top + height / 2); g.RotateTransform(-90);
                    Text(g, "Ice coverage / Couverture de glace (%)", 0, 0, 28, Color.Black, true, StringAlignment.Center); g.Restore(state);
                    state = g.Save(); g.TranslateTransform(2350, top + height / 2); g.RotateTransform(-90);
                    Text(g, "Ice area / Superficie englacée (10⁶ km²)", 0, 0, 28, Color.DarkGreen, true, StringAlignment.Center); g.Restore(state);
                    Text(g, "CSV: " + Path.GetFileName(o.Csv) + " | Start year / Année de début: " + o.StartYear, 1200, 1410, 23, Color.DimGray, false, StringAlignment.Center);
                }
                bitmap.Save(output, ImageFormat.Png);
            }
        }
        static void Anomaly(Options o, Result r, string output)
        {
            using (var bitmap = new Bitmap(2400, 1300))
            {
                bitmap.SetResolution(180, 180);
                using (var g = Graphics.FromImage(bitmap))
                {
                    Setup(g);
                    Text(g, o.Region.ToString(), 1200, 25, 38, Color.Black, true, StringAlignment.Center);
                    Text(g, Engine.Inv.DateTimeFormat.GetMonthName(r.Latest.Month) + " / " + Engine.FrenchMonths[r.Latest.Month] + ", week / semaine " + r.Week + " | " + r.Years.First().Year + "–" + r.Years.Last().Year,
                         1200, 80, 29, Color.DimGray, false, StringAlignment.Center);
                    Text(g, "Median / Médiane: " + Engine.F(r.Median) + "%", 1200, 140, 27, Color.Black, false, StringAlignment.Center);
                    float left = 175, top = 230, width = 2100, height = 820;
                    double min = Math.Min(-1, r.Years.Min(a => a.Anomaly) * 1.2), max = Math.Max(1, r.Years.Max(a => a.Anomaly) * 1.2);
                    double span = r.Years.Last().Year - r.Years.First().Year + 2;
                    Func<double, float> x = v => left + (float)((v - r.Years.First().Year + 1) / span * width);
                    Func<double, float> y = v => top + height - (float)((v - min) / (max - min) * height);
                    for (int i = 0; i <= 8; i++)
                    {
                        double value = min + (max - min) * i / 8;
                        Line(g, Color.FromArgb(225, 230, 235), 1, left, y(value), left + width, y(value));
                        Text(g, value.ToString("F1", Engine.Inv), left - 15, y(value) - 15, 25, Color.Black, false, StringAlignment.Far);
                    }
                    float zero = y(0), bw = Math.Min(100, (float)(width / span * .75));
                    foreach (var annual in r.Years)
                    {
                        float py = y(annual.Anomaly);
                        using (var brush = new SolidBrush(annual.Anomaly >= 0 ? Color.Blue : Color.Red))
                            g.FillRectangle(brush, x(annual.Year) - bw / 2, Math.Min(py, zero), bw, Math.Max(1, Math.Abs(py - zero)));
                    }
                    Line(g, Color.Black, 2, left, zero, left + width, zero);
                    Line(g, Color.Black, 2, left, top, left, top + height);
                    int step = Math.Max(1, (int)Math.Ceiling(r.Years.Count / 30.0));
                    for (int i = 0; i < r.Years.Count; i += step)
                    {
                        var state = g.Save(); g.TranslateTransform(x(r.Years[i].Year), top + height + 20); g.RotateTransform(-45);
                        Text(g, r.Years[i].Year.ToString(), 0, 0, 24, Color.Black, false, StringAlignment.Far); g.Restore(state);
                    }
                    var saved = g.Save(); g.TranslateTransform(25, top + height / 2); g.RotateTransform(-90);
                    Text(g, "Anomaly / Anomalie (percentage points / points de pourcentage)", 0, 0, 27, Color.Black, true, StringAlignment.Center); g.Restore(saved);
                    Text(g, "CSV: " + Path.GetFileName(o.Csv) + " | Start year / Année de début: " + o.StartYear, 1200, 1225, 23, Color.DimGray, false, StringAlignment.Center);
                }
                bitmap.Save(output, ImageFormat.Png);
            }
        }
    }
}
