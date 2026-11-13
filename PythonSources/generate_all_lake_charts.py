#!/usr/bin/env python3
"""Generate English, French, and combined bilingual ice-cover charts."""

import argparse
from pathlib import Path

import matplotlib.dates as mdates
import matplotlib.pyplot as plt
import pandas as pd
import seaborn as sns
from matplotlib.lines import Line2D
from matplotlib.ticker import FuncFormatter
from pandas.tseries.offsets import DateOffset


ROOT = Path(__file__).resolve().parent
CHART_JOBS = (
    (
        "Erie",
        "Erie/erie_with_average.csv",
        "Lake Erie Ice Cover",
        "Couverture de glace du lac Érié",
    ),
    (
        "Huron",
        "Huron/huron_with_average.csv",
        "Lake Huron Ice Cover",
        "Couverture de glace du lac Huron",
    ),
    (
        "Michigan",
        "Michigan/michigan_with_average.csv",
        "Lake Michigan Ice Cover",
        "Couverture de glace du lac Michigan",
    ),
    (
        "Ontario",
        "Ontario/ontario_with_average.csv",
        "Lake Ontario Ice Cover",
        "Couverture de glace du lac Ontario",
    ),
    (
        "Superior",
        "Superior/superior_with_average.csv",
        "Lake Superior Ice Cover",
        "Couverture de glace du lac Supérieur",
    ),
    (
        "Great Lakes basin",
        "Great-Lakes/bas_with_average.csv",
        "Great Lakes Ice Cover",
        "Couverture de glace des Grands Lacs",
    ),
)


CHART_VARIANTS = (
    {
        "suffix": "_chart_EN.png",
        "title_language": "EN",
        "ylabel": "Ice Cover (%)",
        "average_label": "Average",
        "date_language": "EN",
    },
    {
        "suffix": "_chart_FR.png",
        "title_language": "FR",
        "ylabel": "Couverture de glace (%)",
        "average_label": "Moyenne",
        "date_language": "FR",
    },
    {
        "suffix": "_chart.png",
        "title_language": "BILINGUAL",
        "ylabel": "Ice Cover (%) Couverture de glace",
        "average_label": "Average - Moyenne",
        "date_language": "EN",
    },
)

FRENCH_MONTHS = {
    1: "janv.",
    2: "févr.",
    3: "mars",
    4: "avr.",
    5: "mai",
    6: "juin",
    7: "juil.",
    8: "août",
    9: "sept.",
    10: "oct.",
    11: "nov.",
    12: "déc.",
}


def parse_date_robust(date_string):
    """Parse day-month or month-day values onto a leap-year-safe timeline."""
    date_string = str(date_string).strip()
    parts = date_string.split("-")
    if len(parts) == 2:
        if parts[0].isdigit():
            return pd.to_datetime(f"{parts[0]}-{parts[1]}-2020", format="%d-%b-%Y")
        return pd.to_datetime(f"{parts[1]}-{parts[0]}-2020", format="%d-%b-%Y")
    return pd.to_datetime(f"{date_string}-2020")


def lighten_color(color, amount=0.5):
    return tuple(channel + (1 - channel) * amount for channel in color)


def french_date_formatter(value, _position):
    date = mdates.num2date(value)
    return f"{date.day:02d}-{FRENCH_MONTHS[date.month]}"


def prepare_data(csv_path):
    dataframe = pd.read_csv(csv_path)
    if "Date" not in dataframe.columns:
        raise ValueError(f"CSV has no 'Date' column: {csv_path}")

    dataframe["Date"] = dataframe["Date"].apply(parse_date_robust)
    january_to_june = dataframe["Date"].dt.month <= 6
    dataframe.loc[january_to_june, "Date"] += DateOffset(years=1)

    long_dataframe = dataframe.melt(
        id_vars="Date", var_name="Year", value_name="Value"
    )
    average_data = long_dataframe[long_dataframe["Year"] == "Average"].copy()
    year_data = long_dataframe[long_dataframe["Year"] != "Average"].copy()
    year_data["Year"] = pd.to_numeric(year_data["Year"], errors="coerce")
    year_data = year_data.dropna(subset=["Year"])
    year_data["Year"] = year_data["Year"].astype(int)

    years = sorted(year_data["Year"].unique())
    if not years:
        raise ValueError(f"CSV contains no numeric year columns: {csv_path}")

    latest_year = years[-1]
    cutoff = latest_year - 4
    recent_years = [year for year in years if year >= cutoff]
    older_years = [year for year in years if year < cutoff]
    colors = sns.color_palette("tab10", n_colors=len(recent_years))
    recent_colors = dict(zip(recent_years, colors))

    return {
        "year_data": year_data,
        "average_data": average_data,
        "years": years,
        "latest_year": latest_year,
        "cutoff": cutoff,
        "recent_years": recent_years,
        "older_years": older_years,
        "recent_colors": recent_colors,
    }


def create_chart(csv_path, lake_name, english_title, french_title, chart_data, variant):
    titles = {
        "EN": english_title,
        "FR": french_title,
        "BILINGUAL": f"{english_title}\n{french_title}",
    }
    year_data = chart_data["year_data"]
    average_data = chart_data["average_data"]
    latest_year = chart_data["latest_year"]
    recent_colors = chart_data["recent_colors"]
    grey = (0.85, 0.85, 0.85)

    sns.set(style="whitegrid")
    figure, axis = plt.subplots(figsize=(12, 6))

    for year in chart_data["older_years"]:
        series = year_data[year_data["Year"] == year]
        series = series.dropna(subset=["Value"]).sort_values("Date")
        axis.plot(
            series["Date"], series["Value"], color=grey,
            linestyle="-", linewidth=1.2, alpha=0.4,
        )

    for year in chart_data["recent_years"]:
        series = year_data[year_data["Year"] == year]
        series = series.dropna(subset=["Value"]).sort_values("Date")
        if year == latest_year:
            color, alpha = recent_colors[year], 1.0
        else:
            color, alpha = lighten_color(recent_colors[year], 0.6), 0.8
        axis.plot(series["Date"], series["Value"], color=color, linewidth=1.8, alpha=alpha)

    average_series = average_data.dropna(subset=["Value"]).sort_values("Date")
    axis.plot(
        average_series["Date"], average_series["Value"],
        color="lime", linestyle="-", linewidth=1.2, alpha=1.0,
    )

    handles = [
        Line2D([0], [0], color=grey, linestyle="-", linewidth=1.2, alpha=0.4)
    ]
    labels = [f"{chart_data['years'][0]} - {chart_data['cutoff'] - 1}"]
    for year in chart_data["recent_years"]:
        color = recent_colors[year]
        if year != latest_year:
            color = lighten_color(color, 0.6)
        handles.append(Line2D([0], [0], color=color, linestyle="-", linewidth=1.8))
        labels.append(str(year))
    handles.append(Line2D([0], [0], color="lime", linestyle="-", linewidth=1.2))
    labels.append(variant["average_label"])
    axis.legend(handles=handles, labels=labels, loc="upper left", frameon=True)

    axis.xaxis.set_major_locator(mdates.DayLocator(bymonthday=[1, 7, 14, 21]))
    if variant["date_language"] == "FR":
        axis.xaxis.set_major_formatter(FuncFormatter(french_date_formatter))
    else:
        axis.xaxis.set_major_formatter(mdates.DateFormatter("%b-%d"))
    plt.setp(axis.get_xticklabels(), rotation=45, ha="right", fontsize=6)

    axis.set_xlim(
        pd.to_datetime("01-Nov-2020", format="%d-%b-%Y"),
        pd.to_datetime("21-Jun-2021", format="%d-%b-%Y"),
    )
    axis.set_title(titles[variant["title_language"]])
    axis.set_ylabel(variant["ylabel"])
    axis.set_ylim(bottom=0)
    figure.tight_layout()

    csv_path = Path(csv_path)
    output_path = csv_path.with_name(f"{csv_path.stem}{variant['suffix']}")
    figure.savefig(output_path, dpi=300, bbox_inches="tight")
    print(f"Saved {lake_name} {variant['title_language']} chart to: {output_path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="verify the six CSV files without generating charts",
    )
    args = parser.parse_args()

    missing = [ROOT / csv_relative for _, csv_relative, _, _ in CHART_JOBS]
    missing = [path for path in missing if not path.is_file()]
    if missing:
        missing_paths = "\n".join(f"  {path}" for path in missing)
        parser.error(f"Required CSV files were not found:\n{missing_paths}")

    if args.check:
        print(f"Validated {len(CHART_JOBS)} CSV files ({len(CHART_JOBS) * len(CHART_VARIANTS)} charts).")
        for lake, csv_relative, _, _ in CHART_JOBS:
            print(f"{lake}: {ROOT / csv_relative}")
        return

    for lake, csv_relative, english_title, french_title in CHART_JOBS:
        csv_path = ROOT / csv_relative
        print(f"Generating charts for {lake} from {csv_path}")
        chart_data = prepare_data(csv_path)
        for variant in CHART_VARIANTS:
            create_chart(
                csv_path,
                lake,
                english_title,
                french_title,
                chart_data,
                variant,
            )

    print("Displaying generated charts...")
    plt.show()


if __name__ == "__main__":
    main()
