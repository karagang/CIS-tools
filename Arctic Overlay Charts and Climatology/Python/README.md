[Project overview and chart previews](../README.md)

# Arctic overlay charts / Graphiques superposés de l’Arctique

Seasonal ice coverage overlays, a latest-week anomaly chart, and separate English
and French climatology reports for five regions. This folder is a self-contained
updated copy of the supplied Arctic scripts and data. Original programs are in
`reference_scripts` for reference; run the regional launchers listed below.

## Run / Exécution

Install Python 3.10 or later, then open a terminal in this folder:

```powershell
python -m pip install -r requirements.txt
python ".\Hudson Bay\HudsonBay-3Plots-zoomed2.py"
```

The program asks for the CSV filename, start year, and end date (YYYYMMDD).
Press Enter for the displayed default. Paths are relative to the script's region
folder, regardless of the terminal's current folder. The end date defaults to the
latest observation in the CSV. Nothing is downloaded automatically.

Le programme demande le nom du fichier CSV, l’année de début et la date de fin
(AAAAMMJJ). Appuyez sur Entrée pour accepter la valeur proposée. Les chemins
relatifs sont résolus depuis le dossier du script régional.

For an unattended run / Pour une exécution sans interaction:

```powershell
python ".\Hudson Bay\HudsonBay-3Plots-zoomed2.py" --csv HudsonBay_area.csv --start-year 1991 --end-date 20251126
python ".\Canadian Arctic\CanadianArctic-3Plots-zoomed2.py" --start-year 1991 --end-date 20251126
```

| Region / Région | Launcher / Script |
| --- | --- |
| Canadian Arctic / Arctique canadien | `Canadian Arctic/CanadianArctic-3Plots-zoomed2.py` |
| Eastern Arctic / Arctique oriental | `Eastern Canadian Arrctic/Eastern_CDN_Arctic.py` |
| Western Arctic / Arctique occidental | `Western Arctic/Western_Arctic-3Plots-climatology.py` |
| Hudson Bay / Baie d’Hudson | `Hudson Bay/HudsonBay-3Plots-zoomed2.py` |
| Northern Canadian Waters / Eaux du Nord canadien | `Northern_Canadian_Waters/Northern_CDN_Waters.py` |

The Eastern folder retains its original spelling, `Eastern Canadian Arrctic`.
The Eastern default is `Eastern_Arctic-1983-area.csv`, which has a valid region
area. Its alternative 1973-present CSV has no area values and requires correction
before it can be used for area charts. CSV snapshots may end on different dates.

## Outputs / Résultats

Every chart and report goes into the regional `output` subfolder. Override it
with `--output-dir "D:\my-results"`. Filenames include the input CSV stem,
requested start year and end date. Repeating the same selection replaces its outputs.
Each run produces separate UTF-8 `_EN.txt` and `_FR.txt` reports, a weekly anomaly
bar chart and April–December, July–December and October–December overlays.
Eastern and Northern also have October–January; Northern has January–June.
An empty seasonal window is skipped with a message. Add a custom window with
`--custom-start 0101 --custom-end 0326` (MMDD); windows crossing New Year work too.

Tous les graphiques et rapports sont enregistrés dans le sous-dossier régional
`output`. Chaque exécution produit deux rapports distincts, `_EN.txt` et `_FR.txt`.
Les noms incluent le fichier source et la période demandée. Les fichiers existants
de la même sélection sont remplacés. Une fenêtre saisonnière sans données est omise.

## Calculations / Calculs

All observations from January 1 of the chosen start year through the chosen end
date are used, including zero concentration. Invalid dates/concentrations cause
an error. Required columns: DATE (YYYYMMDD), Concentration (fraction 0–1), and a
constant positive Area in km² (blank area cells may inherit the region's unique value).

Statistics compare the latest observed month/week against that same month/week
in available years within the selected range. Weeks are days 1–7, 8–14, 15–21,
22–31. Multiple observations per year/week are averaged first. Medians, means,
rankings, and anomalies use these yearly means, including the latest year.
Anomalies are percentage-point differences from the median. Missing years are
omitted; ties share the lowest rank. The reports list the seven lowest/highest years.
CSV Average and Median columns are not used. The seasonal median is calculated
across seasons for each calendar month/day, averaging duplicates within a season.
Dates align on a fixed leap-year calendar; February 29 stays separate. October–January
seasons are labeled by the October year; partial seasons at range boundaries remain.
Chart titles show observed dates. This removes the original hard-coded year labels.

La période choisie s’applique aux courbes, aux médianes et aux statistiques. Les
valeurs nulles de concentration sont conservées. Les statistiques comparent la
semaine du mois de la dernière observation aux mêmes semaines des années présentes.
Les observations d’une même semaine et année sont moyennées avant les calculs.
Les anomalies sont exprimées en points de pourcentage par rapport à la médiane.
Les rapports indiquent les années présentes et les fichiers utilisés avec leur
chemin complet et leur empreinte SHA-256 pour vérifier les données exactes.

## Canadian Arctic merge / Fusion de l’Arctique canadien

By default, the Canadian launcher rebuilds `output/Canadian_Arctic.csv` from
`create Canadian Arctic CSV/Eastern_Arctic.csv` and `Western_Arctic.csv` on every
run. These are the sources specified by the supplied merge instructions. Only
matching dates are included; duplicate dates in either source are rejected.
Weights from the original script are Eastern 1,265,567 km² and Western 1,296,172 km²
(total 2,561,739 km²). Concentration and shared ice-type fields are area weighted
and rounded to four decimals. The reports record excluded unmatched date counts,
the merged CSV and both inputs, including SHA-256 checksums. Update these two
source CSVs when new data become available. The root regional CSVs are separate snapshots.

Use `--east-csv PATH --west-csv PATH` to choose merge inputs. Use `--csv PATH` to
analyze an already merged Canadian CSV instead. The original manual merge script
is preserved in `reference_scripts`.

Par défaut, le script canadien recrée le CSV combiné dans `output` à chaque
exécution. La fusion conserve les dates communes et utilise les superficies de
l’Est et de l’Ouest comme pondérations. Les rapports indiquent les dates exclues
et les trois fichiers CSV utilisés. L’option `--csv` permet d’utiliser directement
un CSV déjà combiné.

## Geographic areas / Régions géographiques

These are the supplied geographic reference maps. They appear on this page in
GitHub and in the local `maps.html` page. No Hudson Bay map was supplied.
Cartes de référence fournies : aucune carte de la baie d’Hudson n’était présente.

### Canadian Arctic / Arctique canadien

![Canadian Arctic / Arctique canadien](<Canadian Arctic/Canadian_Arctic_map/Canadian_Arctic_highlighted.png>)

### Eastern Arctic / Arctique oriental

![Eastern Arctic / Arctique oriental](<Eastern Canadian Arrctic/Eastern_Arctic_map/Eastern_Arctic_highlighted.png>)

### Western Arctic / Arctique occidental

![Western Arctic / Arctique occidental](<Western Arctic/Western Arctic  Map/Western_Arctic_highlighted.png>)

### Northern Canadian Waters / Eaux du Nord canadien

![Northern Canadian Waters / Eaux du Nord canadien](<Northern_Canadian_Waters/Northern Canadian Waters Map/northern_canada_highlighted.png>)
