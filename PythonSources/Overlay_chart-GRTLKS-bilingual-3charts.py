#!/usr/bin/env python3
"""Create bilingual, English, and French Great Lakes ice coverage PNG charts.

Requires: pandas, matplotlib, seaborn
Run without arguments to be prompted for the CSV, or use:
    python Overlay_chart-GRTLKS-bilingual-3charts.py bas_with_average.csv
Add --no-show for unattended runs and --output-dir to choose a save folder.
"""

import argparse
from pathlib import Path

import matplotlib.dates as mdates
import matplotlib.pyplot as plt
from matplotlib.ticker import FuncFormatter
import pandas as pd
import seaborn as sns


FR_MONTHS = ('', 'janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin',
             'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.')
CONFIGS = {
    'BILINGUAL': ('Great Lakes Ice Cover\nCouverture de glace des Grands Lacs',
                  'Ice Cover (%) / Couverture de glace (%)', 'Average - Moyenne'),
    'EN': ('Great Lakes Ice Cover', 'Ice Cover (%)', 'Average'),
    'FR': ('Couverture de glace des Grands Lacs', 'Couverture de glace (%)', 'Moyenne'),
}


def parse_date_robust(value):
    """Accept 14-Nov or Nov-14 and preserve February 29 on a seasonal axis."""
    value = str(value).strip()
    for fmt in ('%d-%b-%Y', '%b-%d-%Y'):
        try:
            date = pd.to_datetime(value + '-2000', format=fmt)
            # November/December precede January–June in the same ice season.
            return date.replace(year=1999 if date.month >= 7 else 2000)
        except ValueError:
            pass
    raise ValueError(f'Invalid date {value!r}; expected 14-Nov or Nov-14.')


def french_date_formatter(value, position=None):
    date = mdates.num2date(value)
    return f'{date.day:02d}-{FR_MONTHS[date.month]}'


def lighten_color(color, amount=0.6):
    return tuple(channel + (1 - channel) * amount for channel in color)


def plot_overlay(csv_path, last_n=5, output_dir=None, show=True):
    """Read the CSV once, save three charts, and return their paths."""
    if last_n < 1:
        raise ValueError('last_n must be at least 1.')
    if not show:
        plt.switch_backend('Agg')
    csv_path = Path(csv_path).expanduser().resolve()
    df = pd.read_csv(csv_path)
    df.columns = df.columns.str.strip()
    if not {'Date', 'Average'}.issubset(df.columns):
        raise ValueError('The CSV must contain Date and Average columns.')
    year_columns = sorted((c for c in df.columns if c.isdigit()), key=int)
    if not year_columns:
        raise ValueError('The CSV must contain at least one numeric year column.')
    df['Date'] = df['Date'].apply(parse_date_robust)
    for column in year_columns + ['Average']:
        df[column] = pd.to_numeric(df[column], errors='raise')
    df = df.sort_values('Date')
    latest = int(year_columns[-1])
    cutoff = latest - last_n + 1
    recent = [c for c in year_columns if int(c) >= cutoff]
    older = [c for c in year_columns if int(c) < cutoff]
    palette = dict(zip(recent, sns.color_palette('tab10', n_colors=len(recent))))
    destination = Path(output_dir).expanduser().resolve() if output_dir else csv_path.parent
    destination.mkdir(parents=True, exist_ok=True)
    sns.set_theme(style='whitegrid')
    paths = []

    for language, (title, ylabel, average_label) in CONFIGS.items():
        fig, ax = plt.subplots(figsize=(12, 6))
        try:
            for index, year in enumerate(older):
                series = df[['Date', year]].dropna()
                label = (older[0] if len(older) == 1 else f'{older[0]} - {older[-1]}')
                ax.plot(series['Date'], series[year], color=(0.85, 0.85, 0.85),
                        linewidth=1.2, alpha=0.4, label=label if index == 0 else '_nolegend_')
            for year in recent:
                series = df[['Date', year]].dropna()
                newest = int(year) == latest
                color = palette[year] if newest else lighten_color(palette[year])
                ax.plot(series['Date'], series[year], color=color, linewidth=1.8,
                        alpha=1 if newest else 0.8, label=year)
            average = df[['Date', 'Average']].dropna()
            ax.plot(average['Date'], average['Average'], color='lime',
                    linewidth=1.2, label=average_label)
            ax.legend(loc='upper left', frameon=True)
            ax.xaxis.set_major_locator(mdates.DayLocator(bymonthday=[1, 7, 14, 21]))
            formatter = (FuncFormatter(french_date_formatter) if language == 'FR'
                         else mdates.DateFormatter('%b-%d'))
            ax.xaxis.set_major_formatter(formatter)
            ax.tick_params(axis='x', labelsize=6)
            plt.setp(ax.get_xticklabels(), rotation=45, ha='right')
            ax.set_xlim(pd.Timestamp('1999-11-01'), pd.Timestamp('2000-06-21'))
            ax.set_ylim(bottom=0)
            ax.set_title(title)
            ax.set_ylabel(ylabel)
            fig.tight_layout()
            path = destination / f'{csv_path.stem}_chart_{language}.png'
            fig.savefig(path, dpi=300, bbox_inches='tight')
            paths.append(path)
            print(f'Saved {language} chart to: {path}')
        finally:
            if not show:
                plt.close(fig)
    if show:
        plt.show()
    return paths


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('csv_path', nargs='?', help='Input CSV path')
    parser.add_argument('--output-dir', help='Defaults to the CSV folder')
    parser.add_argument('--last-n', type=int, default=5, help='Recent calendar years to highlight')
    parser.add_argument('--no-show', action='store_true', help='Save without displaying windows')
    args = parser.parse_args()
    csv_path = args.csv_path
    if csv_path is None:
        default = Path(__file__).resolve().parent / 'bas_with_average.csv'
        csv_path = input(f'Enter CSV path [{default}]: ').strip().strip('\"') or default
    try:
        plot_overlay(csv_path, args.last_n, args.output_dir, show=not args.no_show)
    except (OSError, ValueError) as error:
        parser.exit(1, f'ERROR: {error}\n')


if __name__ == '__main__':
    main()
