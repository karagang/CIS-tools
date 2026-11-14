# Calculation verification

Both versions were tested independently with the supplied CSV snapshots.

- C# was rebuilt with its included build script and passed its calculation tests.
- All five Python launchers and all four Python unit tests passed.
- Each version generated 24 charts and 10 English/French text reports.
- All annual means, medians, means, anomalies, ranks, region areas, latest dates,
  and week classifications matched across the five regions.
- Every Canadian Arctic merged concentration matched the independent Python
  weighted calculation. One unmatched Eastern date and four unmatched Western
  dates were excluded and disclosed in both reports.
- Source checksums were verified against the actual CSV bytes.
- Tests cover start-year filtering, zero values, repeated observations within a
  week, week boundaries, leap-day alignment, seasons crossing New Year, weighted
  merges, field aliases, duplicate/invalid dates, source overwrite protection,
  invalid concentration/area values, and invalid date ranges.

Example runs use start year 1991 and end date 20251126, except Northern Canadian
Waters, which uses 20260326 and optional seasonal window 0101–0326. Preview images
are snapshots of these runs. Run the programs with updated CSVs for newer results.

C# chart and interface renderings were inspected. Live Windows capture timed out,
so live UI interaction was not verified. Chart appearance differs between native
Windows drawing and matplotlib.

Repeat the tests with `python -m unittest discover -s . -v` from Python, or
`./test.ps1` from CSharp. Neither version downloads data automatically.
