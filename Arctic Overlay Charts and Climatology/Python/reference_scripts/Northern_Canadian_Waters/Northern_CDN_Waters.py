import pandas as pd
import matplotlib.pyplot as plt
import datetime as dt
import numpy as np
import os

# ============================================================
# CONSTANTS
# ============================================================

MONTH_DOY_CENTER = {
    1: 15,  2: 46,  3: 74,  4: 105, 5: 135, 6: 166,
    7: 196, 8: 227, 9: 258, 10: 288, 11: 319, 12: 349
}

MONTH_NAMES_EN = {
    1: 'Jan', 2: 'Feb', 3: 'Mar', 4: 'Apr',  5: 'May',  6: 'Jun',
    7: 'Jul', 8: 'Aug', 9: 'Sep', 10: 'Oct', 11: 'Nov', 12: 'Dec'
}

YEAR_COLORS = ['blue', 'purple', 'orange', 'brown', 'cyan']


# ============================================================
# DATA LOADING AND PREPARATION
# ============================================================

def load_and_prepare(file_name, end_date):
    """
    Load the CSV file, clean the data and return a prepared dataframe.
    """
    df = pd.read_csv(file_name)

    for col in ['Concentration', 'Area']:
        df[col] = pd.to_numeric(df[col], errors='coerce')

    df = df.dropna(subset=['Concentration'])
    df = df[df['Concentration'] > 0].copy()

    df['DATE'] = pd.to_datetime(df['DATE'].astype(str), format='%Y%m%d')
    df = df[df['DATE'] <= end_date].copy()

    df['Season']    = df['DATE'].dt.year
    df['Month']     = df['DATE'].dt.month
    df['DayOfYear'] = df['DATE'].dt.dayofyear

    # Values in file are 0-1, convert to percentage
    df['Concentration'] = df['Concentration'] * 100

    df = df.sort_values('DATE').reset_index(drop=True)
    return df


# ============================================================
# CORE PLOT FUNCTION
# ============================================================

def build_plot(df_plot, doy_col, area, title, output_filename,
               month_list, xlim_start, xlim_end, legend_loc):
    """
    Build and save a single ice concentration plot.

    df_plot        : filtered dataframe for this plot
    doy_col        : column name to use as x-axis ('DayOfYear' or
                     'AdjustedDOY')
    area           : total region area in km²
    title          : plot title
    output_filename: output PNG filename
    month_list     : list of month numbers (1-12) to show on x-axis
    xlim_start     : left x-axis limit (DOY units)
    xlim_end       : right x-axis limit (DOY units)
    legend_loc     : matplotlib legend location string
    """

    years = sorted(df_plot['Season'].unique())
    if len(years) == 0:
        print(f"  WARNING: no data for '{output_filename}' – skipping.")
        return

    current_year = max(years)

    fig, ax1 = plt.subplots(figsize=(12, 8))

    # --- all years older than 5 years ago in grey ---
    for year in years:
        if year < current_year - 5:
            d = df_plot[df_plot['Season'] == year].sort_values(doy_col)
            ax1.plot(d[doy_col], d['Concentration'],
                     color='lightgrey', alpha=0.3, linewidth=1)

    # --- previous 5 complete years in colour ---
    for i, year in enumerate(range(current_year - 5, current_year)):
        if year in years:
            d = df_plot[df_plot['Season'] == year].sort_values(doy_col)
            ax1.plot(d[doy_col], d['Concentration'],
                     color=YEAR_COLORS[i], alpha=0.8,
                     linewidth=2, label=str(year))

    # --- current year in red ---
    d_cur = df_plot[df_plot['Season'] == current_year].sort_values(doy_col)
    ax1.plot(d_cur[doy_col], d_cur['Concentration'],
             color='red', linewidth=2.5, label=str(current_year))

    # --- historical median in black dashed ---
    # Exclude current year so a partial record does not skew the median
    df_hist = df_plot[df_plot['Season'] < current_year]
    if not df_hist.empty:
        median_series = df_hist.groupby(doy_col)['Concentration'].median()
        ax1.plot(median_series.index, median_series.values,
                 color='black', linestyle='--', linewidth=2, label='Median')

    # --- left y-axis ---
    ax1.set_ylim(0, 100)
    ax1.set_xlim(xlim_start, xlim_end)
    ax1.set_ylabel('Ice Coverage (%)  Couverture de Glace',
                   color='black', fontweight='bold')
    ax1.tick_params(axis='y', labelcolor='black')
    ax1.grid(True, alpha=0.3)

    # --- right y-axis ---
    ax2 = ax1.twinx()
    ax2.set_ylim(0, area / 1e6)
    ax2.set_ylabel('Ice Area Covered (×10⁶ km²)  Superficie de Glace',
                   color='darkgreen', fontweight='bold')
    ax2.tick_params(axis='y', labelcolor='darkgreen')

    ax1.set_title(title, fontweight='bold', fontsize=13)

    # --- x-axis ticks: day 7, month name, day 21 for each month ---
    ticks  = []
    labels = []
    for m in month_list:
        centre = MONTH_DOY_CENTER[m]
        mname  = MONTH_NAMES_EN[m]
        ticks  += [centre - 8, centre, centre + 6]
        labels += ['7', mname, '21']

    ax1.set_xticks(ticks)
    ax1.set_xticklabels(labels)

    month_name_set = set(MONTH_NAMES_EN.values())
    for tick_label in ax1.get_xticklabels():
        txt = tick_label.get_text()
        if txt in month_name_set:
            tick_label.set_fontweight('bold')
            tick_label.set_fontsize(10)
        else:
            tick_label.set_fontsize(8)
            tick_label.set_color('grey')

    ax1.tick_params(axis='x', which='major', length=6, width=1)

    ax1.legend(title='Year / Année', loc=legend_loc,
               framealpha=0.95, fontsize=11, title_fontsize=12)

    plt.tight_layout()
    plt.savefig(output_filename, bbox_inches='tight', dpi=300)
    plt.close(fig)
    print(f"  Saved: {output_filename}")


# ============================================================
# CLIMATOLOGY TEXT OUTPUT
# ============================================================

def ordinal(n):
    if 11 <= (n % 100) <= 13:
        suffix = 'th'
    else:
        suffix = {1: 'st', 2: 'nd', 3: 'rd'}.get(n % 10, 'th')
    return f"{n}{suffix}"


def week_of_month(day):
    if   day <= 7:  return 1
    elif day <= 14: return 2
    elif day <= 21: return 3
    else:           return 4


def write_climatology(df, file_base, area):
    """
    Compute and write weekly climatology text files (EN and FR).
    """
    latest_date = df['DATE'].max()
    latest_row  = df[df['DATE'] == latest_date].iloc[0]
    latest_year = int(latest_row['Season'])
    latest_conc = float(latest_row['Concentration'])

    ref_month = latest_date.month
    ref_wom   = week_of_month(latest_date.day)
    month_str = latest_date.strftime('%B')

    mask = (
        (df['Month'] == ref_month) &
        (df['DATE'].dt.day.apply(week_of_month) == ref_wom)
    )
    same_week = df[mask].copy()

    if same_week.empty:
        print("  No same-week data found for climatology.")
        return

    per_year = (same_week.groupby('Season', as_index=False)
                .agg(Concentration=('Concentration', 'mean')))
    per_year = per_year.sort_values('Concentration', ascending=True,
                                    kind='mergesort').reset_index(drop=True)
    per_year['Rank'] = per_year['Concentration'].rank(
        method='min', ascending=True).astype(int)

    n          = len(per_year)
    med_conc   = per_year['Concentration'].median()
    mean_conc  = per_year['Concentration'].mean()

    cur_row  = per_year[per_year['Season'] == latest_year]
    rank_str = "N/A" if cur_row.empty else \
               f"{ordinal(int(cur_row['Rank'].iloc[0]))} lowest of {n}"

    latest_area  = latest_conc * area / 100
    median_area  = med_conc    * area / 100
    average_area = mean_conc   * area / 100
    diff_median  = latest_area - median_area
    diff_average = latest_area - average_area

    lowest7  = per_year.head(min(7, n))
    highest7 = per_year.sort_values('Concentration', ascending=False,
                                    kind='mergesort').head(min(7, n))

    translations = {
        'en': {
            'title':       'Weekly Climatology Summary for Northern Canadian Waters',
            'latest_date': 'Latest date observed',
            'week_def':    'Week definition: days 1–7=1st, 8–14=2nd, 15–21=3rd, 22–31=4th',
            'latest_week': 'Latest week classification',
            'week_of':     'week of',
            'comp_set':    'Comparison set: all years for the',
            'n_years':     'Number of years in comparison',
            'cur_val':     "Current year's value",
            'rank':        'Rank (by lowest)',
            'median':      'Median (this week, all years)',
            'average':     'Average (this week, all years)',
            'cur_area':    'Current ice area',
            'med_area':    'Median ice area',
            'diff_med':    'Difference from median',
            'avg_area':    'Average ice area',
            'diff_avg':    'Difference from average',
            'low7':        'Seven lowest years (this week):',
            'high7':       'Seven highest years (this week):',
        },
        'fr': {
            'title':       'Résumé de climatologie hebdomadaire – Eaux canadiennes du Nord',
            'latest_date': 'Dernière date observée',
            'week_def':    'Définition semaine : jours 1–7=1ère, 8–14=2e, 15–21=3e, 22–31=4e',
            'latest_week': 'Classification de la dernière semaine',
            'week_of':     'semaine de',
            'comp_set':    'Ensemble de comparaison : toutes les années pour la',
            'n_years':     'Nombre d\'années en comparaison',
            'cur_val':     'Valeur de l\'année en cours',
            'rank':        'Classement (par le plus bas)',
            'median':      'Médiane (cette semaine, toutes années)',
            'average':     'Moyenne (cette semaine, toutes années)',
            'cur_area':    'Superficie de glace actuelle',
            'med_area':    'Superficie de glace médiane',
            'diff_med':    'Différence par rapport à la médiane',
            'avg_area':    'Superficie de glace moyenne',
            'diff_avg':    'Différence par rapport à la moyenne',
            'low7':        'Sept années les plus basses (cette semaine) :',
            'high7':       'Sept années les plus hautes (cette semaine) :',
        }
    }

    for lang, code in [('en', 'EN'), ('fr', 'FR')]:
        t = translations[lang]
        lines = [
            t['title'], '',
            '-' * 50,
            f"{t['latest_date']}: {latest_date.strftime('%Y-%m-%d')}",
            t['week_def'],
            f"{t['latest_week']}: {ordinal(ref_wom)} {t['week_of']} {month_str}",
            '',
            f"{t['comp_set']} {ordinal(ref_wom)} {t['week_of']} {month_str}",
            '',
            f"{t['n_years']}: {n}",
            f"{t['cur_val']} ({latest_year}): {latest_conc:.2f}%",
            f"{t['rank']}: {rank_str}",
            f"{t['median']}: {med_conc:.2f}%",
            f"{t['average']}: {mean_conc:.2f}%",
            f"{t['cur_area']}: {latest_area:,.0f} km²",
            f"{t['med_area']}: {median_area:,.0f} km²",
            f"{t['diff_med']}: {diff_median:+,.0f} km²",
            f"{t['avg_area']}: {average_area:,.0f} km²",
            f"{t['diff_avg']}: {diff_average:+,.0f} km²",
            '',
            t['low7'],
        ]
        for _, r in lowest7.iterrows():
            ia = r['Concentration'] * area / 100
            lines.append(
                f"  {int(r['Season'])}: {r['Concentration']:.2f}%"
                f"  |  {ia:,.0f} km²")
        lines += ['', t['high7']]
        for _, r in highest7.iterrows():
            ia = r['Concentration'] * area / 100
            lines.append(
                f"  {int(r['Season'])}: {r['Concentration']:.2f}%"
                f"  |  {ia:,.0f} km²")
        lines.append('')

        out_name = (f"Climatology_{file_base}_"
                    f"{latest_date.strftime('%Y%m%d')}_{code}.txt")
        with open(out_name, 'w', encoding='utf-8') as fh:
            fh.write('\n'.join(lines))
        print(f"  Saved: {out_name}")


# ============================================================
# MAIN
# ============================================================

def main():

    # ----------------------------------------------------------
    # User input
    # ----------------------------------------------------------
    while True:
        file_name = input("Please enter the name of the data file: ").strip()
        if os.path.exists(file_name):
            break
        print(f"  File '{file_name}' not found. Please try again.")

    while True:
        try:
            end_date = pd.to_datetime(
                input("Please enter the end date (YYYYMMDD): ").strip(),
                format='%Y%m%d')
            break
        except ValueError:
            print("  Invalid format. Please use YYYYMMDD.")

    # ----------------------------------------------------------
    # Load and prepare data
    # ----------------------------------------------------------
    print("\nLoading data …")
    df = load_and_prepare(file_name, end_date)

    area     = float(df['Area'].iloc[0])
    out_base = os.path.splitext(os.path.basename(file_name))[0]
    cur_year = int(df['Season'].max())

    print(f"  Rows loaded : {len(df)}")
    print(f"  Latest year : {cur_year}")
    print(f"  Latest date : {df['DATE'].max().strftime('%Y-%m-%d')}")
    print(f"  Area        : {area:,.0f} km²")

    title_base = ('Northern Canadian Waters – Eaux du nord canadien\n'
                  f'1983 – {cur_year}')

    # ----------------------------------------------------------
    # PLOT 1 : April – December
    # Shows the melt season through to freeze-up for every year.
    # ----------------------------------------------------------
    print("\nGenerating Plot 1: Apr – Dec …")

    df_p1      = df[df['Month'].between(4, 12)].copy()
    months_p1  = list(range(4, 13))               # 4 to 12
    xl_p1      = MONTH_DOY_CENTER[4]  - 20        # just before April
    xr_p1      = MONTH_DOY_CENTER[12] + 20        # just after December

    build_plot(
        df_p1, 'DayOfYear', area,
        title_base,
        f'Ice_Concentration_{out_base}_Apr-Dec.png',
        months_p1, xl_p1, xr_p1, 'lower left'
    )

    # ----------------------------------------------------------
    # PLOT 2 : July – December
    # Focuses on the minimum extent through to early freeze-up.
    # ----------------------------------------------------------
    print("Generating Plot 2: Jul – Dec …")

    df_p2      = df[df['Month'].between(7, 12)].copy()
    months_p2  = list(range(7, 13))               # 7 to 12
    xl_p2      = MONTH_DOY_CENTER[7]  - 20
    xr_p2      = MONTH_DOY_CENTER[12] + 20

    build_plot(
        df_p2, 'DayOfYear', area,
        title_base,
        f'Ice_Concentration_{out_base}_Jul-Dec.png',
        months_p2, xl_p2, xr_p2, 'upper left'
    )

    # ----------------------------------------------------------
    # PLOT 3 : October – December
    # Freeze-up period only.
    # ----------------------------------------------------------
    print("Generating Plot 3: Oct – Dec …")

    df_p3      = df[df['Month'].between(10, 12)].copy()
    months_p3  = list(range(10, 13))              # 10 to 12
    xl_p3      = MONTH_DOY_CENTER[10] - 20
    xr_p3      = MONTH_DOY_CENTER[12] + 20

    build_plot(
        df_p3, 'DayOfYear', area,
        title_base,
        f'Ice_Concentration_{out_base}_Oct-Dec.png',
        months_p3, xl_p3, xr_p3, 'upper left'
    )

    # ----------------------------------------------------------
    # PLOT 4 : October – January  (cross-year)
    # Freeze-up through to mid-winter.
    # January data is shifted by +365 DOY so it plots continuously
    # after December.  Season label stays as the October year so
    # e.g. Oct-2025 through Jan-2026 is labelled Season 2025.
    # ----------------------------------------------------------
    print("Generating Plot 4: Oct – Jan …")

    df_p4 = df[df['Month'].isin([10, 11, 12, 1])].copy()

    def oct_jan_season(row):
        # January belongs to the previous Oct-based season
        return row['Season'] if row['Month'] >= 10 else row['Season'] - 1

    def oct_jan_doy(row):
        # Push January DOYs past December (DOY 365/366)
        return row['DayOfYear'] if row['Month'] >= 10 \
               else row['DayOfYear'] + 365

    df_p4['Season']      = df_p4.apply(oct_jan_season, axis=1)
    df_p4['AdjustedDOY'] = df_p4.apply(oct_jan_doy,    axis=1)

    # x-axis: Oct centre to Jan centre + 365
    xl_p4 = MONTH_DOY_CENTER[10] - 20
    xr_p4 = MONTH_DOY_CENTER[1]  + 365 + 20

    # Build tick list: Oct, Nov, Dec use real DOY centres;
    # Jan uses MONTH_DOY_CENTER[1] + 365
    months_p4        = [10, 11, 12]          # handled normally in build_plot
    # We handle the shifted January manually below by passing a custom
    # month_list via a small wrapper — simplest approach is to add a
    # temporary entry and handle it inside the tick-building section.
    # Instead we just call build_plot with months 10-12 and then
    # manually add the January tick after.

    # Easiest clean solution: extend MONTH_DOY_CENTER temporarily
    # with key 13 = shifted January
    MONTH_DOY_CENTER[13] = MONTH_DOY_CENTER[1] + 365
    MONTH_NAMES_EN[13]   = 'Jan'
    months_p4 = [10, 11, 12, 13]

    build_plot(
        df_p4, 'AdjustedDOY', area,
        title_base,
        f'Ice_Concentration_{out_base}_Oct-Jan.png',
        months_p4, xl_p4, xr_p4, 'upper left'
    )

    # Clean up temporary keys
    del MONTH_DOY_CENTER[13]
    del MONTH_NAMES_EN[13]

    # ----------------------------------------------------------
    # PLOT 5 : January – June   *** NEW PLOT ***
    # This plot covers the winter maximum through spring melt.
    # It naturally includes the current year's Jan-Mar data
    # (e.g. 2026) alongside all historical Jan-Jun records,
    # so the most recent partial-year data is always visible.
    # ----------------------------------------------------------
    print("Generating Plot 5: Jan – Jun …")

    df_p5      = df[df['Month'].between(1, 6)].copy()
    months_p5  = list(range(1, 7))                # 1 to 6
    xl_p5      = MONTH_DOY_CENTER[1] - 20
    xr_p5      = MONTH_DOY_CENTER[6] + 20

    build_plot(
        df_p5, 'DayOfYear', area,
        title_base,
        f'Ice_Concentration_{out_base}_Jan-Jun.png',
        months_p5, xl_p5, xr_p5, 'lower left'
    )

    # ----------------------------------------------------------
    # PLOT 6 : Custom date range
    # ----------------------------------------------------------
    print("\n--- Custom Date Range Chart ---")
    custom_start = input("Enter start date (MMDD): ").strip()
    custom_end   = input("Enter end date   (MMDD): ").strip()

    try:
        # Use 2024 (leap year) to get correct DOY for any calendar date
        s_obj = dt.datetime.strptime(f"2024{custom_start}", "%Y%m%d")
        e_obj = dt.datetime.strptime(f"2024{custom_end}",   "%Y%m%d")

        s_doy      = s_obj.timetuple().tm_yday
        e_doy      = e_obj.timetuple().tm_yday
        cross_year = e_doy < s_doy
        e_doy_adj  = e_doy + 366 if cross_year else e_doy

        df_c = df.copy()

        if cross_year:
            def custom_season(row):
                if row['Month'] >= s_obj.month:
                    return row['Season']
                elif row['Month'] <= e_obj.month:
                    return row['Season'] - 1
                return np.nan

            def custom_doy(row):
                return (row['DayOfYear'] + 365
                        if row['Month'] <= e_obj.month
                        else row['DayOfYear'])

            df_c['Season']      = df_c.apply(custom_season, axis=1)
            df_c['AdjustedDOY'] = df_c.apply(custom_doy,    axis=1)
            df_c = df_c.dropna(subset=['Season'])
            df_c['Season'] = df_c['Season'].astype(int)
            df_c = df_c[(df_c['AdjustedDOY'] >= s_doy) &
                        (df_c['AdjustedDOY'] <= e_doy_adj)]
            doy_col_c = 'AdjustedDOY'
        else:
            df_c = df_c[(df_c['DayOfYear'] >= s_doy) &
                        (df_c['DayOfYear'] <= e_doy)]
            doy_col_c = 'DayOfYear'

        # Build month list for the custom range
        custom_months = []
        curr = s_obj
        while True:
            m = curr.month
            if cross_year and (
                m < s_obj.month or
                (m == s_obj.month and curr.year > s_obj.year)
            ):
                # Shifted month: add temporary entry
                shifted_key = m + 12
                MONTH_DOY_CENTER[shifted_key] = MONTH_DOY_CENTER[m] + 365
                MONTH_NAMES_EN[shifted_key]   = MONTH_NAMES_EN[m]
                custom_months.append(shifted_key)
            else:
                custom_months.append(m)

            if m == e_obj.month:
                break
            curr = (dt.datetime(curr.year + 1, 1, 1)
                    if curr.month == 12
                    else dt.datetime(curr.year, curr.month + 1, 1))

        build_plot(
            df_c, doy_col_c, area,
            f"{title_base}\n({custom_start} to {custom_end})",
            f'Ice_Concentration_{out_base}_Custom_{custom_start}'
            f'-{custom_end}.png',
            custom_months, s_doy - 15, e_doy_adj + 15, 'best'
        )

        # Clean up any temporary shifted keys
        for key in list(MONTH_DOY_CENTER.keys()):
            if key > 12:
                del MONTH_DOY_CENTER[key]
        for key in list(MONTH_NAMES_EN.keys()):
            if key > 12:
                del MONTH_NAMES_EN[key]

    except ValueError as ve:
        print(f"  Invalid date format: {ve}. Custom chart skipped.")
    except Exception as ex:
        print(f"  Error generating custom chart: {ex}")

    # ----------------------------------------------------------
    # Climatology text files
    # ----------------------------------------------------------
    print("\nGenerating climatology text files …")
    write_climatology(df, out_base, area)

    print(f"\nAll done.  Area used: {area:,.0f} km²")


if __name__ == '__main__':
    main()