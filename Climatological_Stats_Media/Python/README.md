# Python ice concentration programs

Each regional script asks for the CSV filename or full path, the first comparison year, and the target year. Press Enter to use the earliest and latest years with data. The CSV can come from another region if it uses a supported layout.

## Run a report

Extract the complete Python download so each script keeps its accompanying `ice_reports.py`. From this folder, for example:

```powershell
python "Erie/Climatology-erie-usrStartDate.py"
```

Enter `erie_with_average.csv` at the CSV prompt. Results are written in `Erie/output`, even if you select a CSV elsewhere. Existing reports are preserved with numbered suffixes. The last line of every text report shows the full CSV path.

Text reports need only Python 3 and its standard library; they were tested with Python 3.12. To create the HTML graphs, install the tested Plotly dependency:

```powershell
python -m pip install -r requirements.txt
python "Erie/erie-analysis.py"
```

## Regional inputs and climatology scripts

| Folder | Example source CSV | Climatology script |
| --- | --- | --- |
| Erie | `erie_with_average.csv` | `Climatology-erie-usrStartDate.py` |
| GRTLKS | `bas_with_average.csv` or `combined.csv` | `Climatology-GRTLKS-usrStartDate.py` |
| Huron | `huron_with_average.csv` | `Climatology-Huron-usrStartDate.py` |
| Michigan | `michigan_with_average.csv` | `Climatology-michigan-usrStartDate.py` |
| Ontario | `ontario_with_average.csv` | `Climatology-ontario-usrStartDate.py` |
| Superior | `superior_with_average.csv` | `Climatology-superior-usrStartDate.py` |
| Gulf of St Lawrence | `weeklyGulf-seasons-1969-2025.csv` | `Climatology-Gulf-of-St-Lawrence-usrStartDate.py` |
| NE Newfoundland | `weekly_NE-NFLND.csv` | `Climatology-NE-Newfoundland-usrStartDate.py` |
| South Labrador Coast | `weekly-SLab-Coast-1970-2025.csv` | `Climatology-South-Labrador-Coast-usrStartDate.py` |

For weekly CSVs, the start/target year is the first season year: `1995` means `1995-96`. Concentration fractions are converted to percentages. Weekly ranking bins and tie rules are explained in the [project guide](../README.md).

## Other scripts

- `rankings.py` and `rankings-2.py` use the same highest/lowest report as the climatology scripts.
- Lake `topten-*.py` and weekly `topten-weekly.py` generate top-ten text rankings and list all higher years.
- Weekly `topTen.py` retains the November–May monthly seasonal-maximum report.
- Lake `*analysis.py` generates four interactive HTML graphs in one offline file.

The former `sorted_ice_concentration_by_date.csv` exports are not needed as inputs and are not included. The download contains current programs and source CSV snapshots; generated reports, local backups, and caches are excluded. See [UPDATED_PROGRAMS_README.txt](UPDATED_PROGRAMS_README.txt) for additional format and calculation details.
