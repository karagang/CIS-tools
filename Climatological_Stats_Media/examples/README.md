# Sample outputs

These files are generated from the repository's CSV snapshots by the unmodified `Python/GRTLKS/ice_reports.py` implementation, shared by the regional entry scripts. PNG text previews typeset excerpts of the generated reports; chart PNGs export the actual Plotly figures used in the HTML output.

| Example | Regional entry script | CSV | Start | Target |
| --- | --- | --- | --- | --- |
| Top Ten | `GRTLKS/topten-greatLakes.py` | `GRTLKS/bas_with_average.csv` | 1973 | 2014 |
| Climatology | `GRTLKS/Climatology-GRTLKS-usrStartDate.py` | `GRTLKS/bas_with_average.csv` | 1973 | 2024 |
| Four analysis charts | `GRTLKS/grtlks-analysis.py` | `GRTLKS/bas_with_average.csv` | 1973 | 2024 |
| Monthly seasonal maxima | `South Labrador Coast/topTen.py` | `South Labrador Coast/weekly-SLab-Coast-1970-2025.csv` | 1970 | 2024 |

Scripts and CSV paths above are relative to `Python/`. For weekly data, the start and target are season starting years: 2024 selects 2024–25. Examples deliberately stop before the latest supplied year; they are demonstrations, not a current conditions report.

To reproduce a report, run its regional entry script and enter the CSV filename, start year, and target year shown above. Reports appear in `output/` beside the regional script. The example copies replace the machine-specific absolute CSV path with its repository-relative path; report values and explanatory text are unchanged.

To regenerate all documentation assets from the repository root:

```powershell
python -m pip install plotly==7.1.0 kaleido==1.5.1 Pillow
python Climatological_Stats_Media/examples/generate_examples.py
```

Kaleido requires Chrome for PNG export. The generator uses Segoe UI and Consolas on Windows, with DejaVu font fallbacks on other systems. It invokes the real interactive report entry function with the inputs above and exports its four figures at 1100 × 480 with a 1.5 scale factor. Ordinary report runs need neither Pillow nor Kaleido.

The HTML includes Plotly and works offline. Missing observations are excluded; partial years/seasons are not normalized. Concentration sums are not area measurements. See the [main guide](../README.md#data-and-comparison-methods) for ranking and weekly-bin methods.
