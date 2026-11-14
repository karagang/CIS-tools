using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace ArcticOverlay
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 0) { Application.Run(new MainForm()); return 0; }
            string report = null;
            try
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] allowed = { "--region", "--csv", "--east-csv", "--west-csv", "--start-year", "--end-date", "--output-dir", "--custom-start", "--custom-end", "--report", "--ui-preview" };
                for (int i = 0; i < args.Length; i += 2)
                {
                    if (i + 1 >= args.Length || !allowed.Contains(args[i])) throw new ArgumentException("Unknown option or missing value / Option inconnue ou valeur absente: " + args[i]);
                    values.Add(args[i], args[i + 1]);
                }
                if (values.ContainsKey("--report")) report = values["--report"];
                if (values.ContainsKey("--ui-preview"))
                {
                    using (var form = new MainForm())
                    {
                        form.Show(); Application.DoEvents();
                        using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
                        { form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(values["--ui-preview"], System.Drawing.Imaging.ImageFormat.Png); }
                        form.Close();
                    }
                    return 0;
                }
                if (!values.ContainsKey("--region")) throw new ArgumentException("--region is required / --region est requis.");
                var region = Region.All.FirstOrDefault(r => r.English == values["--region"] || r.Folder == values["--region"]);
                if (region == null) throw new ArgumentException("Unknown region / Région inconnue.");
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", region.Folder);
                Func<string, string, string> get = (key, fallback) => values.ContainsKey(key) ? values[key] : fallback;
                var options = new Options { Region = region, Merge = region.English == "Canadian Arctic" && !values.ContainsKey("--csv"),
                    Csv = get("--csv", Path.Combine(root, region.Csv)),
                    East = get("--east-csv", Path.Combine(root, "create Canadian Arctic CSV", "Eastern_Arctic.csv")),
                    West = get("--west-csv", Path.Combine(root, "create Canadian Arctic CSV", "Western_Arctic.csv")),
                    Output = Path.GetFullPath(get("--output-dir", Path.Combine(root, "output"))) };
                if (!options.Merge && (values.ContainsKey("--east-csv") || values.ContainsKey("--west-csv"))) throw new ArgumentException("East/West options require Canadian merge / Les options Est/Ouest exigent la fusion canadienne.");
                var source = Engine.Read(options.Merge ? options.East : options.Csv);
                DateTime latest = source.Rows.Max(r => r.Date);
                if (options.Merge)
                {
                    var west = Engine.Read(options.West).Rows.Select(r => r.Date).ToArray();
                    latest = source.Rows.Where(r => west.Contains(r.Date)).Max(r => r.Date);
                }
                options.StartYear = int.Parse(get("--start-year", source.Rows.Min(r => r.Date.Year).ToString()), Engine.Inv);
                options.End = DateTime.ParseExact(get("--end-date", latest.ToString("yyyyMMdd", Engine.Inv)), "yyyyMMdd", Engine.Inv);
                if (values.ContainsKey("--custom-start")) options.CustomStart = MainForm.ParseCustom(values["--custom-start"]);
                if (values.ContainsKey("--custom-end")) options.CustomEnd = MainForm.ParseCustom(values["--custom-end"]);
                Result result = Engine.Run(options);
                if (report != null) File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { success = true, median = result.Median, mean = result.Mean, area = result.Area,
                    rank = result.Rank, latest = Engine.Date(result.Latest), week = result.Week, years = result.Years, sources = result.Sources, files = result.Files, excluded = result.Excluded }), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception ex)
            {
                if (report != null) File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { success = false, error = ex.Message }), new UTF8Encoding(false));
                else File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "batch-error.txt"), ex.Message, new UTF8Encoding(false));
                return 1;
            }
        }
    }
}
