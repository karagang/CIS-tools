"""Shared analysis for the regional launchers. Requires pandas and matplotlib."""
from pathlib import Path
import argparse
import calendar
import datetime as dt
import math
import pandas as pd


def week_of_month_from_day(day):
    """Keep the original four buckets; week 4 includes days 22 through 31."""
    return min((day - 1) // 7 + 1, 4)


def ordinal(n):
    return str(n) + ('th' if 10 <= n % 100 <= 20 else {1: 'st', 2: 'nd', 3: 'rd'}.get(n % 10, 'th'))


def read_data(path):
    df = pd.read_csv(path)
    df.columns = df.columns.str.strip()
    if not {'DATE', 'Concentration'}.issubset(df.columns):
        raise ValueError('CSV must contain DATE and Concentration columns.')
    df['DATE'] = pd.to_datetime(df['DATE'].astype(str).str.strip(), format='%Y%m%d', errors='coerce')
    df['Concentration'] = pd.to_numeric(df['Concentration'], errors='coerce')
    valid = df['DATE'].notna() & df['Concentration'].map(lambda x: pd.notna(x) and math.isfinite(x))
    if not valid.all():
        print(f'Warning: skipping {int((~valid).sum())} rows with invalid dates or concentrations.')
    df = df.loc[valid].copy().sort_values('DATE').reset_index(drop=True)
    if df.empty:
        raise ValueError('CSV has no valid observations.')
    if not df['Concentration'].between(0, 1).all():
        raise ValueError('Concentration must be a fraction between 0 and 1, as in the original CSVs.')
    df['Year'] = df['DATE'].dt.year
    df['Month'] = df['DATE'].dt.month
    df['WeekOfMonth'] = df['DATE'].dt.day.map(week_of_month_from_day)
    return df


def select_week(df, year, month, week):
    if not 1 <= year <= 9999 or not 1 <= month <= 12 or not 1 <= week <= 4:
        raise ValueError('Year must be 1–9999, month 1–12, and week 1–4.')
    selected = df.loc[(df['Year'] >= year) & (df['Month'] == month) & (df['WeekOfMonth'] == week)].copy()
    if selected.empty:
        raise ValueError(f'No data for week {week} of {calendar.month_name[month]} from {year} onward.')
    annual = selected.groupby('Year', as_index=False).agg(Concentration=('Concentration', 'mean'))
    median = annual['Concentration'].median()
    annual['Anomaly_Percent'] = (annual['Concentration'] - median) * 100
    return selected, annual, median


def prompt_integer(label, default, minimum, maximum):
    while True:
        raw = input(f'{label} [{default}]: ').strip()
        try:
            value = int(raw) if raw else default
            if minimum <= value <= maximum:
                return value
        except ValueError:
            pass
        print(f'Enter a whole number from {minimum} to {maximum}.')


def get_options(region, script_dir, default_csv, argv=None):
    parser = argparse.ArgumentParser(description=f'{region}: compare a selected month/week across years.')
    parser.add_argument('--csv', help='CSV filename in this regional folder, or a full path')
    parser.add_argument('--start-year', type=int)
    parser.add_argument('--month', type=int)
    parser.add_argument('--week', type=int, help='1=days 1–7, 2=8–14, 3=15–21, 4=22–31')
    args = parser.parse_args(argv)
    folder = Path(script_dir).resolve()
    filename = args.csv
    if filename is None:
        filename = input(f'CSV filename [{default_csv}]: ').strip().strip('"') or default_csv
    path = Path(filename).expanduser()
    if not path.is_absolute():
        path = folder / path
    df = read_data(path)
    latest = df['DATE'].max()
    print(f'\n{region}: data {df.DATE.min():%Y-%m-%d} to {latest:%Y-%m-%d}')
    print('Compare the same month/week across years; median uses the selected years.')
    print('Weeks: 1 = days 1–7; 2 = 8–14; 3 = 15–21; 4 = 22–31.')
    year = args.start_year if args.start_year is not None else prompt_integer('Start year', int(df.Year.min()), 1, 9999)
    month = args.month if args.month is not None else prompt_integer('Month (1–12)', latest.month, 1, 12)
    week = args.week if args.week is not None else prompt_integer('Week (1–4)', week_of_month_from_day(latest.day), 1, 4)
    select_week(df, year, month, week)  # Validate before creating any outputs.
    return path, df, year, month, week, folder / 'output'


def create_anomaly_chart(df, region, year, month, week, output_dir):
    import matplotlib.pyplot as plt
    from matplotlib.patches import Patch
    from matplotlib.lines import Line2D

    selected, annual, median = select_week(df, year, month, week)
    latest = selected['DATE'].max()
    latest_file = df['DATE'].max()
    month_name = calendar.month_name[month]
    period = f'{ordinal(week)} Week {month_name}'
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    # Include the selected start year so different analysis ranges do not overwrite one another.
    stem = f'{ordinal(week)}_Week_{month_name}_from_{year}_through_{latest:%Y%m%d}'
    chart = output_dir / f'Ice_Anomaly_{stem}.png'
    summary = output_dir / f'Summary_{stem}.txt'
    years = annual['Year'].to_numpy()
    anomalies = annual['Anomaly_Percent'].to_numpy()
    fig, ax = plt.subplots(figsize=(14, 7))
    try:
        colors = ['blue' if value >= 0 else 'red' for value in anomalies]
        ax.bar(years, anomalies, color=colors, alpha=0.7, edgecolor='black', linewidth=0.5)
        ax.axhline(0, color='black', linewidth=1.5)
        ax.set_xticks(years[::2])
        ax.tick_params(axis='x', rotation=45)
        span = max(1, anomalies.max() - anomalies.min())
        ax.set_ylim(min(-1, anomalies.min() - max(1, span * 0.08)),
                    max(1, anomalies.max() + max(1, span * 0.32)))
        ax.set_ylabel('Anomaly %', fontsize=12, fontweight='bold')
        for x, value, color in zip(years, anomalies, colors):
            ax.text(x, value, f'{value:.1f}', ha='center', va='bottom' if value >= 0 else 'top', color=color, fontsize=7)
        ax.legend(handles=[Patch(facecolor='blue', alpha=0.7, label='Positive Anomaly'),
                           Patch(facecolor='red', alpha=0.7, label='Negative Anomaly'),
                           Line2D([0], [0], color='black', label='Median')], loc='upper left')
        last = annual.iloc[-1]
        ax.text(0.98, 0.97, f'Median Concentration: {median * 100:.1f}%\nLatest Year ({int(last.Year)}): {last.Concentration * 100:.1f}%',
                transform=ax.transAxes, va='top', ha='right', fontsize=10,
                bbox=dict(boxstyle='round,pad=0.8', facecolor='wheat', alpha=0.8))
        ax.set_title(f'{region} Sea Ice Extent Anomalies {period}\n{int(years[0])}–{int(years[-1])}', fontweight='bold')
        ax.grid(True, alpha=0.3, axis='y')
        ax.set_axisbelow(True)
        fig.tight_layout()
        fig.savefig(chart, bbox_inches='tight', dpi=300)
    finally:
        plt.close(fig)
    lines = [f'=== Anomaly Summary for {region} ===', '', f'Week analyzed: {period}',
             'Week definition: days 1–7=1st, 8–14=2nd, 15–21=3rd, 22–31=4th',
             f'Requested start year: {year}', f'Latest data date in CSV: {latest_file:%Y-%m-%d}',
             f'Latest selected-week observation: {latest:%Y-%m-%d}',
             f'Years in analysis: {int(years[0])} to {int(years[-1])}',
             f'Number of years: {len(annual)}', f'Median concentration: {median * 100:.2f}%',
             'Baseline: median of yearly mean concentrations for the selected month/week and years.',
             'Anomalies are percentage-point differences from that median.', '', 'Top 5 positive anomalies:']
    for positive in (True, False):
        if not positive:
            lines.extend(['', 'Top 5 negative anomalies:'])
        rows = annual.loc[annual.Anomaly_Percent > 0].nlargest(5, 'Anomaly_Percent') if positive else annual.loc[annual.Anomaly_Percent < 0].nsmallest(5, 'Anomaly_Percent')
        if rows.empty:
            lines.append('  None')
        for row in rows.itertuples(index=False):
            lines.append(f'  {int(row.Year)}: {row.Concentration * 100:.2f}% (anomaly: {row.Anomaly_Percent:+.1f}%)')
    summary.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'Chart: {chart}\nSummary: {summary}')
    return chart, summary


def run(region, script_dir, default_csv, argv=None):
    try:
        _, df, year, month, week, output = get_options(region, script_dir, default_csv, argv)
        create_anomaly_chart(df, region, year, month, week, output)
    except (ValueError, OSError, ImportError) as exc:
        raise SystemExit(f'Error: {exc}') from exc
