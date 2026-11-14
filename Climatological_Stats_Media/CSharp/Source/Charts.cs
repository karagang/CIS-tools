using System.Net;
using System.Reflection;
using System.Text.Json;

namespace IceClimatology;

public sealed record Metric(string Title, string Unit, double?[] Values, bool Bars);

public static class Charts
{
    public static (int[] Years, int[] Counts, Metric[] Metrics) Calculate(IceData data)
    {
        var groups = data.Observations.GroupBy(o => o.Year).OrderBy(g => g.Key).ToArray();
        var values = groups.Select(g => g.Select(o => o.Value).ToArray()).ToArray();
        return (groups.Select(g => g.Key).ToArray(), values.Select(v => v.Length).ToArray(), new[] {
            new Metric("Mean observed ice concentration", "%", values.Select(v => (double?)v.Average()).ToArray(), false),
            new Metric("Observations with ice", data.Weekly ? "weekly observations" : "days", values.Select(v => (double?)v.Count(n => n > 0)).ToArray(), false),
            new Metric("Sum of observed ice concentrations", data.Weekly ? "percentage-observations" : "percentage-days", values.Select(v => (double?)v.Sum()).ToArray(), true),
            new Metric("Standard deviation of observed ice concentration", "percentage points", values.Select(StdDev).ToArray(), false)
        });
    }

    private static double? StdDev(double[] values)
    {
        if (values.Length < 2) return null;
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1));
    }

    public static string Write(string path, IceData data, int start, int target, string outputDir)
    {
        var (years, counts, metrics) = Calculate(data);
        string name = Path.GetFileNameWithoutExtension(path);
        string H(string value) => WebUtility.HtmlEncode(value);
        string payload = JsonSerializer.Serialize(new {
            labels = years.Select(y => Reports.Season(y, data.Weekly)),
            counts,
            xTitle = data.Weekly ? "Season" : "Year",
            metrics = metrics.Select(m => new { title = m.Title, unit = m.Unit, values = m.Values, bars = m.Bars })
        });
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("IceClimatology.Plotly")
            ?? throw new InvalidOperationException("The executable is missing its embedded chart library. Rebuild using BUILD.ps1.");
        using var reader = new StreamReader(resource);
        string plotly = reader.ReadToEnd();
        using var licenseResource = Assembly.GetExecutingAssembly().GetManifestResourceStream("IceClimatology.PlotlyLicense")!;
        using var licenseReader = new StreamReader(licenseResource);
        string plotlyLicense = licenseReader.ReadToEnd();
        string warnings = string.Join("", data.Warnings.Select(w => "<p>Warning: " + H(w) + "</p>"));
        string html = $$$"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{{{H(name)}}} - Ice concentration analysis</title>
<style>body{font:16px system-ui,sans-serif;color:#152c40;background:#f4f7fa;margin:0}main{max-width:1400px;margin:auto;padding:24px}h1{font-size:26px}.chart{height:460px;background:white;margin:22px 0;border:1px solid #dce4ed;border-radius:8px}p{line-height:1.5}.source{overflow-wrap:anywhere}button{padding:8px 14px;cursor:pointer}@media print{.chart{break-inside:avoid}button{display:none}}</style>
<!-- Plotly JavaScript license:
{{{plotlyLicense}}}
-->
<script>{{{plotly}}}</script></head><body><main>
<h1>{{{H(name)}}}: {{{start}}} to {{{target}}}</h1>
<p>Statistics use available observations only; missing values are not zero. Partial seasons are not adjusted for missing observations. Sums are not area measurements. Hover for values; drag to zoom and double-click to reset. The toolbar can save chart images.</p>
{{{warnings}}}
<button onclick="window.print()">Print report</button>
<div id="chart0" class="chart"></div><div id="chart1" class="chart"></div><div id="chart2" class="chart"></div><div id="chart3" class="chart"></div>
<script id="ice-chart-data" type="application/json">{{{payload}}}</script>
<script>
const report=JSON.parse(document.getElementById('ice-chart-data').textContent);
report.metrics.forEach((metric,index)=>{
  const trace={x:report.labels,y:metric.values,type:metric.bars?'bar':'scatter',mode:'lines+markers',customdata:report.counts,marker:{color:'#176b9d'},line:{color:'#176b9d'},hovertemplate:'%{x}<br>%{y:.4f} '+metric.unit+'<br>%{customdata} observations<extra></extra>'};
  Plotly.newPlot('chart'+index,[trace],{title:{text:metric.title},xaxis:{title:{text:report.xTitle},type:'category'},yaxis:{title:{text:metric.unit}},margin:{t:70,b:65,l:85,r:30},paper_bgcolor:'#ffffff',plot_bgcolor:'#ffffff'},{responsive:true,displaylogo:false});
});
</script>
<p class="source">CSV file used: {{{H(Path.GetFullPath(path))}}}</p>
</main></body></html>
""";
        return Reports.WriteUnique(outputDir, $"{name}_analysis_{target}_from_{start}.html", html);
    }
}
