# Python regional anomaly charts

## Python scripts

Open the regional folder and run its new `*-selectable.py` file with Python.
Enter the CSV filename (press Enter for the supplied regional CSV), start year,
month number (1–12), and week number (1–4). Press Enter to accept a displayed default.

The program compares **one selected month/week across years**, starting with your
chosen year. It uses all available observations through the latest data in the CSV;
there is no end-date prompt. If that week has not occurred in the latest year,
the comparison ends with the latest year that has observations for that week.

Week definitions are unchanged:

| Week | Days of month |
| --- | --- |
| 1 | 1–7 |
| 2 | 8–14 |
| 3 | 15–21 |
| 4 | 22–31 |

Each year's concentration is the mean of observations in that month/week.
The baseline is the median of those yearly means **within the selected year range**.
Anomalies are the difference from that baseline, multiplied by 100.
The CSV's existing Average and Median columns are not used, matching the original
anomaly calculation. Years without observations are omitted.

Charts and UTF-8 text summaries go into the regional folder's `output` subfolder,
regardless of the directory from which Python is launched. The summaries name the
region. Anomaly filenames include the start year and latest selected observation.
Plots are saved automatically without opening a blocking chart window.

## Regional scripts

| Folder | Script |
| --- | --- |
| Gulf | `anomalyChart-Gulf-selectable.py` |
| NE Newfoundland | `anomalyChart-NE-NFLND-selectable.py` |
| South_Labrador_Coast | `anomalyChart-SLab-selectable.py` |
| Western Arctic | `anomalyChart-Western_Arctic-selectable.py` |
| Northern Canadian Waters | `anomalyChart-Northern_CDN_Waters-selectable.py` |

Northern Canadian Waters also includes
`Northern_CDN_Waters-anomaly_bilingual-selectable.py`. It creates the selected-week
anomaly chart and summary, the four original seasonal overlay plots, and English
and French climatology summaries for the selected week. The seasonal overlays
include all observations from January 1 of the chosen start year through the latest
CSV date within each plot's seasonal window; the selected month/week controls the
anomaly and climatology comparisons. Overlay titles use the chosen year range.

## Requirements and command-line use

Use Python 3 with pandas, matplotlib, and numpy installed:

```powershell
python -m pip install -r requirements.txt
python ".\NE Newfoundland\anomalyChart-NE-NFLND-selectable.py"
```

For a run without prompts, supply all four options:

```powershell
python ".\NE Newfoundland\anomalyChart-NE-NFLND-selectable.py" --csv weekly_NE-NFLND.csv --start-year 1991 --month 4 --week 3
```

CSV filenames are resolved relative to the regional script's folder. An absolute
CSV path can be supplied to use a newer file directly from the USB drive.

The CSVs here are supplied data snapshots. Replace them with updated data as
needed, or supply an absolute path at the CSV prompt. Keep
`anomaly_analysis.py` in the parent Python folder
when moving the regional folders. No helper preparation script is needed to run them.

