[← All programs](../README.md#program-catalogue)

Gulf of St. Lawrence / From 1990/91

# Gulf of St. Lawrence — From 1990/91

Create both charts from 1990/91 through the last season in the CSV, without entering any dates.

Windows C# · Two PNG charts · Bilingual chart labels

[Download the programs (.zip)](https://github.com/karagang/CIS-tools/raw/refs/heads/main/Overlay%20Charts/docs/downloads/east-coast-csharp.zip) · [View C# source](https://github.com/karagang/CIS-tools/blob/main/Overlay%20Charts/Gulf/gulf-Overlay-from1990.cs)

[Open the Python version guide →](gulf-overlay-from1990-python.md)

## What the program shows

Weekly ice coverage is plotted as a percentage, with each season aligned from November to June. Each run creates two views of the same data and saves them before opening the chart windows.

[![Gulf of St. Lawrence: latest season + average, 1990/91 through 2025/26](../assets/thumbs/gulf-Overlay-from1990-single.webp)](../assets/charts/gulf-Overlay-from1990-single.png)

**Latest season + average**

The latest season is orange-red, the supplied average is green, and earlier seasons form a light gray background. Select the image to see the full-size PNG.

[![Gulf of St. Lawrence: latest + five previous seasons, 1990/91 through 2025/26](../assets/thumbs/gulf-Overlay-from1990-lastFive.webp)](../assets/charts/gulf-Overlay-from1990-lastFive.png)

**Latest + five previous seasons**

The latest season is red. Five preceding available seasons have distinct colors, alongside the average and historical background. Select the image to see the full-size PNG.

These example plots use 1990/91–2025/26 from the included CSV. They are examples, not a live data feed.

## Run this program

Extract the ZIP first. Open the `Gulf` folder and double-click `gulf-Overlay-from1990.exe`, or run this command from the extracted package folder:

```powershell
.\Gulf\gulf-Overlay-from1990.exe
```

**Inputs**

No prompts. Reads `weeklyGulf-seasons.csv` beside the executable.

**Season range**

1990/91 through the latest season present in the CSV. The title updates automatically as new seasons are added.

**Saved charts**

`Gulf\output\gulf-Overlay-from1990-single.png`

`Gulf\output\gulf-Overlay-from1990-lastFive.png`

**Run again**

The program replaces its own two PNGs. Other programs' images have different filenames.

For saving without opening plot windows, append `--no-show`. No prompts appear.

```powershell
.\Gulf\gulf-Overlay-from1990.exe --no-show
```

## Data and requirements

Runs on Windows with .NET Framework and its charting library. Python and additional NuGet packages are not required. Keep the regional CSV beside the executable and replace it when new observations are available.

The green line uses the CSV's `Average` column, averaged by normalized calendar date. It does not calculate a new climatology from the concentration measurements. Historical season labels are preserved.

[Full setup and rebuild instructions](https://github.com/karagang/CIS-tools/blob/main/Overlay%20Charts/README.md) · [Shared charting source](https://github.com/karagang/CIS-tools/blob/main/Overlay%20Charts/Shared/OverlayCharts.cs)

[← All programs, source, and chart previews](../README.md)
