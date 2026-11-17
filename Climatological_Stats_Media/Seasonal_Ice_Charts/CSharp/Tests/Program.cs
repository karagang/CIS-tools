using GreatLakesCharts;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}

string csv = Path.Combine(Path.GetTempPath(), $"ice-chart-test-{Guid.NewGuid():N}.csv");
try
{
    // Quoted CSV, nonchronological rows, leap day, missing values, and a deliberately wrong Average column.
    File.WriteAllText(csv, "Date,2000,2001,2002,2003,Average\n\"29-Feb\",NA,40,na,30,999\n01-Nov,10,20,30,25,999\n15-Jan,20,NA,10,15,999\ninvalid,99,99,99,99,999\n");
    IceData data = ChartGenerator.ReadCsv(csv);
    Analysis result = ChartGenerator.Analyze(data, 2000);
    Check(data.Rows.Count == 3 && data.Rows[0].Date == new DateTime(1999, 11, 1) && data.Rows[^1].Date == new DateTime(2000, 2, 29), "Date sorting, invalid-date removal, and leap-day alignment");
    Check(result.CurrentYear == 2003 && result.HistoryYears.SequenceEqual(new[] { 2000, 2001, 2002 }), "Current year is excluded from historical average");
    Check(result.Average.SequenceEqual(new[] { 20d, 15d, 40d }), "Average skips missing data and ignores CSV Average column");
    Check(result.Heavier.SequenceEqual(new[] { 2001 }) && result.Lighter.SequenceEqual(new[] { 2000 }), "Strict heavier/lighter comparisons exclude tied current-year maxima");
    Check(result.Recent.SequenceEqual(new[] { 2000, 2001, 2002 }), "Recent category retains chronological order");
    Analysis filtered = ChartGenerator.Analyze(data, 2002);
    Check(filtered.HistoryYears.SequenceEqual(new[] { 2002 }) && double.IsNaN(filtered.Average[^1]), "Filtered history and all-missing average");
    Check(!filtered.HasLowerYears && filtered.Lighter.SequenceEqual(new[] { 2002 }), "Lighter category falls back when no historical maximum is lower");
    bool rejected = false;
    try { ChartGenerator.Analyze(data, 2003); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "Latest-year start is rejected without an empty-history crash");
    rejected = false;
    try { ChartGenerator.Analyze(data, 1999); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "Start years before the data range are rejected");

    int[] years = Enumerable.Range(2000, 8).ToArray();
    var row = new IceRow(new DateTime(2000, 1, 1), years.ToDictionary(y => y, y => y == 2007 ? 90d : 50d));
    Analysis ties = ChartGenerator.Analyze(new IceData(years, [row]), 2000);
    Check(ties.Lighter.SequenceEqual(new[] { 2000, 2001, 2002, 2003, 2004 }) && ties.Recent.SequenceEqual(new[] { 2002, 2003, 2004, 2005, 2006 }), "Five-year limits and stable tie ordering");

    File.WriteAllText(csv, "Date,2000,2001\n01-Nov,broken,10\n");
    rejected = false;
    try { ChartGenerator.ReadCsv(csv); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Malformed numeric values report an error");
    File.WriteAllText(csv, "Date,2000,2001\n01-Nov,10\n");
    rejected = false;
    try { ChartGenerator.ReadCsv(csv); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Malformed CSV row lengths report an error");
    Console.WriteLine("All source-level checks passed.");
}
finally { File.Delete(csv); }
