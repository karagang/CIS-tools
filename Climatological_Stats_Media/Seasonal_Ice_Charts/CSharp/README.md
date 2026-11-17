# Great Lakes ice charts — standalone C# edition

This source folder is independent of the Python edition. It contains all six C# source projects, CSV data, and example output folders. **The ready-to-run executables are in the [complete Windows download](https://github.com/karagang/CIS-tools/releases/download/great-lakes-seasonal-ice-charts-v1.0.0/Great_Lakes_Climatology_Charts_CSharp_Windows_x64.zip).** Extract that download to run the applications; alternatively, build this source folder using `Build.ps1`.

## Run the applications

1. Extract/copy this folder to a writable location on a **64-bit Windows 10 or Windows 11** computer.
2. Open the folder for the desired lake, or `combined_GRTLKS` for all Great Lakes.
3. Double-click the `*_IceCharts.exe` file.
4. Press Enter to use the CSV next to the executable, or supply another CSV path.
5. Enter the first historical year to include, or press Enter for the earliest available year.

Each application writes **eight PNG charts** into the `output` folder next to its executable: Combined, Recent, Heavier, and Lighter in both English (`EN`) and French (`FR`). Existing files with the same names are replaced. Combined charts are 1800 × 2700 pixels; individual charts are 1800 × 1050 pixels. Both use 150 DPI, as in the Python edition.

The executable includes the .NET runtime and its chart dependencies. **Users do not need Python, VS Code, Visual Studio, a .NET installation, or an internet connection to generate charts.** At first launch, the bundled runtime extracts native libraries under the user's temporary folder. These applications are unsigned; Windows or an organization's security policy may show a warning or restrict execution.

You can distribute an individual lake folder with its executable, CSV, `output` folder, and bundled `DOTNET-LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` files. Keep it writable. No files from the Python edition or the other lake folders are needed to run it. The `Shared` folder is needed only to rebuild from source.

## Folder contents

| Folder | Application | Default CSV |
| --- | --- | --- |
| `combined_GRTLKS` | `combined_GRTLKS_IceCharts.exe` | `bas_with_average.csv` |
| `Erie` | `Erie_IceCharts.exe` | `erie_with_average.csv` |
| `Huron` | `Huron_IceCharts.exe` | `huron_with_average.csv` |
| `Michigan` | `Michigan_IceCharts.exe` | `michigan_with_average.csv` |
| `Ontario` | `Ontario_IceCharts.exe` | `ontario_with_average.csv` |
| `Superior` | `Superior_IceCharts.exe` | `superior_with_average.csv` |

The CSV files are copies of the data supplied with the Python edition. Updating a CSV in one edition does not update the other. `output` initially contains charts generated during verification, using the earliest available start year.

## Calculations and chart appearance

The latest numeric year column is the current year. The chosen start year filters earlier years, excluding the current year from the average. Missing values (`NA`, `na`, `NaN`, and empty fields) are skipped when averaging and finding maxima. The CSV's existing `Average` column is ignored; the average is recalculated from the selected historical years.

The Recent panel uses the last five historical years. Heavier selects up to five years with maxima greater than the current year's maximum, highest first. Lighter selects up to five with lower maxima, lowest first. If none are lower, it uses the five lowest historical maxima. Other historical years appear as faint grey lines. The average is dashed green and the current year is red. Tied maxima retain chronological order. Missing values break the plotted lines.

Dates use a November 1–June 30 axis, with autumn dates in 1999 and winter/spring dates in leap year 2000. The vertical axis is fixed to 0–100%. French titles, legends, descriptions, and month labels are included.

These are native C# renderings, not pixel-identical Matplotlib copies. Colors, fonts, spacing, and text boxes may differ. The calculations, categories, filenames, image dimensions, and bilingual chart set follow the Python edition. The C# version also rejects a start year that leaves no historical data, rather than failing during average calculation.

## Command line

From a lake folder in PowerShell:

```powershell
.\Erie_IceCharts.exe --defaults
.\Erie_IceCharts.exe --defaults --start-year 2000
.\Erie_IceCharts.exe --defaults --csv "C:\Data\erie.csv" --output "C:\Charts\Erie"
.\Erie_IceCharts.exe --defaults --summary "calculations.json"
.\Erie_IceCharts.exe --help
```

`--defaults` disables prompts and the closing pause. Relative paths passed on the command line are relative to the working directory. The default CSV and default output folder are always relative to the executable, even if it is launched from another directory. Errors return exit code 1; success returns 0. `--summary` additionally writes the selected years, annual maxima, and daily calculated averages to JSON for checking the results.

## Build or modify the C# source

Only developers need the **.NET 10 SDK**. Open this folder in VS Code or a terminal, then run:

```powershell
.\Build.ps1
# Or specify a dotnet.exe outside PATH:
.\Build.ps1 -Dotnet 'C:\Path\To\dotnet.exe'
```

Each lake's `Program.cs` supplies its CSV name, filename prefix, and bilingual titles. `Shared/ChartGenerator.cs` contains the CSV handling, calculations, and native Windows chart rendering. Each `.csproj` references that shared source and pins `System.Drawing.Common` to version 10.0.15. Publishing creates a self-contained single-file `win-x64` executable. Building requires access to NuGet and the appropriate .NET runtime packs; running the published executable does not.

To run the source-level calculation and CSV checks:

```powershell
dotnet run --project Tests/GreatLakesCharts.Tests.csproj --configuration Release --artifacts-path "$env:TEMP\GreatLakesChartsTests"
```

The build script stages its intermediate files in a short temporary path to accommodate deeply nested Windows folders, copies the executables into their lake folders, and cleans its temporary build files.

## Verification

`VERIFICATION.json` records the completed checks and SHA-256 hashes of the CSV files and executables. All six applications were compared with the original Python calculations at the earliest available start year and at start year 2000: 12 comparisons covering selected years, annual maxima, and every daily calculated average. All 96 resulting PNGs passed filename, language, size, and DPI checks. Each application also passed a separate run after relocating only its executable and CSV, with Python and `dotnet` absent from the command path. Sample English and French charts were visually inspected.

The executables target Windows x64. They are not macOS, Linux, or native Windows ARM64 builds. For single-file .NET deployment details, see [Microsoft's deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
