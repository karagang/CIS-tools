"""Shared seasonal overlays and bilingual weekly statistics for Arctic regions."""
from pathlib import Path
import argparse
import calendar
import hashlib
import io
import math
import pandas as pd

REGIONS = {
    'Canadian Arctic': ('Canadian Arctic', 'Arctique canadien', 'Canadian_Arctic.csv'),
    'Eastern Canadian Arrctic': ('Eastern Arctic', 'Arctique oriental', 'Eastern_Arctic-1983-area.csv'),
    'Western Arctic': ('Western Arctic', 'Arctique occidental', 'Western_Arctic-1983.csv'),
    'Hudson Bay': ('Hudson Bay', 'Baie d’Hudson', 'HudsonBay_area.csv'),
    'Northern_Canadian_Waters': ('Northern Canadian Waters', 'Eaux du Nord canadien', 'Northern_CDN_Waters.csv'),
}
MONTHS_FR = ['', 'janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet',
             'août', 'septembre', 'octobre', 'novembre', 'décembre']


def load_csv(path):
    """Hash the exact bytes parsed. Reject suspect data rather than silently omit it."""
    path = Path(path).resolve()
    raw = path.read_bytes()
    frame = pd.read_csv(io.BytesIO(raw))
    frame.columns = frame.columns.str.strip()
    if not {'DATE', 'Concentration'}.issubset(frame):
        raise ValueError(f'{path}: DATE and Concentration columns are required.')
    frame['DATE'] = pd.to_datetime(frame.DATE.astype(str).str.strip(), format='%Y%m%d', errors='raise')
    frame['Concentration'] = pd.to_numeric(frame.Concentration, errors='raise')
    if frame.empty or frame.DATE.isna().any() or not frame.Concentration.between(0, 1).all():
        raise ValueError(f'{path}: require valid dates and concentration fractions from 0 to 1 (including zero).')
    frame = frame.sort_values('DATE').reset_index(drop=True)
    return frame, (str(path), hashlib.sha256(raw).hexdigest(), len(frame))


def area_of(frame):
    if 'Area' not in frame:
        raise ValueError('CSV requires an Area column in km².')
    areas = pd.to_numeric(frame.Area, errors='raise').dropna().unique()
    if len(areas) != 1 or not math.isfinite(areas[0]) or areas[0] <= 0:
        raise ValueError('Area must contain a single positive finite region area in km².')
    return float(areas[0])


def merge_canadian(east_path, west_path, output):
    east, east_info = load_csv(east_path)
    west, west_info = load_csv(west_path)
    for frame in (east, west):
        if frame.DATE.duplicated().any():
            raise ValueError('Canadian Arctic merge requires unique dates in each source CSV.')
    # Fixed areas specified in the original create_CDN-Arctic-CSV.py.
    ea, wa = 1265567, 1296172
    joined = east.merge(west, on='DATE', suffixes=('_East', '_West'), validate='one_to_one')
    if joined.empty:
        raise ValueError('Eastern and Western sources have no matching dates.')
    result = pd.DataFrame({'Area': ea + wa, 'DATE': joined.DATE.dt.strftime('%Y%m%d')})
    # Normalize historical spellings before combining ice-type columns.
    aliases = {'First-Year Ice': 'First-Year-Ice', 'Young Ice': 'Young_Ice', 'New Ice': 'New_Ice'}
    east = east.rename(columns=aliases)
    west = west.rename(columns=aliases)
    joined = east.merge(west, on='DATE', suffixes=('_East', '_West'), validate='one_to_one')
    for col in ('Concentration', 'Old_Ice', 'First-Year-Ice', 'Young_Ice', 'New_Ice', 'Average', 'Median'):
        if col in east and col in west:
            a = pd.to_numeric(joined[col + '_East'], errors='raise')
            b = pd.to_numeric(joined[col + '_West'], errors='raise')
            result[col] = ((a * ea + b * wa) / (ea + wa)).round(4)
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    result.to_csv(output, index=False)
    details = (len(east) - len(joined), len(west) - len(joined))
    return [east_info, west_info], details


def week_of_month(day):
    return min((day - 1) // 7 + 1, 4)


def weekly_stats(frame):
    latest = frame.DATE.max()
    week = week_of_month(latest.day)
    subset = frame[(frame.DATE.dt.month == latest.month) &
                   (frame.DATE.dt.day.map(week_of_month) == week)]
    annual = subset.groupby(subset.DATE.dt.year).Concentration.mean().mul(100)
    return latest, week, annual


def write_reports(frame, region, start, end, output, stem, sources, merge_details=None):
    latest, week, annual = weekly_stats(frame)
    area = area_of(frame)
    median, mean = annual.median(), annual.mean()
    value = annual.loc[latest.year]
    rank = int(annual.rank(method='min').loc[latest.year])
    paths = []
    for french in (False, True):
        language = 'FR' if french else 'EN'
        name = region[1] if french else region[0]
        month = MONTHS_FR[latest.month] if french else calendar.month_name[latest.month]
        if french:
            lines = [f'Résumé climatologique hebdomadaire — {name}',
                     f'Année de début demandée : {start}', f'Date de fin demandée : {end:%Y-%m-%d}',
                     f'Période observée : {frame.DATE.min():%Y-%m-%d} au {latest:%Y-%m-%d}',
                     f'Semaine comparée : {week}, {month}',
                     'Semaines : 1 = jours 1–7; 2 = 8–14; 3 = 15–21; 4 = 22–31.',
                     f'Années comparées : {annual.index.min()}–{annual.index.max()} ({len(annual)} années présentes)',
                     'Les années sans observation pour cette semaine sont omises.',
                     'Calcul : moyenne par année pour cette semaine, puis médiane et moyenne de ces valeurs annuelles.',
                     'Les colonnes Average et Median du CSV ne sont pas utilisées.',
                     'Les anomalies sont des différences par rapport à la médiane, en points de pourcentage.',
                     f'Superficie de la région : {area:.0f} km²',
                     f'Valeur de {latest.year} (moyenne de la semaine) : {value:.2f} %',
                     f'Rang croissant (ex æquo au même rang) : {rank} sur {len(annual)}',
                     f'Médiane : {median:.2f} %', f'Moyenne : {mean:.2f} %',
                     f'Anomalie de {latest.year} : {value - median:+.2f} points de pourcentage',
                     f'Superficie englacée de {latest.year} : {value * area / 100:.2f} km²',
                     f'Superficie médiane : {median * area / 100:.2f} km²',
                     f'Écart à la médiane : {(value - median) * area / 100:+.2f} km²',
                     f'Superficie moyenne : {mean * area / 100:.2f} km²',
                     f'Écart à la moyenne : {(value - mean) * area / 100:+.2f} km²']
        else:
            lines = [f'Weekly climatology summary — {name}',
                     f'Requested start year: {start}', f'Requested end date: {end:%Y-%m-%d}',
                     f'Observed period: {frame.DATE.min():%Y-%m-%d} to {latest:%Y-%m-%d}',
                     f'Week compared: {week}, {month}',
                     'Weeks: 1 = days 1–7; 2 = 8–14; 3 = 15–21; 4 = 22–31.',
                     f'Years compared: {annual.index.min()}–{annual.index.max()} ({len(annual)} available years)',
                     'Years without observations for this week are omitted.',
                     'Calculation: yearly mean for this week, then median and mean of those yearly values.',
                     'CSV Average and Median columns are not used.',
                     'Anomalies are differences from the median in percentage points.',
                     f'Region area: {area:.0f} km²',
                     f'{latest.year} value (weekly mean): {value:.2f}%',
                     f'Rank from lowest (ties share rank): {rank} of {len(annual)}',
                     f'Median: {median:.2f}%', f'Mean: {mean:.2f}%',
                     f'{latest.year} anomaly: {value - median:+.2f} percentage points',
                     f'{latest.year} ice area: {value * area / 100:.2f} km²',
                     f'Median ice area: {median * area / 100:.2f} km²',
                     f'Difference from median: {(value - median) * area / 100:+.2f} km²',
                     f'Mean ice area: {mean * area / 100:.2f} km²',
                     f'Difference from mean: {(value - mean) * area / 100:+.2f} km²']
        for ascending in (True, False):
            label = ('Sept années les plus faibles :' if ascending else 'Sept années les plus élevées :') if french else ('Seven lowest years:' if ascending else 'Seven highest years:')
            lines += ['', label]
            for year, concentration in annual.sort_values(ascending=ascending, kind='stable').head(7).items():
                lines.append(f'  {year}: {concentration:.2f}% | {concentration * area / 100:.2f} km²')
        if merge_details is not None:
            lines += ['', ('Fusion : dates communes seulement; pondération Est 1265567 km², Ouest 1296172 km².' if french else
                           'Merge: matching dates only; weights East 1265567 km², West 1296172 km².'),
                      (f'Dates exclues faute de correspondance : Est {merge_details[0]}, Ouest {merge_details[1]}.' if french else
                       f'Unmatched dates excluded: East {merge_details[0]}, West {merge_details[1]}.')]
        lines += ['', 'Fichiers CSV utilisés (chemins complets, empreintes SHA-256) :' if french else
                  'CSV files used (full paths, SHA-256 checksums):']
        for path, digest, count in sources:
            lines += [path, f'SHA-256: {digest}', f'{"Lignes" if french else "Rows"}: {count}']
        path = output / f'Climatology_{stem}_{language}.txt'
        path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
        paths.append(path)
    return paths


def seasonal_data(frame, first, last):
    """Use a fixed leap-year calendar so the same month/day aligns in every year."""
    data = frame.copy()
    month_day = data.DATE.dt.strftime('%m%d').astype(int)
    wraps = first > last
    mask = ((month_day >= first) | (month_day <= last)) if wraps else month_day.between(first, last)
    data = data.loc[mask].copy()
    month_day = month_day.loc[mask]
    data['Season'] = data.DATE.dt.year - ((month_day <= last).astype(int) if wraps else 0)
    data['X'] = pd.to_datetime('2000' + data.DATE.dt.strftime('%m%d'), format='%Y%m%d').dt.dayofyear
    if wraps:
        data.loc[month_day <= last, 'X'] += 366
    return data


def draw_outputs(frame, region, output, stem, custom=None):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    area = area_of(frame)
    windows = [('Apr-Dec', 401, 1231), ('Jul-Dec', 701, 1231), ('Oct-Dec', 1001, 1231)]
    if region[0] in ('Northern Canadian Waters', 'Eastern Arctic'):
        windows += [('Oct-Jan', 1001, 131)]
    if region[0] == 'Northern Canadian Waters':
        windows += [('Jan-Jun', 101, 630)]
    if custom:
        windows += [(f'Custom_{custom[0]:04d}-{custom[1]:04d}', *custom)]
    for label, first, last in windows:
        data = seasonal_data(frame, first, last)
        if data.empty:
            print(f'No observations / Aucune observation: {label}')
            continue
        fig, ax = plt.subplots(figsize=(12, 8))
        try:
            recent = int(data.Season.max())
            colors = ['blue', 'purple', 'orange', 'brown', 'cyan', 'red']
            for year, group in data.groupby('Season'):
                group = group.sort_values('X')
                age = recent - year
                ax.plot(group.X, group.Concentration * 100,
                        color=colors[5 - age] if age <= 5 else 'lightgrey',
                        alpha=0.85 if age <= 5 else 0.35, linewidth=2 if age <= 5 else 1,
                        label=(str(year) + (f'–{year + 1}' if first > last else '')) if age <= 5 else None)
            # One value per season/day before taking a median across seasons.
            median = data.groupby(['Season', 'X']).Concentration.mean().groupby('X').median()
            ax.plot(median.index, median * 100, 'k--', linewidth=2, label='Median / Médiane')
            ax.set_ylim(0, 100)
            ax.set_ylabel('Ice coverage / Couverture de glace (%)')
            other = ax.secondary_yaxis('right', functions=(lambda x: x * area / 100 / 1e6,
                                                          lambda x: x * 100 * 1e6 / area))
            other.set_ylabel('Ice area / Superficie englacée (10⁶ km²)')
            def day(code):
                return pd.Timestamp(f'2000-{code // 100:02d}-{code % 100:02d}').dayofyear
            lower, upper = day(first), day(last) + (366 if first > last else 0)
            ticks, labels = [], []
            for offset in (0, 366):
                for month in range(1, 13):
                    tick = pd.Timestamp(2000, month, 15).dayofyear + offset
                    if lower <= tick <= upper:
                        ticks.append(tick)
                        labels.append(calendar.month_abbr[month] + '\n' + MONTHS_FR[month][:4])
            ax.set_xticks(ticks, labels)
            ax.set_xlim(lower, upper)
            ax.set_title(f'{region[0]} / {region[1]}\n{frame.DATE.min():%Y-%m-%d} – {frame.DATE.max():%Y-%m-%d} | {label}')
            ax.grid(alpha=0.3)
            ax.legend(title='Year / Année', loc='best')
            fig.tight_layout()
            fig.savefig(output / f'Ice_Concentration_{stem}_{label}.png', dpi=180)
        finally:
            plt.close(fig)
    latest, week, annual = weekly_stats(frame)
    anomalies = annual - annual.median()
    fig, ax = plt.subplots(figsize=(13, 7))
    try:
        ax.bar(annual.index, anomalies, color=['blue' if v >= 0 else 'red' for v in anomalies])
        ax.axhline(0, color='black', linewidth=1)
        ax.set_ylabel('Anomaly / Anomalie (percentage points / points de pourcentage)')
        ax.set_title(f'{region[0]} / {region[1]}\n{calendar.month_name[latest.month]} / {MONTHS_FR[latest.month]}, '
                     f'week / semaine {week} | {annual.index.min()}–{annual.index.max()}')
        ax.grid(axis='y', alpha=0.3)
        fig.tight_layout()
        fig.savefig(output / f'Ice_Anomaly_{stem}.png', dpi=180)
    finally:
        plt.close(fig)


def prompt(label, default):
    return input(f'{label} [{default}]: ').strip().strip('"') or str(default)


def run(folder, argv=None):
    folder = Path(folder).resolve()
    region = REGIONS[folder.name]
    parser = argparse.ArgumentParser(description=f'{region[0]} / {region[1]}')
    parser.add_argument('--csv', help='Input CSV, relative to script folder or absolute')
    parser.add_argument('--start-year', type=int)
    parser.add_argument('--end-date', help='YYYYMMDD; default latest CSV date')
    parser.add_argument('--output-dir', help='Output folder, relative to script folder or absolute')
    parser.add_argument('--east-csv', help='Canadian merge source; relative to script folder or absolute')
    parser.add_argument('--west-csv', help='Canadian merge source; relative to script folder or absolute')
    parser.add_argument('--custom-start', help='Optional seasonal window start MMDD')
    parser.add_argument('--custom-end', help='Optional seasonal window end MMDD')
    args = parser.parse_args(argv)
    def local(name):
        path = Path(name).expanduser()
        return path if path.is_absolute() else folder / path
    try:
        output = local(args.output_dir or 'output').resolve()
        custom = None
        if bool(args.custom_start) != bool(args.custom_end):
            raise ValueError('Supply both --custom-start and --custom-end (MMDD).')
        if args.custom_start:
            for value in (args.custom_start, args.custom_end):
                if len(value) != 4 or not value.isdigit():
                    raise ValueError('Custom dates must be MMDD.')
                pd.to_datetime('2000' + value, format='%Y%m%d', errors='raise')
            custom = (int(args.custom_start), int(args.custom_end))
        sources, details = [], None
        if folder.name == 'Canadian Arctic' and args.csv is None:
            east = local(args.east_csv or 'create Canadian Arctic CSV/Eastern_Arctic.csv')
            west = local(args.west_csv or 'create Canadian Arctic CSV/Western_Arctic.csv')
            csv_path = output / 'Canadian_Arctic.csv'
            if csv_path.resolve() in (east.resolve(), west.resolve()):
                raise ValueError('Merged CSV cannot overwrite a source CSV.')
            sources, details = merge_canadian(east, west, csv_path)
        else:
            if args.east_csv or args.west_csv:
                raise ValueError('--east-csv/--west-csv require Canadian Arctic without --csv.')
            csv_path = local(args.csv or prompt('CSV filename / Nom du fichier CSV', region[2]))
        frame, info = load_csv(csv_path)
        sources = [info] + sources
        start = args.start_year if args.start_year is not None else int(prompt('Start year / Année de début', frame.DATE.dt.year.min()))
        if not 1 <= start <= 9999:
            raise ValueError('Start year must be between 1 and 9999.')
        end_text = args.end_date or prompt('End date / Date de fin (YYYYMMDD)', frame.DATE.max().strftime('%Y%m%d'))
        if len(end_text) != 8 or not end_text.isdigit():
            raise ValueError('End date must be YYYYMMDD.')
        end = pd.to_datetime(end_text, format='%Y%m%d', errors='raise')
        frame = frame[(frame.DATE.dt.year >= start) & (frame.DATE <= end)].copy()
        if frame.empty:
            raise ValueError('No observations in the selected date range / Aucune observation dans la période choisie.')
        area_of(frame)
        output.mkdir(parents=True, exist_ok=True)
        stem = f'{csv_path.stem}_from_{start}_through_{end:%Y%m%d}'
        draw_outputs(frame, region, output, stem, custom)
        reports = write_reports(frame, region, start, end, output, stem, sources, details)
        print(f'Output folder / Dossier de sortie: {output}')
        for report in reports:
            print(report)
        print('CSV / Fichiers CSV:')
        for source, _, _ in sources:
            print(source)
    except (ValueError, OSError, KeyError) as exc:
        parser.exit(1, f'Error / Erreur: {exc}\n')
