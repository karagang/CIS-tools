# Overlay Charts — East Coast and Great Lakes

This project now lives in **[CIS-tools / Overlay Charts](https://github.com/karagang/CIS-tools/tree/main/Overlay%20Charts)**. It includes the original source, data, example charts, downloadable packages, and program guides. The earlier catalogue website remains at its original address; to browse the included static catalogue offline, download the repository and open `Overlay Charts/docs/index.html` in a browser.

## Quick start

1. Choose a complete ZIP in [docs/downloads](docs/downloads).
2. Download it with GitHub’s **Download raw file** button and extract the entire ZIP.
3. Keep the executable or Python script together with its CSV inputs and configuration files.
4. Follow the included README; see [East Coast instructions](EAST_COAST.md) for the regional programs.

[Existing program catalogue website](https://karagang.github.io/east-coast-ice-charts/)

This repository publishes the ice-coverage chart programs developed in C# and
Python. Each catalogue page explains the program, shows a small plot preview,
links its source, and provides a complete download with the required folder
structure and CSV inputs.

## Programs

- Six East Coast C# programs: Gulf, Newfoundland, and South Labrador Coast,
  each with interactive-period and automatic-from-1990/91 modes.
- The same six East Coast Python programs.
- Six lake-specific C# overlay programs: Erie, Huron, Michigan, Ontario,
  Superior, and the combined Great Lakes; each makes three language variants.
- A Python batch companion for those six regions (eighteen language plots).
- Great Lakes twelve-chart batch programs in both C# and Python.
- The original generic combined-Great-Lakes generator in both languages,
  clearly labeled as the earlier version.

## Downloads and source

Use the individual [program guides](https://karagang.github.io/east-coast-ice-charts/#programs) for the right ZIP and commands.
All complete ZIPs are also in [docs/downloads](docs/downloads). Extract the full
ZIP rather than downloading an executable alone: programs need their CSVs and,
where applicable, configuration files.

The East Coast C# and Python source files are in `Gulf`, `NewFoundLand`, and
`SouthLabCoast`; the shared C# engine is in `Shared`. The root `Build.ps1`
rebuilds the six East Coast C# executables. [East Coast instructions](EAST_COAST.md).

The earlier C# sources are browsable in [OtherCSharpSources](OtherCSharpSources),
and their complete runnable/rebuildable distributions are in the ZIP downloads.
Other Python sources are browsable in [PythonSources](PythonSources), with their
complete data folders and instructions in the corresponding ZIPs. Original
sources are preserved; source snapshots in these two directories use unique
names for browsing, so use the ZIPs for their original layout and build scripts.

All chart images are example outputs from the included data. CSVs and previews
are snapshots; the website does not fetch new observations or run the programs.
The code uses the supplied Average values rather than deriving a new climatology.

## Catalogue

| Program | Language | Webpage |
| --- | --- | --- |
| gulf-Overlay-both | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/gulf-overlay-both.html) |
| gulf-Overlay-from1990 | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/gulf-overlay-from1990.html) |
| NFLND-Overlay-both | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/nflnd-overlay-both.html) |
| NFLND-Overlay-from1990 | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/nflnd-overlay-from1990.html) |
| SLabCoast-Overlay-both | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/slabcoast-overlay-both.html) |
| SLabCoast-Overlay-from1990 | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/slabcoast-overlay-from1990.html) |
| gulf-overlay-both-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/gulf-overlay-both-python.html) |
| gulf-overlay-from1990-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/gulf-overlay-from1990-python.html) |
| nflnd-overlay-both-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/nflnd-overlay-both-python.html) |
| nflnd-overlay-from1990-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/nflnd-overlay-from1990-python.html) |
| slabcoast-overlay-both-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/slabcoast-overlay-both-python.html) |
| slabcoast-overlay-from1990-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/slabcoast-overlay-from1990-python.html) |
| great-lakes-overlay-erie | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlay-erie.html) |
| great-lakes-overlay-ontario | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlay-ontario.html) |
| great-lakes-overlay-superior | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlay-superior.html) |
| great-lakes-overlay-huron | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlay-huron.html) |
| great-lakes-overlay-michigan | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlay-michigan.html) |
| great-lakes-overlay-great_lakes | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlay-great_lakes.html) |
| great-lakes-overlays-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-overlays-python.html) |
| great-lakes-batch-csharp | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-batch-csharp.html) |
| great-lakes-batch-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/great-lakes-batch-python.html) |
| combined-great-lakes-legacy-csharp | C# | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/combined-great-lakes-legacy-csharp.html) |
| combined-great-lakes-legacy-python | Python | [Guide](https://karagang.github.io/east-coast-ice-charts/programs/combined-great-lakes-legacy-python.html) |
