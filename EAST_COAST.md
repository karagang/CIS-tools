# East Coast ice overlay charts — C#

This folder contains six Windows C# programs, their compiled executables, and
copies of the three regional CSV files. The chart logic is implemented in C#
using the Windows .NET Framework charting library. Python and NuGet packages
are not needed. Source is compatible with the .NET Framework compiler included
on this computer.

## Run

Open `Gulf`, `NewFoundLand`, or `SouthLabCoast` and double-click an `.exe`:

| Region | Interactive period selection | Automatic 1990/91 to latest data |
| --- | --- | --- |
| Gulf | `gulf-Overlay-both.exe` | `gulf-Overlay-from1990.exe` |
| Newfoundland | `NFLND-Overlay-both.exe` | `NFLND-Overlay-from1990.exe` |
| South Labrador Coast | `SLabCoast-Overlay-both.exe` | `SLabCoast-Overlay-from1990.exe` |

The `both` programs ask for a starting season, cutoff date, and CSV file. Enter
the season as `1990-91` or `1990/91` and the date as `20260630`. Press Enter at
the CSV prompt to use the regional CSV beside the executable. Relative CSV paths
are checked in the working directory, then beside the executable.

The `from1990` programs require no input. They read their local CSV and use all
available November–June observations from the 1990/91 season onward. Their titles
show `1990/91 - 2025/26` with the end season taken from the selected data.

Each program saves **two 1800 × 1200 PNGs** in its region's `output` folder before
opening two chart windows:

- `<executable-name>-single.png`: latest season and the supplied average,
  with earlier seasons in light gray.
- `<executable-name>-lastFive.png`: latest season plus the five previous
  available seasons, the supplied average, and earlier seasons in light gray.

Rerunning a program replaces its own two PNGs. Different programs have different
output names. Output paths are relative to the executable, so launching from
another working directory still saves in the correct region's `output` folder.
Closing either chart window leaves the other window open.

The included sample PNGs were generated using 1990/91 through the end of the
current CSV data (2025/26). Running either program updates its own sample PNGs.

For saving without opening windows, run from PowerShell, for example:

```powershell
.\Gulf\gulf-Overlay-from1990.exe --no-show
```

The `both` variant still asks for its inputs with `--no-show`. Errors are written
to the console and return exit code 1; successful runs return 0. Use PowerShell
when troubleshooting, so error messages remain visible.

## Data and plotting behavior

These CSVs are copies of the Python programs' input files at creation time.
Replace/update the CSV in the C# region folder when new data is available.
The original Python files and their input CSVs are independent of these copies.

Required columns: `DATE`, `Season`, `Concentration`, and `Average`. Dates use
`yyyyMMdd`; numeric fractions are converted to percentages. CSV headers may have
additional columns. Quoted fields, UTF-8 text, scientific notation, blank numeric
values, and `NaN` values are supported. Blank/NaN values become gaps in the lines.
CSV records must occupy one line each, as in the supplied datasets.

The average curve groups the supplied `Average` column by normalized calendar
date and takes its mean, matching the Python programs. It does not recompute a
climatological average from `Concentration`. November–December map to 2023 and
January–June to 2024 so leap-day observations are supported.

The charts use the same series and data as the Python versions; fonts, tick
spacing, and window controls follow Windows chart rendering. Historical CSV
season labels are preserved, including the existing `1999-20` label in the Gulf
CSV. Season filtering uses the first four digits, matching the Python programs.

## Rebuild after editing C# source

From this folder, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

`Build.ps1` builds all six executables in their regional folders with the local
Windows .NET Framework compiler. Close the chart programs before rebuilding.
The six small regional `.cs` files set the CSV, region names, and operating mode;
`Shared\OverlayCharts.cs` contains the shared CSV reader, chart logic, window
management, and PNG saving. Each executable includes that shared code and runs
independently; no shared DLL needs to be deployed.

## Verification

All six executables were compiled and run in `--no-show` mode, generating all
twelve PNGs. For each region, every plotted date and percentage, legend
membership, and y-axis limit was compared with the existing Python program for
the same period. Checks also covered leap-day normalization, a single available
season, and rejection of invalid starting-season input. PNG layout was visually
reviewed. Interactive chart-window behavior has not been manually exercised.

The Windows charting API used here is documented in
[Microsoft's Chart reference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.datavisualization.charting.chart?view=netframework-4.8).
