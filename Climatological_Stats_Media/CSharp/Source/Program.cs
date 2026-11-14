using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace IceClimatology;

public static class Program
{
    private static readonly string[] Modes = { "climatology", "topten", "monthly", "analysis", "all" };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        // Use the same numeric/date conventions regardless of the recipient's locale.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        bool interactive = args.Length == 0;
        int exitCode = 0;
        try
        {
            var options = ParseOptions(args);
            if (options.ContainsKey("help")) { Console.WriteLine(Help); return 0; }
            Console.WriteLine("Ice Climatology - daily and weekly ice concentration reports\n");
            do
            {
                Run(options);
                if (!interactive || Console.IsInputRedirected) break;
                Console.Write("\nAnalyze another CSV? [y/N]: ");
                if (!ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase)) return 0;
            } while (true);
        }
        catch (OperationCanceledException) { Console.WriteLine("Cancelled."); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or ArgumentException or MalformedLineException or InvalidOperationException)
        {
            Console.Error.WriteLine("Error: " + error.Message);
            exitCode = 1;
        }
        if (interactive && !Console.IsInputRedirected)
        {
            Console.Write("\nPress Enter to close.");
            Console.ReadLine();
        }
        return exitCode;
    }

    private static void Run(Dictionary<string, string> options)
    {
        string path;
        IceData data;
        while (true)
        {
            bool provided = options.TryGetValue("csv", out string? entered);
            if (!provided) { Console.Write("CSV file to read (filename or full path): "); entered = ReadLine(); }
            try
            {
                if (string.IsNullOrWhiteSpace(entered)) throw new FormatException("Please enter a CSV filename.");
                path = ResolveCsv(entered);
                data = CsvLoader.Read(path);
                break;
            }
            catch (Exception error) when (!provided && error is IOException or UnauthorizedAccessException or FormatException or ArgumentException or MalformedLineException)
            {
                Console.WriteLine("Cannot use this CSV: " + error.Message);
            }
        }
        foreach (string warning in data.Warnings) Console.WriteLine("Warning: " + warning);
        int[] years = data.Observations.Select(o => o.Year).Distinct().Order().ToArray();
        string unit = data.Weekly ? "season starting year" : "year";
        Console.WriteLine($"Data type: {(data.Weekly ? "weekly (fractions converted to percent)" : "daily (percentages)")}");
        Console.WriteLine($"Available {unit}s: {years.First()} to {years.Last()}.");
        if (data.Weekly) Console.WriteLine("For example, 1995 includes season 1995-96. Start and target use the FIRST season year.");
        int start = AskYear("Start " + unit, "start", years.First(), years.First(), years.Last(), options);
        int target;
        while (true)
        {
            target = AskYear("Target " + unit + " to rank/analyze through", "target", years.Last(), start, years.Last(), options);
            if (years.Contains(target)) break;
            if (options.ContainsKey("target")) throw new FormatException("The target must have data.");
            Console.WriteLine("The target must have data.");
        }
        string mode = options.TryGetValue("mode", out var selected) ? selected.ToLowerInvariant() : AskMode(data.Weekly);
        if (!Modes.Contains(mode)) throw new FormatException("Unknown mode. Choose climatology, topten, monthly, analysis, or all.");
        if (mode == "monthly" && !data.Weekly) throw new FormatException("Monthly seasonal maxima require a weekly DATE/Season/Concentration CSV.");
        data = data with { Observations = data.Observations.Where(o => o.Year >= start && o.Year <= target).ToList() };
        string outputDir = Path.Combine(AppContext.BaseDirectory, "output");
        string[] modes = mode == "all"
            ? (data.Weekly ? Modes.Take(4).ToArray() : new[] { "climatology", "topten", "analysis" })
            : new[] { mode };
        foreach (string reportMode in modes)
        {
            string output = reportMode == "analysis"
                ? Charts.Write(path, data, start, target, outputDir)
                : Reports.WriteText(path, data, start, target, reportMode, outputDir);
            Console.WriteLine("Results saved to: " + output);
        }
    }

    private static string ResolveCsv(string entered)
    {
        string path = entered.Trim().Trim('"');
        if (path.StartsWith("~\\") || path.StartsWith("~/"))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        if (!Path.IsPathRooted(path))
        {
            string local = Path.Combine(AppContext.BaseDirectory, path);
            path = File.Exists(local) ? local : Path.Combine(Environment.CurrentDirectory, path);
        }
        return Path.GetFullPath(path);
    }

    private static int AskYear(string prompt, string key, int defaultYear, int minimum, int maximum, Dictionary<string, string> options)
    {
        while (true)
        {
            bool provided = options.TryGetValue(key, out string? text);
            if (!provided) { Console.Write($"{prompt} [{defaultYear}]: "); text = ReadLine(); }
            if (string.IsNullOrEmpty(text)) text = defaultYear.ToString(CultureInfo.InvariantCulture);
            if (int.TryParse(text, out int year) && year >= minimum && year <= maximum) return year;
            string message = $"Enter a year between {minimum} and {maximum}.";
            if (provided) throw new FormatException(message);
            Console.WriteLine(message);
        }
    }

    private static string AskMode(bool weekly)
    {
        Console.WriteLine("\nReport choices:");
        Console.WriteLine("  1  Climatology rankings (top 10 highest and bottom 5 lowest)");
        Console.WriteLine("  2  Top 10 highest with all higher years/seasons");
        if (weekly) Console.WriteLine("  3  Monthly seasonal maxima (November through May)");
        Console.WriteLine("  4  HTML graphs (four interactive charts; works offline)");
        Console.WriteLine("  5  All applicable reports");
        while (true)
        {
            Console.Write("Choose report [1]: ");
            string choice = ReadLine();
            if (choice.Length == 0) return "climatology";
            if (int.TryParse(choice, out int number) && number >= 1 && number <= 5 && (weekly || number != 3))
                return Modes[number - 1];
            Console.WriteLine("Please select an available report number.");
        }
    }

    private static string ReadLine() => Console.ReadLine()?.Trim() ?? throw new OperationCanceledException();

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--help" or "-h" or "/?") { options["help"] = ""; continue; }
            if (!arg.StartsWith("--"))
            {
                if (options.ContainsKey("csv")) throw new ArgumentException("Only one CSV file can be selected at a time.");
                options["csv"] = arg;
                continue;
            }
            string key = arg[2..];
            if (key is not ("csv" or "start" or "target" or "mode")) throw new ArgumentException("Unknown option: " + arg);
            if (++i >= args.Length) throw new ArgumentException("Missing value for " + arg);
            options[key] = args[i];
        }
        return options;
    }

    private const string Help = """
Ice Climatology - one executable for all daily and weekly regions

Double-click IceClimatology.exe and follow the prompts, or use:
  IceClimatology.exe --csv "C:\data\erie.csv" --start 1995 --target 2026 --mode all

Options:
  --csv PATH       Source CSV; a bare filename looks beside the executable first.
  --start YEAR     First comparison year; weekly: FIRST year of the season.
  --target YEAR    Target year/season and final comparison year.
  --mode MODE     climatology | topten | monthly | analysis | all
  --help          Show these instructions.

Omitted options are prompted for. Press Enter to accept a default year.
Outputs are always saved in output beside the executable. Previous reports are
preserved with _2, _3, etc. Monthly reports require the weekly CSV format.
Use a writable folder for the executable. No Python, .NET installation, chart
library installation, or internet connection is needed to run the published exe.
""";
}
