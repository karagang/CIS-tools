[Project overview and chart previews](../README.md)

# C# Arctic overlays and climatology

Double-click **ArcticOverlayCharts.exe** to start the Windows application.
Keep the executable, its `.config` file, `Data`, and `Maps` together.
Requires Windows with .NET Framework 4.8. Python, NuGet packages, and an internet
connection are not required. This folder has independent copies of the CSVs and maps.

## Generate results

1. Select a region. Browse to a replacement CSV if required, then click **Load / Charger**.
2. Choose the start year and end date. The default end date is the latest source observation.
3. Optionally select a custom seasonal window using MMDD dates, such as 0101 and 0326.
4. Choose the output folder, then click **Generate / Produire**.
5. Use the chart selector, English report, French report, and map tabs to review results.
   Click **Open output / Ouvrir les résultats** to view saved files.

Pour démarrer, ouvrez **ArcticOverlayCharts.exe**. Choisissez la région, les données,
l’année de début et la date de fin, puis cliquez sur **Generate / Produire**.
Les rapports anglais et français sont créés séparément. L’onglet **Map / Carte**
présente la région géographique. Aucun fichier de carte de la baie d’Hudson n’a été fourni.

The start year applies to all observations, seasonal medians, anomalies, and
report statistics. Zero concentration values are retained. All charts and reports
are saved in `Data/<region>/output` unless another output folder is selected.
Filenames include the CSV stem, requested start year, and requested end date.
Repeating a selection replaces its previous files. Empty seasonal windows are skipped.

## Canadian Arctic

The **Rebuild Canadian CSV** checkbox is enabled by default for Canadian Arctic.
It rebuilds `output/Canadian_Arctic.csv` from the Eastern and Western CSVs in
`Data/Canadian Arctic/create Canadian Arctic CSV`. Update these two files when
new data become available, or browse to replacement inputs in the interface.
Uncheck the box to analyze an existing combined CSV instead.

The merge uses matching dates only and rejects duplicate source dates. Area
weights are East 1,265,567 km² and West 1,296,172 km², as specified in the original
Python merge script. Concentration and shared ice fields are rounded to four
decimal places after weighting. Reports list unmatched date counts and all three
CSV paths and SHA-256 checksums.

## Calculations and outputs

This implementation follows the sibling Python version:

- Weekly statistics use the latest observed month/week against the same week in
  available years within the selected range. Weeks are days 1–7, 8–14, 15–21, 22–31.
- Multiple observations in one year/week are averaged before computing the median,
  mean, rank, or anomaly. Anomalies are percentage-point differences from the median.
  Ties share the lowest rank. Missing years are omitted.
- CSV `Average` and `Median` columns do not control the analysis.
- Seasonal medians use calendar month/day values across seasons, averaging
  duplicates within each season/day first. The fixed leap-year calendar keeps
  matching dates aligned and treats February 29 separately.
- October–January seasons are labeled by the October year. Partial seasons remain
  at selected range boundaries.
- Required input columns are `DATE` (YYYYMMDD), `Concentration` (fraction 0–1), and
  a constant positive `Area` in km². Blank area cells may inherit the unique region area.
  Invalid dates and concentrations cause an error.
- Every region produces April–December, July–December, October–December and a
  latest-week anomaly chart. Eastern and Northern also produce October–January;
  Northern additionally produces January–June. Custom seasons may cross New Year.
- Every run creates separate UTF-8 `_EN.txt` and `_FR.txt` reports. Their final
  section lists exact source CSV paths, SHA-256 checksums, and row counts.

The supplied Eastern 1973-present CSV has no area values. The default Eastern CSV
is the 1983-area version, which includes the required region area.
Charts use native Windows drawing, so their appearance differs from matplotlib.

## Batch mode

PowerShell examples, run from this folder:

```powershell
$app = Start-Process -FilePath .\ArcticOverlayCharts.exe -ArgumentList '--region "Hudson Bay" --start-year 1991 --end-date 20251126 --report "result.json"' -WindowStyle Hidden -PassThru -Wait
$app.ExitCode
Get-Content .\result.json
```

Options requiring values:

| Option | Purpose |
| --- | --- |
| `--region` | Canadian Arctic, Eastern Arctic, Western Arctic, Hudson Bay, Northern Canadian Waters |
| `--csv` | Input CSV; for Canadian Arctic, this bypasses automatic merging |
| `--east-csv`, `--west-csv` | Canadian merge input files |
| `--start-year` | First year included; default earliest source year in batch mode |
| `--end-date` | YYYYMMDD; default latest available date (common to both sources for merging) |
| `--output-dir` | Destination; default region's `output` folder |
| `--custom-start`, `--custom-end` | Optional paired MMDD dates |
| `--report` | JSON result file for automation and calculation verification |

Batch paths are relative to the current working folder unless absolute. Supplied
data defaults are resolved beside the executable. Exit code is 0 for success and
1 for errors. Without `--report`, batch errors go to `batch-error.txt` beside the
executable. With `--report`, check its `success` and `error` fields.

## Rebuild and verify

Run `./build.ps1` in PowerShell to rebuild using the Windows .NET Framework compiler.
Source files and manifest are included. Building does not replace data files.

Run `./test.ps1` to compile and run the included C# calculation tests.
All five supplied regional datasets were also compared against Python: annual
means, anomalies, medians, means, ranks, areas, every merged concentration, source
checksums and output paths matched. Sample runs produced 24 charts and 10 text
reports (start year 1991; end date 20251126, or 20260326 for Northern).

Map images are included in `Maps/` and in the local [map gallery](maps.html).
There is no supplied Hudson Bay map.
