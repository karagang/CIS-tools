# CIS Tools

Tools for producing Canadian sea ice charts from CSV observations.

| Project | Description | Start here |
| --- | --- | --- |
| [Anomaly Charts](Anomaly%20Charts/) | Compare a selected month/week across years, with Python scripts and an independent C# Windows app. | [Instructions](Anomaly%20Charts/README.md) · [Windows download](Anomaly%20Charts/CSharp.zip) |
| [Overlay Charts](Overlay%20Charts/) | Overlay historical ice coverage for East Coast regions and the Great Lakes, with C# and Python programs, data, examples, and downloadable packages. | [Catalogue and chart previews](Overlay%20Charts/docs/README.md) · [Instructions](Overlay%20Charts/README.md) · [Downloads](Overlay%20Charts/docs/downloads/) |
| [Arctic Overlay Charts and Climatology](Arctic%20Overlay%20Charts%20and%20Climatology/) | Arctic seasonal overlays, weekly anomalies, bilingual reports, Canadian Arctic merging and geographic maps, in C# and Python. | [Chart previews and instructions](Arctic%20Overlay%20Charts%20and%20Climatology/README.md) · [Windows download](Arctic%20Overlay%20Charts%20and%20Climatology/CSharp.zip) · [Python download](Arctic%20Overlay%20Charts%20and%20Climatology/Python.zip) |
| [Climatological Stats Media](Climatological_Stats_Media/) | Daily and weekly ice concentration rankings, top-ten reports, monthly seasonal maxima, and offline HTML graphs in Python and standalone C#. | [Instructions](Climatological_Stats_Media/README.md) · [Windows download](Climatological_Stats_Media/CSharp.zip) · [Python download](Climatological_Stats_Media/Python.zip) |

## Download and run

For **Anomaly Charts**, download `Anomaly Charts/CSharp.zip`, extract it, and open `CSharp/AnomalyCharts.exe`. Python is not required. Windows with .NET Framework 4.8 is required. The alternative Python scripts and their data are in `Anomaly Charts/Python`.

For **Overlay Charts**, choose the appropriate complete ZIP under `Overlay Charts/docs/downloads`, extract the entire package, and follow its included instructions. Executables require their accompanying CSV files.

All projects include descriptions, source code, data snapshots, and how-to instructions. Replace the supplied CSV snapshots with updated observations when needed. Each program's own guide explains its calculation method and output files.

For **Arctic Overlay Charts and Climatology**, download its complete C# or Python ZIP, extract it, and follow the included instructions. The [project page](Arctic%20Overlay%20Charts%20and%20Climatology/) shows chart examples and maps.

For **Climatological Stats Media**, download `Climatological_Stats_Media/CSharp.zip`, extract it, and open `IceClimatology.exe`. This Windows x64 executable needs no Python or separate .NET installation. The Python download supplies the regional scripts and CSV snapshots usable by either edition. Both editions prompt for the CSV and comparison years and save reports in an `output` folder beside the program.

## Repository layout

```text
CIS-tools/
├── Climatological_Stats_Media/
│   ├── CSharp/
│   ├── Python/
│   ├── CSharp.zip
│   └── Python.zip
├── Arctic Overlay Charts and Climatology/
│   ├── CSharp/
│   ├── Python/
│   ├── CSharp.zip
│   └── Python.zip
├── Anomaly Charts/
│   ├── CSharp/
│   ├── Python/
│   └── CSharp.zip
└── Overlay Charts/
    ├── Gulf/
    ├── NewFoundLand/
    ├── SouthLabCoast/
    ├── Great Lakes/
    ├── Shared/
    ├── PythonSources/
    └── docs/downloads/
```


