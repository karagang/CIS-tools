#!/usr/bin/env python3
"""Generate single-season and two-season ice-cover charts for all Great Lakes.

Install: python -m pip install pandas matplotlib
Run:     python great_lakes_charts.py

By default, reads Data/ and writes output/ beside this script, regardless of
the working directory. CSV columns: Date (e.g. Nov-10), year columns, Average.
Year labels retain the meaning used in the original CSVs and plotting scripts.
"""

import argparse
from datetime import datetime
from pathlib import Path
import sys

import matplotlib

matplotlib.use("Agg")  # Save all charts without opening/blocking on plot windows.
import matplotlib.dates as mdates
import matplotlib.pyplot as plt
import pandas as pd


# Accept both the actual filenames and the alternative spellings in the request.
LAKES = (
    ("Lake Erie", ("eri_with_avg.csv",),
     "Lake Erie Ice Cover\nCouverture de glace du lac Érié"),
    ("Lake Ontario", ("ont_with_avg.csv", "ontr_with_avg.csv"),
     "Lake Ontario Ice Cover\nCouverture de glace du lac Ontario"),
    ("Lake Michigan", ("mic_with_avg.csv",),
     "Lake Michigan Ice Cover\nCouverture de glace du lac Michigan"),
    ("Lake Huron", ("hur_with_avg.csv",),
     "Lake Huron and Georgian Bay Ice Cover\n"
     "Couverture de glace du lac Huron et de la baie Georgienne"),
    ("Lake Superior", ("sup_with_avg.csv", "supr_with_avg.csv"),
     "Lake Superior Ice Cover\nCouverture de glace du lac Supérieur"),
    ("Great Lakes", ("grtlks_with_avg.csv",),
     "Great Lakes Ice Cover\nCouverture de glace des Grands Lacs"),
)


def find_csv(data_dir, filenames):
    """Choose the first supported filename, ignoring filename case."""
    files = {p.name.lower(): p for p in data_dir.iterdir() if p.is_file()}
    for name in filenames:
        if name.lower() in files:
            return files[name.lower()]
    raise ValueError("Missing CSV: expected " + " or ".join(filenames))


def season_date(value):
    """Align Nov/Dec to 1999 and Jan onward to leap year 2000.

    The synthetic timeline overlays seasons and supports Feb-29, unlike parsing
    month/day alone (which uses non-leap year 1900).
    """
    parsed = datetime.strptime(f"{str(value).strip()}-2000", "%b-%d-%Y")
    return parsed.replace(year=1999 if parsed.month >= 11 else 2000)


def load_data(csv_path, requested_year=None):
    df = pd.read_csv(csv_path, encoding="utf-8-sig")
    df.columns = df.columns.str.strip()
    required = {"Date", "Average"}
    if not required.issubset(df.columns):
        raise ValueError("CSV must include Date and Average columns")
    if df.empty:
        raise ValueError("CSV contains no data rows")

    df["Date"] = pd.to_datetime(df["Date"].map(season_date))
    if df["Date"].duplicated().any():
        raise ValueError("CSV contains duplicate month/day rows")
    df = df.sort_values("Date").reset_index(drop=True)

    year_columns = [c for c in df.columns if len(c) == 4 and c.isdigit()]
    if not year_columns:
        raise ValueError("CSV has no four-digit year columns")

    # Blank entries remain gaps; invalid text and out-of-range percentages fail.
    for col in ["Average"] + year_columns:
        df[col] = pd.to_numeric(df[col], errors="raise")
        values = df[col].dropna()
        if ((values < 0) | (values > 100)).any():
            raise ValueError(f"Column {col} contains ice cover outside 0–100%")

    in_window = df["Date"].between(datetime(1999, 11, 1), datetime(2000, 6, 21))
    df = df.loc[in_window].copy()
    populated = sorted(int(c) for c in year_columns if df[c].notna().any())
    if not populated:
        raise ValueError("No seasonal data within November 1–June 21")
    latest = requested_year if requested_year is not None else populated[-1]
    for year in (latest - 1, latest):
        if year not in populated:
            raise ValueError(f"No data for {year}; both {latest - 1} and {latest} are required")
    if not df["Average"].notna().any():
        raise ValueError("Average has no values within November 1–June 21")
    return df, latest


def save_chart(df, latest, title, destination, two_seasons=False):
    """Draw one chart using the styling of the two supplied scripts."""
    with plt.rc_context({"axes.facecolor": "white", "axes.axisbelow": True}):
        fig, ax = plt.subplots(figsize=(12, 6))
        try:
            if two_seasons:
                ax.plot(df["Date"], df[str(latest - 1)], color="red",
                        linewidth=1.8, label=str(latest - 1))
            ax.plot(df["Date"], df[str(latest)], color="blue",
                    linewidth=1.8, label=str(latest))
            ax.plot(df["Date"], df["Average"], color="lime",
                    linewidth=1.2, label="Average - Moyenne")
            ax.legend(loc="upper left", frameon=True)
            ax.xaxis.set_major_locator(mdates.DayLocator(bymonthday=[1, 7, 14, 21]))
            ax.xaxis.set_minor_locator(mdates.DayLocator())
            ax.xaxis.set_major_formatter(mdates.DateFormatter("%b-%d"))
            ax.grid(True, which="major", axis="y", alpha=0.5)
            ax.grid(True, which="major", axis="x", linestyle="-", alpha=0.5)
            ax.grid(True, which="minor", axis="x", linestyle=":", alpha=0.2)
            ax.tick_params(axis="x", labelsize=6, rotation=45)
            for label in ax.get_xticklabels():
                label.set_horizontalalignment("right")
            ax.set_xlim(datetime(1999, 11, 1), datetime(2000, 6, 21))
            ax.set_ylim(bottom=0)
            ax.set_ylabel("Ice Cover (%) Couverture de glace")
            ax.set_title(title)
            fig.tight_layout()
            fig.savefig(destination, dpi=200, facecolor="white")
        finally:
            plt.close(fig)


def main(argv=None):
    script_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data-dir", type=Path, default=script_dir / "Data",
                        help="CSV folder (default: Data beside this script)")
    parser.add_argument("--output-dir", type=Path, default=script_dir / "output",
                        help="Chart folder (default: output beside this script)")
    parser.add_argument("--year", type=int,
                        help="Latest season year (default: latest populated year per CSV)")
    args = parser.parse_args(argv)
    if not args.data_dir.is_dir():
        parser.error(f"Data folder does not exist: {args.data_dir}")

    # Validate all six inputs before generating charts, reporting every input error.
    prepared, errors = [], []
    for lake, filenames, title in LAKES:
        try:
            csv_path = find_csv(args.data_dir, filenames)
            df, latest = load_data(csv_path, args.year)
            prepared.append((lake, title, csv_path, df, latest))
        except (OSError, ValueError, TypeError, OverflowError) as exc:
            errors.append(f"{lake}: {exc}")
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1

    count = 0
    for lake, title, csv_path, df, latest in prepared:
        folder = args.output_dir / lake
        try:
            folder.mkdir(parents=True, exist_ok=True)
            for two_seasons in (False, True):
                filename = (f"two_seasons_{latest - 1}_{latest}.png" if two_seasons
                            else f"single_season_{latest}.png")
                destination = folder / filename
                save_chart(df, latest, title, destination, two_seasons)
                count += 1
                print(f"Saved: {destination}")
        except (OSError, ValueError) as exc:
            print(f"ERROR: {lake}: {exc}", file=sys.stderr)
            return 1
    print(f"Done: {count} charts saved in {args.output_dir.resolve()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
