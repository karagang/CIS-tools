using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace ArcticOverlay
{
    public sealed class Region
    {
        public string English, French, Folder, Csv, Map;
        public Region(string en, string fr, string folder, string csv, string map)
        { English = en; French = fr; Folder = folder; Csv = csv; Map = map; }
        public override string ToString() { return English + " / " + French; }
        public static readonly Region[] All = {
            new Region("Canadian Arctic", "Arctique canadien", "Canadian Arctic", "Canadian_Arctic.csv", "Canadian.png"),
            new Region("Eastern Arctic", "Arctique oriental", "Eastern Canadian Arrctic", "Eastern_Arctic-1983-area.csv", "Eastern.png"),
            new Region("Western Arctic", "Arctique occidental", "Western Arctic", "Western_Arctic-1983.csv", "Western.png"),
            new Region("Hudson Bay", "Baie d’Hudson", "Hudson Bay", "HudsonBay_area.csv", null),
            new Region("Northern Canadian Waters", "Eaux du Nord canadien", "Northern_Canadian_Waters", "Northern_CDN_Waters.csv", "Northern.png")
        };
    }
    public sealed class Observation
    {
        public DateTime Date;
        public double Concentration;
        public double? Area;
    }
    public sealed class SourceInfo { public string Path, Sha256; public int Rows; }
    public sealed class CsvData
    {
        public string[] Headers;
        public List<string[]> Records = new List<string[]>();
        public List<Observation> Rows = new List<Observation>();
        public SourceInfo Source;
    }
    public sealed class Annual { public int Year; public double Concentration, Anomaly; }
    public sealed class Options
    {
        public Region Region;
        public string Csv, East, West, Output;
        public bool Merge;
        public int StartYear, CustomStart, CustomEnd;
        public DateTime End;
    }
    public sealed class Result
    {
        public List<string> Files = new List<string>();
        public List<Annual> Years;
        public List<SourceInfo> Sources;
        public DateTime Latest;
        public double Median, Mean, Area;
        public int Rank, Week;
        public int[] Excluded;
    }
    public static class Engine
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static readonly string[] FrenchMonths = { "", "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" };
        public static string F(double value) { return Math.Round(value, 2, MidpointRounding.ToEven).ToString("F2", Inv); }
        public static string Date(DateTime value) { return value.ToString("yyyy-MM-dd", Inv); }
        public static int Week(int day) { return Math.Min((day - 1) / 7 + 1, 4); }
        public static double Median(IEnumerable<double> values)
        {
            double[] list = values.OrderBy(v => v).ToArray();
            if (list.Length == 0) throw new ArgumentException("No values / Aucune valeur.");
            return list.Length % 2 == 1 ? list[list.Length / 2] : (list[list.Length / 2 - 1] + list[list.Length / 2]) / 2;
        }
        static double Number(string value)
        {
            double number;
            if (!double.TryParse(value, NumberStyles.Float, Inv, out number) || double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException("Invalid numeric value / Valeur numérique invalide: " + value);
            return number;
        }
        public static CsvData Read(string path)
        {
            path = Path.GetFullPath(path);
            byte[] bytes = File.ReadAllBytes(path);
            var data = new CsvData();
            using (var sha = SHA256.Create())
                data.Source = new SourceInfo { Path = path, Sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() };
            using (var stream = new MemoryStream(bytes))
            using (var csv = new TextFieldParser(stream, Encoding.UTF8, true))
            {
                csv.SetDelimiters(","); csv.HasFieldsEnclosedInQuotes = true; csv.TrimWhiteSpace = true;
                if (csv.EndOfData) throw new ArgumentException("Empty CSV / CSV vide: " + path);
                data.Headers = csv.ReadFields().Select(h => h.Trim()).ToArray();
                int d = Array.IndexOf(data.Headers, "DATE"), c = Array.IndexOf(data.Headers, "Concentration"), a = Array.IndexOf(data.Headers, "Area");
                if (d < 0 || c < 0) throw new ArgumentException("DATE and Concentration columns required / Colonnes DATE et Concentration requises.");
                while (!csv.EndOfData)
                {
                    string[] fields = csv.ReadFields();
                    if (fields.Length > data.Headers.Length) throw new ArgumentException("CSV has more values than headers / Trop de valeurs dans une ligne CSV.");
                    Array.Resize(ref fields, data.Headers.Length);
                    for (int i = 0; i < fields.Length; i++) fields[i] = fields[i] ?? "";
                    DateTime date;
                    if (!DateTime.TryParseExact(fields[d], "yyyyMMdd", Inv, DateTimeStyles.None, out date))
                        throw new ArgumentException("Invalid date / Date invalide: " + fields[d]);
                    double value = Number(fields[c]);
                    if (value < 0 || value > 1) throw new ArgumentException("Concentration must be 0–1 / La concentration doit être entre 0 et 1.");
                    double? area = a >= 0 && !string.IsNullOrWhiteSpace(fields[a]) ? (double?)Number(fields[a]) : null;
                    data.Records.Add(fields); data.Rows.Add(new Observation { Date = date, Concentration = value, Area = area });
                }
            }
            if (data.Rows.Count == 0) throw new ArgumentException("No observations / Aucune observation: " + path);
            data.Source.Rows = data.Rows.Count;
            return data;
        }
        public static double AreaOf(List<Observation> rows)
        {
            double[] areas = rows.Where(r => r.Area.HasValue).Select(r => r.Area.Value).Distinct().ToArray();
            if (areas.Length != 1 || areas[0] <= 0 || double.IsNaN(areas[0]) || double.IsInfinity(areas[0]))
                throw new ArgumentException("Area must contain one positive region area in km² / Area doit contenir une superficie régionale positive en km².");
            return areas[0];
        }
        static string Canonical(string name)
        {
            return name == "First-Year Ice" ? "First-Year-Ice" : name == "Young Ice" ? "Young_Ice" : name == "New Ice" ? "New_Ice" : name;
        }
        public static CsvData Merge(CsvData east, CsvData west, string output, out int[] excluded)
        {
            foreach (var source in new[] { east, west })
                if (source.Rows.GroupBy(r => r.Date).Any(g => g.Count() > 1))
                    throw new ArgumentException("Merge requires unique source dates / La fusion exige des dates uniques dans chaque source.");
            output = Path.GetFullPath(output);
            if (string.Equals(output, east.Source.Path, StringComparison.OrdinalIgnoreCase) || string.Equals(output, west.Source.Path, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Merged CSV cannot overwrite a source / Le CSV fusionné ne peut pas remplacer une source.");
            string[] eh = east.Headers.Select(Canonical).ToArray(), wh = west.Headers.Select(Canonical).ToArray();
            var columns = new[] { "Concentration", "Old_Ice", "First-Year-Ice", "Young_Ice", "New_Ice", "Average", "Median" }
                .Where(c => eh.Contains(c) && wh.Contains(c)).ToList();
            var westIndex = west.Rows.Select((r, i) => new { r.Date, Index = i }).ToDictionary(r => r.Date, r => r.Index);
            var text = new StringBuilder("Area,DATE," + string.Join(",", columns) + "\n");
            int matched = 0;
            foreach (int i in Enumerable.Range(0, east.Rows.Count).OrderBy(i => east.Rows[i].Date))
            {
                int j;
                if (!westIndex.TryGetValue(east.Rows[i].Date, out j)) continue;
                text.Append("2561739,").Append(east.Rows[i].Date.ToString("yyyyMMdd", Inv));
                foreach (string column in columns)
                {
                    string a = east.Records[i][Array.IndexOf(eh, column)], b = west.Records[j][Array.IndexOf(wh, column)];
                    text.Append(',');
                    if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && a != "NaN" && b != "NaN")
                        text.Append(Math.Round((Number(a) * 1265567 + Number(b) * 1296172) / 2561739, 4, MidpointRounding.ToEven).ToString("0.0000", Inv));
                }
                text.Append('\n'); matched++;
            }
            if (matched == 0) throw new ArgumentException("No matching dates / Aucune date commune.");
            excluded = new[] { east.Rows.Count - matched, west.Rows.Count - matched };
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, text.ToString(), new UTF8Encoding(false));
            return Read(output);
        }
        public static List<Annual> Weekly(List<Observation> rows, out DateTime latest, out int week)
        {
            latest = rows.Max(r => r.Date);
            int month = latest.Month, w = Week(latest.Day); week = w;
            var annual = rows.Where(r => r.Date.Month == month && Week(r.Date.Day) == w)
                .GroupBy(r => r.Date.Year).OrderBy(g => g.Key)
                .Select(g => new Annual { Year = g.Key, Concentration = g.Average(r => r.Concentration) * 100 }).ToList();
            double median = Median(annual.Select(a => a.Concentration));
            foreach (var item in annual) item.Anomaly = item.Concentration - median;
            return annual;
        }
        public static Result Run(Options options)
        {
            if (options.StartYear < 1 || options.StartYear > 9999) throw new ArgumentException("Invalid start year / Année de début invalide.");
            if (options.End.Year < options.StartYear) throw new ArgumentException("End date precedes start year / La date de fin précède l’année de début.");
            if ((options.CustomStart == 0) != (options.CustomEnd == 0)) throw new ArgumentException("Supply both custom dates / Fournir les deux dates personnalisées.");
            if (options.CustomStart != 0) { CalendarDay(options.CustomStart); CalendarDay(options.CustomEnd); }
            var result = new Result { Sources = new List<SourceInfo>() };
            CsvData data;
            if (options.Merge)
            {
                if (options.Region.English != "Canadian Arctic") throw new ArgumentException("Merge is only for Canadian Arctic / Fusion réservée à l’Arctique canadien.");
                var east = Read(options.East); var west = Read(options.West);
                options.Csv = Path.Combine(options.Output, "Canadian_Arctic.csv");
                data = Merge(east, west, options.Csv, out result.Excluded);
                result.Sources.Add(data.Source); result.Sources.Add(east.Source); result.Sources.Add(west.Source);
            }
            else { data = Read(options.Csv); result.Sources.Add(data.Source); }
            var rows = data.Rows.Where(r => r.Date.Year >= options.StartYear && r.Date <= options.End).OrderBy(r => r.Date).ToList();
            if (rows.Count == 0) throw new ArgumentException("No observations in selected range / Aucune observation dans la période choisie.");
            result.Area = AreaOf(rows);
            result.Years = Weekly(rows, out result.Latest, out result.Week);
            result.Median = Median(result.Years.Select(a => a.Concentration));
            result.Mean = result.Years.Average(a => a.Concentration);
            double value = result.Years.Last().Concentration;
            result.Rank = 1 + result.Years.Count(a => a.Concentration < value);
            Directory.CreateDirectory(options.Output);
            string stem = Path.GetFileNameWithoutExtension(options.Csv) + "_from_" + options.StartYear + "_through_" + options.End.ToString("yyyyMMdd", Inv);
            result.Files.AddRange(Charts.Draw(rows, options, result, stem));
            foreach (bool fr in new[] { false, true })
            {
                string path = Path.Combine(options.Output, "Climatology_" + stem + (fr ? "_FR.txt" : "_EN.txt"));
                File.WriteAllText(path, Report(rows, options, result, fr), new UTF8Encoding(false));
                result.Files.Add(path);
            }
            return result;
        }
        public static int CalendarDay(int mmdd)
        {
            return new DateTime(2000, mmdd / 100, mmdd % 100).DayOfYear;
        }
        static string Report(List<Observation> rows, Options o, Result r, bool fr)
        {
            var text = new StringBuilder();
            Action<string, string> line = (en, french) => text.AppendLine(fr ? french : en);
            double value = r.Years.Last().Concentration;
            string month = fr ? FrenchMonths[r.Latest.Month] : Inv.DateTimeFormat.GetMonthName(r.Latest.Month);
            line("Weekly climatology summary — " + o.Region.English, "Résumé climatologique hebdomadaire — " + o.Region.French);
            line("Requested start year: " + o.StartYear, "Année de début demandée : " + o.StartYear);
            line("Requested end date: " + Date(o.End), "Date de fin demandée : " + Date(o.End));
            line("Observed period: " + Date(rows.First().Date) + " to " + Date(r.Latest), "Période observée : " + Date(rows.First().Date) + " au " + Date(r.Latest));
            line("Week compared: " + r.Week + ", " + month, "Semaine comparée : " + r.Week + ", " + month);
            line("Weeks: 1 = days 1–7; 2 = 8–14; 3 = 15–21; 4 = 22–31.", "Semaines : 1 = jours 1–7; 2 = 8–14; 3 = 15–21; 4 = 22–31.");
            line("Years compared: " + r.Years.First().Year + "–" + r.Years.Last().Year + " (" + r.Years.Count + " available years)",
                 "Années comparées : " + r.Years.First().Year + "–" + r.Years.Last().Year + " (" + r.Years.Count + " années présentes)");
            line("Years without observations for this week are omitted.", "Les années sans observation pour cette semaine sont omises.");
            line("Calculation: yearly mean for this week, then median and mean of those yearly values.", "Calcul : moyenne par année pour cette semaine, puis médiane et moyenne de ces valeurs annuelles.");
            line("CSV Average and Median columns are not used.", "Les colonnes Average et Median du CSV ne sont pas utilisées.");
            line("Anomalies are differences from the median in percentage points.", "Les anomalies sont des différences par rapport à la médiane, en points de pourcentage.");
            line("Region area: " + r.Area.ToString("0", Inv) + " km²", "Superficie de la région : " + r.Area.ToString("0", Inv) + " km²");
            line(r.Latest.Year + " value (weekly mean): " + F(value) + "%", "Valeur de " + r.Latest.Year + " (moyenne de la semaine) : " + F(value) + " %");
            line("Rank from lowest (ties share rank): " + r.Rank + " of " + r.Years.Count, "Rang croissant (ex æquo au même rang) : " + r.Rank + " sur " + r.Years.Count);
            line("Median: " + F(r.Median) + "%", "Médiane : " + F(r.Median) + " %");
            line("Mean: " + F(r.Mean) + "%", "Moyenne : " + F(r.Mean) + " %");
            line(r.Latest.Year + " anomaly: " + F(value - r.Median) + " percentage points", "Anomalie de " + r.Latest.Year + " : " + F(value - r.Median) + " points de pourcentage");
            line(r.Latest.Year + " ice area: " + F(value * r.Area / 100) + " km²", "Superficie englacée de " + r.Latest.Year + " : " + F(value * r.Area / 100) + " km²");
            line("Median ice area: " + F(r.Median * r.Area / 100) + " km²", "Superficie médiane : " + F(r.Median * r.Area / 100) + " km²");
            line("Difference from median: " + F((value - r.Median) * r.Area / 100) + " km²", "Écart à la médiane : " + F((value - r.Median) * r.Area / 100) + " km²");
            line("Mean ice area: " + F(r.Mean * r.Area / 100) + " km²", "Superficie moyenne : " + F(r.Mean * r.Area / 100) + " km²");
            line("Difference from mean: " + F((value - r.Mean) * r.Area / 100) + " km²", "Écart à la moyenne : " + F((value - r.Mean) * r.Area / 100) + " km²");
            foreach (bool ascending in new[] { true, false })
            {
                text.AppendLine();
                line(ascending ? "Seven lowest years:" : "Seven highest years:", ascending ? "Sept années les plus faibles :" : "Sept années les plus élevées :");
                var list = ascending ? r.Years.OrderBy(a => a.Concentration) : r.Years.OrderByDescending(a => a.Concentration);
                foreach (var a in list.Take(7)) text.AppendLine("  " + a.Year + ": " + F(a.Concentration) + "% | " + F(a.Concentration * r.Area / 100) + " km²");
            }
            if (r.Excluded != null)
            {
                text.AppendLine();
                line("Merge: matching dates only; weights East 1265567 km², West 1296172 km².", "Fusion : dates communes seulement; pondération Est 1265567 km², Ouest 1296172 km².");
                line("Unmatched dates excluded: East " + r.Excluded[0] + ", West " + r.Excluded[1] + ".", "Dates exclues faute de correspondance : Est " + r.Excluded[0] + ", Ouest " + r.Excluded[1] + ".");
            }
            text.AppendLine();
            line("CSV files used (full paths, SHA-256 checksums):", "Fichiers CSV utilisés (chemins complets, empreintes SHA-256) :");
            foreach (var source in r.Sources)
            {
                text.AppendLine(source.Path).AppendLine("SHA-256: " + source.Sha256);
                line("Rows: " + source.Rows, "Lignes: " + source.Rows);
            }
            return text.ToString();
        }
    }
}
