# Standalone C# version

This folder contains the Windows executable, its C# source and build files, and
independent copies of all regional CSV data in `Data/`. You can copy this entire
folder to another computer. It does not use the sibling Python folder.

Double-click `AnomalyCharts.exe` to run. Keep `AnomalyCharts.exe.config` and `Data/`
beside it. Requires Windows with .NET Framework 4.8; no Python, NuGet packages or
internet connection is needed. See `READ-ME.txt` for use and batch-mode details.

Run `./build.ps1` in PowerShell to rebuild `AnomalyCharts.exe` in this same folder
using the Windows .NET Framework C# compiler. The build uses only this folder's
source files and standard Windows framework libraries. CSV files are not replaced
when rebuilding. Update the files under `Data/` independently as needed.

The analysis compares one selected month/week across years from the start year
through the latest data. The median is calculated from the selected years. Outputs
default to `Data/<region>/output`, or the output folder selected in the interface.
Northern Canadian Waters additionally supports the four seasonal overlays and
English/French climatology summaries.

Native Windows rendering gives the charts a different appearance from Python.
Calculation results were checked against the Python implementation across all five
regions. The October–January overlay retains the original day-of-year convention.
