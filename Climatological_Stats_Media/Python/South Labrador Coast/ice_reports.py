"""Shared CSV loading and reports. Text reports use only the Python standard library."""
import calendar
import csv
from datetime import datetime, date, timedelta
from html import escape
import math
from pathlib import Path
import re
import statistics


def number(value):
    text = str(value or '').strip()
    if text.lower() in ('', 'na', 'nan', 'n/a', 'null'):
        return None
    result = float(text)
    if not math.isfinite(result):
        return None
    return result


def read_data(path):
    """Return observations as (period, year/season, percentage, actual date)."""
    with Path(path).open(newline='', encoding='utf-8-sig') as stream:
        reader = csv.DictReader(stream)
        if not reader.fieldnames:
            raise ValueError('The CSV is empty.')
        headers = [h.strip() for h in reader.fieldnames]
        if len(set(headers)) != len(headers):
            raise ValueError('Duplicate CSV column names.')
        rows = []
        for line, row in enumerate(reader, 2):
            if None in row or any(row[h] is None for h in reader.fieldnames):
                raise ValueError(f'Line {line}: CSV row has the wrong number of fields.')
            rows.append(dict(zip(headers, (row[h] for h in reader.fieldnames))))
    lookup = {h.lower(): h for h in headers}
    years = sorted((h for h in headers if re.fullmatch(r'\d{4}', h)), key=int)
    observations = []
    if years and 'date' in lookup:
        weekly = False
        for line, row in enumerate(rows, 2):
            raw = row[lookup['date']].strip()
            if not raw:
                continue
            parsed = None
            for fmt in ('%d-%b', '%b-%d', '%m/%d', '%b %d', '%m-%d'):
                try:
                    parsed = datetime.strptime(raw + ' 2000', fmt + ' %Y')
                    break
                except ValueError:
                    pass
            if parsed is None:
                raise ValueError(f'Line {line}: unsupported date {raw!r}; use dd-Mon or mm/dd.')
            period = parsed.strftime('%m-%d')
            for year in years:
                value = number(row[year])
                if value is not None:
                    if not 0 <= value <= 100:
                        raise ValueError(f'Line {line}: concentration outside 0 to 100%.')
                    observations.append((period, int(year), value, raw))
    elif {'date', 'season', 'concentration'} <= lookup.keys():
        weekly = True
        warned_seasons = set()
        for line, row in enumerate(rows, 2):
            if not any(str(v or '').strip() for v in row.values()):
                continue
            raw = row[lookup['date']].strip()
            observed = None
            for fmt in ('%Y%m%d', '%Y-%m-%d', '%m/%d/%Y'):
                try:
                    observed = datetime.strptime(raw, fmt).date()
                    break
                except ValueError:
                    pass
            if observed is None:
                raise ValueError(f'Line {line}: invalid DATE {raw!r}.')
            season = row[lookup['season']].strip()
            match = re.fullmatch(r'(\d{4})-(\d{2}|\d{4})', season)
            if not match:
                raise ValueError(f'Line {line}: invalid Season {season!r}.')
            start = int(match[1])
            if int(match[2]) != ((start + 1) % 100 if len(match[2]) == 2 else start + 1) and season not in warned_seasons:
                print(f'Warning: Season {season!r} has an inconsistent ending year; using its starting year {start}. Source CSV is unchanged.')
                warned_seasons.add(season)
            if observed.year not in (start, start + 1):
                raise ValueError(f'Line {line}: DATE falls outside its Season.')
            value = number(row[lookup['concentration']])
            if value is not None:
                if not 0 <= value <= 1:
                    raise ValueError(f'Line {line}: weekly Concentration must be a fraction from 0 to 1.')
                # Fixed calendar bins avoid weekday drift and leap-year shifts.
                # Feb 29 shares Feb 28's position in a 365-day reference year.
                reference = date(2001, observed.month, min(observed.day, 28) if observed.month == 2 else observed.day)
                period = (reference.timetuple().tm_yday - 1) // 7
                observations.append((period, start, value * 100, observed))
    else:
        raise ValueError('Expected Date plus year columns, or DATE, Season, Concentration. Sorted export CSVs are not source data.')
    if not observations:
        raise ValueError('No usable ice concentration observations found.')
    seen = set()
    for period, year, value, observed in observations:
        key = (year, observed if weekly else period)
        if key in seen:
            raise ValueError(f'Duplicate observation for {year}, {observed}.')
        seen.add(key)
    return observations, weekly


def ask_year(prompt, years, default):
    while True:
        answer = input(f'{prompt} [{default}]: ').strip()
        try:
            result = int(answer) if answer else default
            if min(years) <= result <= max(years):
                return result
        except ValueError:
            pass
        print(f'Enter a year between {min(years)} and {max(years)}.')


def select_data(script_dir):
    while True:
        entered = input('CSV file to read (filename or full path): ').strip().strip('"')
        if not entered:
            print('Please enter a CSV filename.')
            continue
        path = Path(entered).expanduser()
        if not path.is_absolute():
            local = Path(script_dir) / path
            path = local if local.is_file() else Path.cwd() / path
        path = path.resolve()
        try:
            data, weekly = read_data(path)
            break
        except (OSError, ValueError, TypeError, csv.Error) as error:
            print(f'Cannot use this CSV: {error}')
    years = sorted({r[1] for r in data})
    unit = 'season starting year' if weekly else 'year'
    print(f'Available {unit}s: {years[0]} to {years[-1]}.')
    if weekly:
        print('For example, enter 1995 to include season 1995-96 onward. Weekly Concentration is converted from fractions to percent.')
    start = ask_year('Start ' + unit, years, years[0])
    target = ask_year('Target ' + unit + ' to rank/analyze through', years, years[-1])
    while target < start or target not in years:
        print('The target must have data and be at or after the start year.')
        target = ask_year('Target ' + unit, years, years[-1])
    return path, [r for r in data if start <= r[1] <= target], weekly, start, target


def season_name(year, weekly):
    return f'{year}-{str(year + 1)[-2:]}' if weekly else str(year)


def period_name(period, weekly):
    if not weekly:
        return datetime.strptime('2000-' + period, '%Y-%m-%d').strftime('%b %d')
    first = date(2001, 1, 1) + timedelta(days=period * 7)
    last = min(first + timedelta(days=6), date(2001, 12, 31))
    return f'{first:%b %d} to {last:%b %d}'


def ordinal(n):
    return str(n) + ('th' if 11 <= n % 100 <= 13 else {1: 'st', 2: 'nd', 3: 'rd'}.get(n % 10, 'th'))


def ranking_lines(data, weekly, target, include_low=True):
    groups = {}
    for period, year, value, observed in data:
        groups.setdefault(period, {}).setdefault(year, []).append((value, observed))
    lines = []
    for period, by_year in sorted(groups.items()):
        if target not in by_year:
            continue
        # If a bin has multiple observations in a season, compare maxima for all seasons.
        pairs = {year: max(items, key=lambda item: item[0]) for year, items in by_year.items()}
        value, observed = pairs[target]
        higher = sorted(((y, v[0]) for y, v in pairs.items() if v[0] > value), key=lambda p: (-p[1], p[0]))
        lower = sorted(((y, v[0]) for y, v in pairs.items() if v[0] < value), key=lambda p: (p[1], p[0]))
        checks = [('highest', higher, 10)]
        if include_low and sum(v[0] == 0 for v in pairs.values()) <= 5 and sum(v[0] > 0 for v in pairs.values()) >= 5:
            checks.append(('lowest', lower, 5))
        for direction, others, limit in checks:
            rank = len(others) + 1
            if rank > limit:
                continue
            tied = sum(v[0] == value for y, v in pairs.items() if y != target)
            label = period_name(period, weekly)
            if weekly:
                label += f' (observed {observed:%Y-%m-%d})'
            lines.append(f'{label}: {value:.2f}%, {"tied " if tied else ""}{ordinal(rank)} {direction}, {season_name(target, weekly)}; {len(pairs)} years/seasons with data')
            if others:
                lines.append(f'  {"Higher" if direction == "highest" else "Lower"} concentrations in this period:')
                lines.extend(f'    {season_name(y, weekly)}: {v:.2f}%' for y, v in others)
            lines.append('')
    return lines or ['No qualifying rankings for the target year/season.']


def monthly_lines(data, weekly):
    if not weekly:
        raise ValueError('The monthly seasonal-maximum report requires the weekly DATE/Season CSV format.')
    lines = ['Monthly seasonal maximum ice concentrations (November through May)', '']
    for month in (11, 12, 1, 2, 3, 4, 5):
        maxima = {}
        for period, year, value, observed in data:
            if observed.month == month and (year not in maxima or value > maxima[year][0]):
                maxima[year] = (value, observed)
        lines.append(calendar.month_name[month])
        if not maxima:
            lines.extend(['No observations.', ''])
            continue
        for direction, reverse in (('Highest', True), ('Lowest', False)):
            lines.append(f'{direction} 10 seasonal maxima:')
            for year, (value, observed) in sorted(maxima.items(), key=lambda item: item[1][0], reverse=reverse)[:10]:
                lines.append(f'  {observed:%Y-%m-%d}  {season_name(year, True)}  {value:.2f}%')
        lines.append('')
    return lines


def available_output(path):
    """Keep previous reports, including reports from repeated runs."""
    candidate = path
    count = 2
    while candidate.exists():
        candidate = path.with_name(f'{path.stem}_{count}{path.suffix}')
        count += 1
    return candidate


def write_charts(path, data, weekly, start, target, output_dir):
    try:
        import plotly.graph_objects as go
    except ImportError as error:
        raise ValueError('HTML graphs require Plotly. Install it with: python -m pip install plotly') from error
    years = sorted({r[1] for r in data})
    values = {year: [r[2] for r in data if r[1] == year] for year in years}
    x = [season_name(y, weekly) for y in years]
    metrics = [
        ('Mean observed ice concentration', '%', [statistics.mean(values[y]) for y in years]),
        ('Observations with ice', 'weekly observations' if weekly else 'days', [sum(v > 0 for v in values[y]) for y in years]),
        ('Sum of observed ice concentrations', 'percentage-observations' if weekly else 'percentage-days', [sum(values[y]) for y in years]),
        ('Standard deviation of observed ice concentration', 'percentage points', [statistics.stdev(values[y]) if len(values[y]) > 1 else None for y in years]),
    ]
    pieces = ['<!doctype html><html><head><meta charset="utf-8"><title>Ice analysis</title></head><body>',
              f'<h1>{escape(path.stem)}: {start} to {target}</h1>',
              '<p>Statistics use available observations only; missing values are not zero. Partial seasons are not adjusted for missing observations. Sums are not area measurements.</p>']
    for i, (title, ylabel, y) in enumerate(metrics):
        fig = go.Figure(go.Bar(x=x, y=y) if i == 2 else go.Scatter(x=x, y=y, mode='lines+markers'))
        fig.update_layout(title=title, xaxis_title='Season' if weekly else 'Year', yaxis_title=ylabel)
        pieces.append(fig.to_html(full_html=False, include_plotlyjs=True if i == 0 else False))
    pieces.append(f'<p>CSV file used: {escape(str(path))}</p></body></html>')
    output = available_output(output_dir / f'{path.stem}_analysis_{target}_from_{start}.html')
    output.write_text('\n'.join(pieces), encoding='utf-8')
    return output


def main(mode='climatology', script_dir=None):
    try:
        script_dir = Path(script_dir or Path(__file__).parent).resolve()
        path, data, weekly, start, target = select_data(script_dir)
        output_dir = script_dir / 'output'
        output_dir.mkdir(parents=True, exist_ok=True)
        if mode == 'analysis':
            output = write_charts(path, data, weekly, start, target, output_dir)
        else:
            lines = [f'{path.stem}: Ice Concentration {mode.title()} Report',
                     f'Target: {season_name(target, weekly)}; Comparison period: {season_name(start, weekly)} to {season_name(target, weekly)}',
                     'Missing observations are excluded. Ties share rank: 1 + number of strictly higher/lower observations.',
                     'Top 10 highest; bottom 5 lowest only when <=5 zero and >=5 positive years/seasons.' if mode == 'climatology' else '', '']
            if weekly:
                lines.extend(['Weekly method: fixed Jan 01-07, Jan 08-14, etc. calendar bins; Feb 29 shares Feb 28\'s bin.',
                              'Each season contributes its maximum observation in each bin; missing bins are excluded.',
                              'Season labels and start year use the FIRST year (1995 means 1995-96). Concentration fractions multiplied by 100.', ''])
            lines += monthly_lines(data, weekly) if mode == 'monthly' else ranking_lines(data, weekly, target, mode != 'topten')
            lines.extend(['', f'CSV file used: {path}'])
            output = available_output(output_dir / f'{path.stem}_{mode}_{target}_from_{start}.txt')
            output.write_text('\n'.join(lines) + '\n', encoding='utf-8')
        print(f'Results saved to: {output}')
        return output
    except (OSError, ValueError, csv.Error) as error:
        print(f'Error: {error}')
        return None
    except (EOFError, KeyboardInterrupt):
        print('\nCancelled.')
        return None


if __name__ == '__main__':
    main()
