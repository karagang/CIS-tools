"""Create the single-season and recent-season ice coverage charts together.

Requires pandas, numpy, and matplotlib. Run this script and answer the three
prompts. Press Enter at the CSV prompt to use the file beside this script.
Both charts are saved as PNGs in the output folder beside this script.
Both plot windows also open together. Reruns replace this program's PNGs.
The recent-season chart preserves the original latest + five previous seasons.
"""

from pathlib import Path
import re

import matplotlib.dates as mdates
import matplotlib.pyplot as plt
from matplotlib.ticker import AutoMinorLocator, FuncFormatter
import numpy as np
import pandas as pd


DEFAULT_CSV = "weekly_NE-NFLND.csv"
ENGLISH_REGION = "NE Newfoundland"
FRENCH_REGION = "du nord-est de Terre-Neuve"
MONTH_LABELS = {
    11: "Nov/Nov", 12: "Dec/Déc", 1: "Jan/Jan", 2: "Feb/Fév",
    3: "Mar/Mar", 4: "Apr/Avr", 5: "May/Mai", 6: "Jun/Jun",
}


def normalized_date(date):
    # Use a leap year so February 29 is valid in every season.
    return pd.Timestamp(2023 if date.month >= 11 else 2024, date.month, date.day)


def load_data(file_name, start_season, end_date):
    """Read the selected November-June seasons through the cutoff date."""
    match = re.fullmatch(r"(\d{4})[-/](\d{2})", start_season.strip())
    if not match or int(match[2]) != (int(match[1]) + 1) % 100:
        raise ValueError("Starting season must be consecutive years, e.g. 1995-96.")
    first_year = int(match[1])
    cutoff = pd.to_datetime(end_date.strip(), format="%Y%m%d", errors="raise")
    data = pd.read_csv(file_name, usecols=["DATE", "Season", "Concentration", "Average"])
    data["DATE"] = pd.to_datetime(data["DATE"].astype(str), format="%Y%m%d")
    data = data.set_index("DATE").sort_index()
    data["Season"] = data["Season"].astype(str).str.replace("/", "-", regex=False)
    season_years = pd.to_numeric(data["Season"].str[:4], errors="raise")
    data = data.loc[
        (data.index <= cutoff)
        & ((data.index.month >= 11) | (data.index.month <= 6))
        & (season_years >= first_year)
    ].copy()
    if data.empty:
        raise ValueError("No November-June data matches the starting season and end date.")
    return data


def plot_overlay(data, recent=False):
    """Return one chart; the caller displays both with a single show()."""
    seasons = sorted(data["Season"].unique())
    fig = plt.figure(figsize=(12, 8))
    # Leave room above the axes for the three-line bilingual title.
    ax = fig.add_axes([0.12, 0.1, 0.85, 0.75])
    fig.canvas.manager.set_window_title(
        ENGLISH_REGION + (" - latest + five previous seasons" if recent else " - single season")
    )

    historical = seasons[:-6] if recent else seasons[:-1]
    for season in historical:
        values = data.loc[data["Season"] == season]
        ax.plot(values.index.map(normalized_date), values["Concentration"] * 100,
                color="lightgray", alpha=0.2 if recent else 0.3, linewidth=1)

    if recent:
        colors = plt.cm.tab10(np.linspace(0, 1, 5))
        for color, season in zip(colors, seasons[-6:-1]):
            values = data.loc[data["Season"] == season]
            ax.plot(values.index.map(normalized_date), values["Concentration"] * 100,
                    color=color, alpha=0.6, linewidth=1.5, label=season)

    latest = data.loc[data["Season"] == seasons[-1]]
    ax.plot(latest.index.map(normalized_date), latest["Concentration"] * 100,
            color="red" if recent else "#FF4500",
            linewidth=2.5 if recent else 3, label=seasons[-1])

    # Preserve the original use of the supplied Average column, grouped by
    # normalized calendar date (rather than recalculating a climatology).
    average = data.groupby(data.index.map(normalized_date))["Average"].mean()
    ax.plot(average.index, average * 100, color="lime",
            linewidth=2.5 if recent else 3, label="Average - Moyenne", zorder=5)

    ax.xaxis.set_major_locator(mdates.MonthLocator())
    ax.xaxis.set_major_formatter(FuncFormatter(
        lambda value, position: MONTH_LABELS.get(mdates.num2date(value).month, "")
    ))
    ax.xaxis.set_minor_locator(AutoMinorLocator(4))
    ax.tick_params(axis="x", which="major", length=8)
    ax.tick_params(axis="x", which="minor", length=4)
    ax.grid(True, which="minor", axis="x", linestyle="-", alpha=0.1)
    ax.set_xlim(pd.Timestamp("2023-11-01"), pd.Timestamp("2024-06-30"))
    ax.set_ylim(0, max(data["Concentration"].max() * 100 * 1.1, 10))
    season_range = f"{seasons[0]} - {seasons[-1]}"
    ax.set_title(
        f"{ENGLISH_REGION} Ice Concentration by Season\n"
        f"Concentration des glaces {FRENCH_REGION}\n{season_range}",
        fontsize=14, pad=20,
    )
    ax.set_xlabel("Month/Mois", fontsize=10, labelpad=10)
    ax.set_ylabel("% Ice Coverage\nCouverture de glace (%)", fontsize=12)
    ax.legend(loc="upper right", framealpha=0.9, fontsize=10)
    return fig


def create_plots(data):
    """Build both charts from the same input data."""
    return plot_overlay(data), plot_overlay(data, recent=True)


def save_plots(figures):
    """Save both PNGs beside the script, independently of the working directory."""
    script_path = Path(__file__).resolve()
    output_dir = script_path.parent / "output"
    output_dir.mkdir(parents=True, exist_ok=True)
    for figure, chart in zip(figures, ("single", "lastFive")):
        output_path = output_dir / f"{script_path.stem}-{chart}.png"
        figure.savefig(output_path, dpi=200, bbox_inches="tight")
        print(f"Saved: {output_path}")


def main():
    start_season = input("Enter the starting season (YYYY-YY): ")
    end_date = input("Enter the end date (YYYYMMDD): ")
    file_name = input(f"CSV file (press Enter for {DEFAULT_CSV}): ").strip().strip('"')
    if file_name:
        csv_path = Path(file_name).expanduser()
        if not csv_path.is_absolute() and not csv_path.is_file():
            csv_path = Path(__file__).resolve().parent / csv_path
    else:
        csv_path = Path(__file__).resolve().with_name(DEFAULT_CSV)
    try:
        data = load_data(csv_path, start_season, end_date)
    except (OSError, ValueError, KeyError) as error:
        raise SystemExit(f"Cannot create charts: {error}") from error
    figures = create_plots(data)
    save_plots(figures)
    plt.show()


if __name__ == "__main__":
    main()
