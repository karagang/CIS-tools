# Great Lakes seasonal ice charts — Python edition

Six independent Python scripts produce English and French seasonal comparison charts for the five Great Lakes and the combined Great Lakes. Every lake folder contains its own program, CSV snapshot, and `output` folder. The scripts do not require the C# edition.

[Chart previews and both editions](../) · [Complete Python ZIP](https://github.com/karagang/CIS-tools/releases/download/great-lakes-seasonal-ice-charts-v1.0.0/Great_Lakes_Climatology_Charts_Python.zip)

## Install and run

Install Python 3.11 or newer. Extract the complete Python ZIP into a writable folder and open a terminal in `Great_Lakes_Climatology_Charts_Python`:

```powershell
python -m pip install -r requirements.txt
python "Erie/overlay-seasonEnd-Erie-bilang.py"
```

1. Press Enter to use `erie_with_average.csv` next to the script, or enter another CSV path.
2. Enter the start year for the historical comparison, or press Enter for the earliest available year. Choose a year **before the latest year in the CSV**, leaving at least one historical year for the average.
3. Wait for the eight saved-file messages, then open `Erie/output`.

To use VS Code, open this folder, select your installed Python interpreter, install the requirements in that interpreter, open a script, and select **Run Python File**. VS Code is optional.

## Scripts

| Folder | Program | Default CSV |
| --- | --- | --- |
| `combined_GRTLKS` | `overlay-seasonEnd-GRTLKS-bilang.py` | `bas_with_average.csv` |
| `Erie` | `overlay-seasonEnd-Erie-bilang.py` | `erie_with_average.csv` |
| `Huron` | `overlay-seasonEnd-Huron-bilang.py` | `huron_with_average.csv` |
| `Michigan` | `overlay-seasonEnd-Mich-bilang.py` | `michigan_with_average.csv` |
| `Ontario` | `overlay-seasonEnd-Ontario-bilang.py` | `ontario_with_average.csv` |
| `Superior` | `overlay-seasonEnd-Superior-bilang.py` | `superior_with_average.csv` |

For example, run the combined program from this folder with:

```powershell
python "combined_GRTLKS/overlay-seasonEnd-GRTLKS-bilang.py"
```

The default CSV and output folder are located beside the script, independently of your terminal's working directory. Output folders are created if missing. Long Windows output paths are supported.

## CSV input

The CSV must contain a `Date` column with day–English-month values such as `10-Nov`, followed by numeric year columns such as `1973` through `2026`. Ice coverage is a percentage, 0–100. `NA`, `na`, and empty fields represent missing observations. A precomputed `Average` column may be present; the programs ignore it and recalculate the average from the historical years you select.

The highest numeric year is the current year, shown in red. The average excludes that year and skips missing observations. Select a start year earlier than the current year. Supplied files are snapshots; replace or update the appropriate CSV to use newer observations.

## Charts and saved files

Each run writes Combined, Recent, Heavier, and Lighter PNGs in both `EN` and `FR`, for eight images in total. The three-panel Combined image is 1800 × 2700 pixels; individual images are 1800 × 1050 pixels, all at 150 DPI.

The current year is red, the calculated average is dashed green, comparison years are colored, and other historical years are light grey. Recent uses the last five historical years. Heavier and Lighter select up to five years by their annual maxima relative to the current year's maximum. See the [calculation guide](../README.md#how-to-interpret-the-charts) for selection details and the lighter-year fallback.

Filenames include the lake, latest CSV year, chart type, and language, for example `Erie_2026_Recent_FR.png`. The combined Great Lakes filename prefix is `Great_LakesIce`.

**Matching filenames are overwritten.** Save copies elsewhere before regenerating charts for a different start year. The CSV source files are read, not modified.

## Troubleshooting

- **Python is not recognized:** run the script using the full path to your Python executable, or select the correct interpreter in VS Code.
- **Missing pandas, matplotlib, or seaborn:** run `python -m pip install -r requirements.txt` using the same interpreter you use to run the program.
- **CSV not found:** press Enter for the default CSV, or provide an existing CSV path. Paths containing spaces may be quoted.
- **No historical years:** select a start year before the latest year column. The original Python program requires at least one historical year.
- **No write permission:** extract the package into a writable folder and close any application locking the output PNG files.

If you need a version without Python or dependency installation, use the [standalone Windows edition](../#downloads).
