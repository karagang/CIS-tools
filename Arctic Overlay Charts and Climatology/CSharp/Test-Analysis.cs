using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcticOverlay;

class AnalysisTests
{
    static void Check(bool test, string message) { if (!test) throw new Exception(message); }
    static Observation Row(string date, double value) { return new Observation { Date = DateTime.Parse(date), Concentration = value, Area = 1000 }; }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected input rejection."); }
    static int Main()
    {
        var rows = new List<Observation> { Row("1990-04-16", 1), Row("1991-04-16", 0), Row("1991-04-17", .4), Row("1992-04-16", .6) };
        DateTime latest; int week;
        var annual = Engine.Weekly(rows.Where(r => r.Date.Year >= 1991).ToList(), out latest, out week);
        Check(week == 3 && annual.Count == 2 && Math.Abs(annual[0].Concentration - 20) < 1e-10 && Math.Abs(Engine.Median(annual.Select(a => a.Concentration)) - 40) < 1e-10, "Weekly means and start year");
        Check(Engine.Week(31) == 4 && Engine.Week(22) == 4 && Engine.Week(21) == 3, "Week boundaries");
        var calendar = Charts.Calendar(new List<Observation> { Row("2019-03-01", 0), Row("2020-03-01", 0), Row("2020-02-29", 0) }, 101, 1231);
        Check(calendar[0].X == calendar[1].X && calendar[1].X - calendar[2].X == 1, "Leap calendar");
        calendar = Charts.Calendar(new List<Observation> { Row("2019-10-01", 0), Row("2020-01-01", 0) }, 1001, 131);
        Check(calendar.All(p => p.Season == 2019) && calendar[0].X < calendar[1].X, "Cross-year season");
        string folder = Path.Combine(Path.GetTempPath(), "ArcticOverlayTests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            string east = Path.Combine(folder, "east.csv"), west = Path.Combine(folder, "west.csv"), output = Path.Combine(folder, "merged.csv");
            File.WriteAllText(east, "DATE,Concentration,First-Year Ice\n20200101,0,0.2\n20200108,0.2,0.4\n");
            File.WriteAllText(west, "DATE,Concentration,First-Year-Ice\n20200101,1,0.6\n20200115,0.6,0.7\n");
            int[] excluded;
            var merged = Engine.Merge(Engine.Read(east), Engine.Read(west), output, out excluded);
            Check(merged.Rows.Count == 1 && excluded.SequenceEqual(new[] { 1, 1 }), "Matching dates");
            Check(Math.Abs(merged.Rows[0].Concentration - Math.Round(1296172.0 / 2561739, 4)) < 1e-10 && merged.Headers.Contains("First-Year-Ice"), "Weighted merge and aliases");
            Reject(() => Engine.Merge(Engine.Read(east), Engine.Read(west), east, out excluded));
            File.WriteAllText(west, "DATE,Concentration\n20200101,1\n20200101,0\n");
            Reject(() => Engine.Merge(Engine.Read(east), Engine.Read(west), output, out excluded));
            foreach (string bad in new[] { "20200230,0.1", "20200101,NaN", "20200101,1.1" })
            { File.WriteAllText(west, "DATE,Concentration\n" + bad + "\n"); Reject(() => Engine.Read(west)); }
            Reject(() => Engine.AreaOf(new List<Observation> { Row("2020-01-01", 0), new Observation { Date = DateTime.Today, Concentration = 0, Area = 2000 } }));
            Reject(() => Engine.Run(new Options { StartYear = 2021, End = new DateTime(2020, 1, 1) }));
            Reject(() => MainForm.ParseCustom("0230"));
        }
        finally
        {
            // This GUID-created test folder contains only the fixture files above.
            foreach (string path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
        Console.WriteLine("Passed: weekly aggregation, start-year filtering, zeros, week boundaries, leap/calendar seasons, weighted matching-date merge, aliases, source protection, duplicate/invalid input and date validation.");
        return 0;
    }
}
