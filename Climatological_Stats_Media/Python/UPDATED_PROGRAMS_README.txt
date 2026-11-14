UPDATED ICE CONCENTRATION PROGRAMS

Run a regional Python script as before. Keep ice_reports.py in the same folder.
1. Enter a CSV filename in that folder, or a full path to another region's CSV.
2. Enter the start year (Enter uses the earliest year with observations).
3. Enter the target year (Enter uses the latest year with observations).
Results are saved in an output folder BESIDE THE SCRIPT BEING RUN, even when the
selected CSV is elsewhere. The output folder is created automatically.
The program prints the full output path.
Repeated runs receive _2, _3, etc. suffixes; existing reports are preserved.
The last line of every text report identifies the full CSV path.
Titles use the selected CSV name so another region's data is not mislabeled.

PROGRAMS
Climatology-... and rankings.py/rankings-2.py: top 10 highest and bottom 5 lowest
for the target year/season. Bottom 5 requires at most 5 zero years and at least
5 positive years for that period, as in the original climatology scripts.
Ties share rank (1 + count of strictly higher/lower values), including cutoff ties.
All higher/lower concentrations are listed. Missing values are excluded.
The report states the number of years/seasons available for each ranking.

Lake topten-...: top 10 text report for the selected target, with all higher years.
It no longer creates sorted_ice_concentration_by_date.csv. No supplied program
reads that export; old exports are left untouched. Average is never ranked as a year.

Weekly topTen.py: preserves the original November-May monthly seasonal-maximum
report, now with selectable CSV and start/target seasons and a source footer.
Weekly Climatology-... and rankings.py: same highest/lowest functionality as lakes.
Weekly topten-weekly.py: highest-only weekly report.

WEEKLY METHOD AND UNITS
CSV columns: DATE (YYYYMMDD), Season (e.g. 1995-96), Concentration (fraction 0-1).
Surrounding header spaces are accepted. Values are multiplied by 100 for reports.
The supplied Gulf files contain a season label 1999-20. The loader warns and
uses its starting year 1999 (displayed as 1999-00); source CSVs are unchanged.
The start/target year means the FIRST season year: 1995 means 1995-96.
Weekly comparisons use fixed Jan 01-07, Jan 08-14, etc. calendar bins, rather than
exact dates that drift with observation weekdays. Feb 29 shares Feb 28's bin.
Each season contributes its maximum when multiple observations fall in a bin.
Reports give the target observation date and describe this method explicitly.
No interpolation or invented zero observations are used.

DAILY FORMAT
Date plus four-digit year columns, optionally Average and other non-year columns.
Dates: dd-Mon, Mon-dd (including Feb-29), mm/dd, Mon dd, or mm-dd. Values are percentages 0-100.
Years are sorted numerically, not by their column order. Empty years are ignored.

HTML GRAPHS
The existing ...analysis.py programs now save four graphs in one standalone HTML
file. They prompt for the same CSV and years and exclude Average from calculations.
They need Plotly (python -m pip install plotly); text reports need no extra packages.
Charts use observed values only: mean, observations with ice, concentration sum,
and sample standard deviation. Partial years/seasons can affect comparisons.
Counts of weekly observations are not described as exact season duration.

ORIGINAL FAILURES
Weekly rankings.py expected year columns such as 2025, but the weekly files use
DATE/Season/Concentration rows, so its target-column test skipped every row.
South Labrador rankings.py also referred to the Gulf CSV filename.
Some lake scripts referred to nonexistent grtlks.csv or included Average in rankings.
The original weekly monthly program also failed on South Labrador's leading space
in its Concentration header; headers are now stripped.

INSTALLATION
The original Python files are backed up before replacement in a timestamped
_program_backups folder in the original root. CSVs and earlier reports are unchanged.
