ICE CLIMATOLOGY - STANDALONE C# EDITION
=====================================

QUICK START
1. Put IceClimatology.exe in a folder where you can save files.
2. Double-click it.
3. Enter the CSV filename or full path. A quoted path is accepted.
4. Choose the start year and target year, or press Enter for the defaults.
5. Select a report, or choose 5 to create all applicable reports.

One executable works with all nine regions and other data using the same formats.
This is a Windows x64 executable. It bundles its .NET runtime and chart library;
recipients do not need Python, a .NET installation, Plotly, or internet access.
The executable is unsigned.

OUTPUT LOCATION
Every report is saved in an automatically created output folder BESIDE THE EXE,
even when the CSV is elsewhere. The program prints each full output path.
Previous reports are preserved: subsequent files receive _2, _3, etc. suffixes.
Every text report ends with the full CSV path; HTML graphs also show it at the bottom.
CSV files are read only and are never changed. The folder containing the EXE must
be writable. To share it, copy IceClimatology.exe and any desired CSVs; the source
folder and build tools are not needed. README.txt and THIRD_PARTY_NOTICES.txt are
included in the shareable ZIP for reference and license information.

REPORT CHOICES (equivalents of the Python scripts)
1. Climatology: Climatology-...py and rankings.py/rankings-2.py.
   Reports top 10 highest and qualifying bottom 5 lowest for the target year.
   Bottom 5 requires at most 5 zero years and at least 5 positive years per period.
2. Top 10: lake topten-...py and weekly topten-weekly.py.
   Lists target-year top-ten periods and every year/season with higher coverage.
3. Monthly: weekly topTen.py.
   Highest and lowest 10 monthly seasonal maxima, November through May.
   This choice is offered for weekly CSVs only.
4. Analysis: ...analysis.py.
   Four interactive HTML graphs: mean, observations with ice, sum of observed
   concentrations, and sample standard deviation. Open the HTML file in a browser.
   Graphs are fully offline, with hover, zoom, pan, reset and image export.
5. All applicable reports.

RANKING AND COMPARISON RULES
Ties share rank: 1 plus the number of strictly higher/lower years or seasons.
Ties at a cutoff are included. Average/Median columns are not ranked as years.
Missing values are excluded, not replaced with zero. Each ranking states the
number of years/seasons with observations for that period.
The comparison includes the target year and all available years from start to target.
The default target is the latest year/season with usable observations.

DAILY CSV FORMAT (Great Lakes)
Date,1973,1974,...,2026,Average
01-Jan,12.4,15.6,...,25.0,18.2
Year columns may be in any order. Average and other non-year columns are ignored.
Dates: dd-Mon, Mon-dd, mm/dd, Mon dd, or mm-dd. Feb 29 is supported.
Values must be percentages from 0 to 100; blank/NA/N/A/NaN/null values are missing.

WEEKLY CSV FORMAT (Gulf, NE Newfoundland, South Labrador Coast)
DATE,Season,Concentration
20250101,2024-25,0.25
DATE accepts YYYYMMDD, YYYY-MM-DD, or mm/dd/yyyy. Header spaces are accepted.
Concentration must be a fraction from 0 to 1 and is multiplied by 100 for reporting.
The start/target year is the FIRST season year: 1995 means season 1995-96.
Weekly rankings compare fixed calendar bins: Jan 01-07, Jan 08-14, etc.
Feb 29 shares Feb 28's bin. If multiple observations occur in a season's bin,
the maximum is used. Missing bins are excluded; no values are interpolated.
The target observation's actual date is included. This matches the Python method.
The supplied Gulf CSVs contain season 1999-20; the loader warns and uses starting
year 1999, displayed as 1999-00. It does not change the CSV.

GRAPH INTERPRETATION
Statistics use available observations. Missing observations and partial seasons
can affect comparisons. A weekly count measures observations with ice, not exact
season duration. Concentration sums are percentage-days/percentage-observations,
not total ice area. Standard deviation is undefined for a single observation.

COMMAND-LINE USE (optional)
IceClimatology.exe --csv "C:\data\erie.csv" --start 1995 --target 2026 --mode all
IceClimatology.exe --csv "C:\data\weeklyGulf.csv" --start 1995 --target 2024 --mode climatology
Modes: climatology, topten, monthly, analysis, all.
Omitted settings are prompted for. --help prints usage instructions.
A bare CSV filename looks beside the EXE first, then in the working directory.
Errors return exit code 1; success/cancellation return 0.

REBUILDING (developers only)
Source is in Source\ and contains the complete C# implementation and chart asset.
Install a .NET 10 SDK and run BUILD.ps1. To specify a private SDK:
  .\BUILD.ps1 -DotNet 'C:\tools\dotnet\dotnet.exe'
This publishes a self-contained single-file win-x64 EXE and copies it here.
The initial build may need internet access for Microsoft runtime/SDK packages.
No NuGet application libraries or Python runtime are used by this application.
