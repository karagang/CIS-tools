# Anomaly Charts

Sea ice anomaly analysis for five Canadian regions, available as Python scripts
and a standalone C# Windows application. Select a start year, month, and week to
compare that same weekly period across years through the latest available data.
The program saves a PNG chart and a text summary naming the region.

## Choose a version

| Version | Best for | Requirements |
| --- | --- | --- |
| [C# Windows app](CSharp/README.md) | Users who want an executable with dropdowns, CSV browsing, and chart previews | Windows with .NET Framework 4.8 |
| [Python scripts](Python/README.md) | Users who want scripts or command-line automation | Python 3, pandas, matplotlib, numpy |

Both versions have independent copies of the CSV files. Either folder can be
copied and used separately. Python is not required for the C# application.

## Run the Windows application

1. Download [CSharp.zip](CSharp.zip) using GitHub's **Download raw file** button.
2. Extract the whole ZIP into a writable folder on your computer.
3. Open the extracted `CSharp` folder and double-click **AnomalyCharts.exe**.
4. Choose a region. Use the supplied CSV or click **Browse** for updated data.
5. Choose the **start year**, **month**, and **week**, then click **Generate charts and summary**.
6. View the chart and summary in the app, or click **Open output folder**.

Keep the executable, its `.config` file, and the `Data` folder together. The CSVs
in the download are snapshots; replace them with newer files when available.
The source and [build script](CSharp/build.ps1) are included in the same folder.

## Run the Python version

Download the repository, open a terminal in the `Python` folder, and run:

```powershell
python -m pip install -r requirements.txt
python ".\NE Newfoundland\anomalyChart-NE-NFLND-selectable.py"
```

Follow the prompts for CSV filename, start year, month (1–12), and week (1–4).
Press Enter to accept a displayed default. For an unattended run:

```powershell
python ".\NE Newfoundland\anomalyChart-NE-NFLND-selectable.py" --csv weekly_NE-NFLND.csv --start-year 1991 --month 4 --week 3
```

See the [Python guide](Python/README.md) for all regional scripts.

## Regions and output files

| Region | Data folder |
| --- | --- |
| Gulf of St Lawrence | `Gulf` |
| NE Newfoundland | `NE Newfoundland` |
| South Labrador Coast | `South_Labrador_Coast` |
| Western Arctic | `Western Arctic` |
| Northern Canadian Waters | `Northern Canadian Waters` |

Results go into an `output` subfolder beside the selected CSV. In the Windows
package this is normally `CSharp/Data/<region>/output`. The C# interface also
allows another output folder. Filenames include the requested start year and the
latest observation for the selected week. Rerunning the same selection replaces
its previous output files.

For example, start year 1991, month April, week 3 compares the third week of April
in every available year from 1991 onward. It does not analyze every week between
April 1991 and today. No end date is requested. If the latest year has no data for
that week yet, the analysis ends with the latest year that does.

## Week definitions and calculations

| Week | Days of month |
| --- | --- |
| 1 | 1–7 |
| 2 | 8–14 |
| 3 | 15–21 |
| 4 | 22–31 |

Multiple observations in a year's selected month/week are averaged first. The
baseline is the median of those yearly means within the selected year range.
The anomaly is `(yearly mean − baseline median) × 100`, a difference in percentage
points. Years without observations are omitted. Changing the start year also
changes the years used for the baseline. CSV `Average` and `Median` columns are
not used for this calculation.

Required CSV columns are `DATE` (YYYYMMDD) and `Concentration` (a fraction from 0
to 1). Northern seasonal overlays also require a positive `Area` value in km².
Use the supplied CSVs as format examples and ensure the selected region matches
the CSV you choose.

## Northern Canadian Waters extras

The C# checkbox for additional Northern outputs creates four seasonal overlays
(April–December, July–December, October–December, and October–January), plus
English and French climatology summaries. The Python version provides these in
`Northern_CDN_Waters-anomaly_bilingual-selectable.py`.

The selected month/week controls the anomaly and climatology comparison.
Seasonal overlays include observations from January 1 of the chosen start year
through the latest CSV date, within each plot's seasonal window. They preserve
the original day-of-year and October–January conventions.

## Folder layout

```text
Anomaly Charts/
├── Python/
│   ├── anomaly_analysis.py
│   ├── requirements.txt
│   └── regional folders with scripts, CSVs, and output/
├── CSharp/
│   ├── AnomalyCharts.exe
│   ├── AnomalyCharts.exe.config
│   ├── AnomalyCharts.cs
│   ├── build.ps1
│   └── Data/
│       └── regional folders with CSVs and output/
└── CSharp.zip
```

## Verification and troubleshooting

The C# calculations were compared with the Python results across all five regions,
including duplicate observations, late-month weeks, invalid inputs, and single-year
selections. The standalone C# folder was also copied, rebuilt, and run separately.
Charts use native Windows rendering in C#, so their appearance differs from Python.

- **No data for the selected week:** select a month/week present in the CSV, or an earlier start year.
- **Cannot save outputs:** choose a writable output location and extract the ZIP before running.
- **Python import error:** install the packages in `Python/requirements.txt` with the same Python interpreter used to run the script.
- **Windows runtime requirement:** the executable needs .NET Framework 4.8.

Generated output folders, local test dependencies, and temporary verification files
are excluded from version control. Both versions include their own data snapshots;
updating one version's CSV files does not update the other.
