import pandas as pd
import matplotlib.pyplot as plt
import datetime as dt
import numpy as np
import os
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from anomaly_analysis import get_options, create_anomaly_chart

def safe_convert(value):
    """
    Safely convert value to float, handling various potential input types
    """
    try:
        return float(value)
    except (ValueError, TypeError):
        return np.nan

def make_plot(df_in, area, title, output_filename, month_centers, month_labels, week_ticks, xlim_start, xlim_end, legend_loc):
    # Copy to avoid SettingWithCopy issues
    df = df_in.copy()

    # Check if we have AdjustedDayOfYear column (for Oct-Jan plots)
    if 'AdjustedDayOfYear' in df.columns:
        doy_col = 'AdjustedDayOfYear'
    else:
        doy_col = 'DayOfYear'
        df['DayOfYear'] = df['DATE'].dt.dayofyear

    # Determine available years in this filtered set
    years = sorted(df['Season'].unique())
    if len(years) == 0:
        print(f"Warning: No data available for plotting '{output_filename}'. Skipping.")
        return
    current_year = max(years)

    # Create the plot with two y-axes
    fig, ax1 = plt.subplots(figsize=(12, 8))

    # First plot all older years in grey
    older_years = [year for year in years if year < current_year - 5]
    for year in older_years:
        year_data = df[df['Season'] == year].sort_values(doy_col)
        ax1.plot(year_data[doy_col], year_data['Concentration'],
                 color='lightgrey', alpha=0.3, linewidth=1)

    # Plot the previous 5 years with distinct colors
    colors = ['blue', 'purple', 'orange', 'brown', 'cyan']
    previous_5_years = range(current_year - 5, current_year)
    for year, color in zip(previous_5_years, colors):
        if year in years:
            year_data = df[df['Season'] == year].sort_values(doy_col)
            ax1.plot(year_data[doy_col], year_data['Concentration'],
                     label=str(year), color=color, alpha=0.7, linewidth=2)

    # Plot the current year in red
    current_year_data = df[df['Season'] == current_year].sort_values(doy_col)
    ax1.plot(current_year_data[doy_col], current_year_data['Concentration'],
             label=str(current_year), color='red', linewidth=2.5)

    # Calculate and plot median ice coverage for this filtered period
    median_data = df.groupby(doy_col)['Concentration'].median()
    ax1.plot(median_data.index, median_data.values,
             label='Median', color='black', linestyle='--', linewidth=2)

    # Left y-axis: Ice Coverage %
    ax1.set_ylabel('Ice Coverage (%) Couverture de Glace', color='black', fontweight='bold')
    ax1.tick_params(axis='y', labelcolor='black')
    ax1.set_ylim(0, 100)
    ax1.grid(True, alpha=0.3)

    # Right y-axis: Ice Area Covered
    ax2 = ax1.twinx()
    ax2.set_ylabel('Ice Area Covered (x10⁶ km²) Superficie de Glace ', color='darkgreen', fontweight='bold')
    ax2.tick_params(axis='y', labelcolor='darkgreen')
    ax2.set_ylim(0, area / 1e6)

    # Title
    ax1.set_title(title, fontweight='bold')

    # Enhanced X-axis with day 7, 15 (month name), and 21 labels
    all_ticks = []
    all_labels = []
    
    for center, month_name in zip(month_centers, month_labels):
        day_7 = center - 8   # Approximately day 7
        day_21 = center + 6  # Approximately day 21
        
        # Add day 7, month center (15), and day 21
        all_ticks.extend([day_7, center, day_21])
        all_labels.extend(['7', month_name, '21'])
    
    # Set all ticks and labels
    ax1.set_xticks(all_ticks)
    ax1.set_xticklabels(all_labels)
    
    # Style the ticks - make month names bold and larger
    for i, label in enumerate(ax1.get_xticklabels()):
        if all_labels[i] in month_labels:  # This is a month name
            label.set_fontweight('bold')
            label.set_fontsize(10)
        else:  # This is a day number (7 or 21)
            label.set_fontsize(8)
            label.set_color('gray')
    
    # Vary tick lengths: longer for month centers, shorter for days 7 & 21
    ax1.tick_params(axis='x', which='major', length=6, width=1)

    # X-axis limits
    ax1.set_xlim(xlim_start, xlim_end)

    # Legend
    ax1.legend(title='Year/Année', loc=legend_loc, framealpha=0.95, fontsize=12, title_fontsize=13, markerscale=1.5)

    plt.tight_layout()
    plt.savefig(OUTPUT_DIR / output_filename, bbox_inches='tight', dpi=300)
    plt.close(fig)
    print(f"Plot has been saved as '{output_filename}'")


def ordinal(n: int) -> str:
    # Return ordinal string for an integer (1 -> 1st, 2 -> 2nd, 3 -> 3rd, etc.)
    if 11 <= (n % 100) <= 13:
        suffix = "th"
    else:
        suffix = {1: "st", 2: "nd", 3: "rd"}.get(n % 10, "th")
    return f"{n}{suffix}"

def week_of_month_from_day(day: int) -> int:
    # 1-7: week 1, 8-14: week 2, 15-21: week 3, 22-31: week 4
    if 1 <= day <= 7:
        return 1
    elif 8 <= day <= 14:
        return 2
    elif 15 <= day <= 21:
        return 3
    else:
        return 4

def write_weekly_climatology(df_filtered, file_name_base, latest_date, latest_year, latest_conc, latest_rank_str, 
                             month_name, wom, median_conc, mean_conc, latest_ice_area, median_ice_area, 
                             average_ice_area, median_difference, average_difference, lowest7, highest7, total_area):
    """
    Create climatology text files for both English and French.
    """
    if df_filtered.empty:
        print("No data available to compute weekly climatology.")
        return

    # Translation dictionary
    translations = {
        "en": {
            "title": "Weekly Climatology Summary for Northern Canadian Waters",
            "latest_date": "Latest date observed",
            "week_def": "Week definition: days 1–7=1st, 8–14=2nd, 15–21=3rd, 22–31=4th",
            "latest_week": "Latest week classification",
            "week_of": "week of",
            "comparison_set": "Comparison set: All years for the",
            "n_years": "Number of years in comparison",
            "latest_year_value": "Latest year's value",
            "rank": "Rank (by lowest)",
            "median": "Median (this week across all years)",
            "average": "Average (this week across all years)",
            "latest_ice_area": "Latest ice area covered",
            "median_ice_area": "Median week ice area",
            "diff_median": "Difference from median",
            "average_ice_area": "Average week ice area",
            "diff_average": "Difference from average",
            "seven_lowest": "Seven lowest years (this week):",
            "seven_highest": "Seven highest years (this week):",
        },
        "fr": {
            "title": "Résumé de climatologie hebdomadaire pour les eaux canadiennes du Nord",
            "latest_date": "Dernière date observée",
            "week_def": "Définition de la semaine : jours 1–7=1ère, 8–14=2e, 15–21=3e, 22–31=4e",
            "latest_week": "Classification de la dernière semaine",
            "week_of": "semaine de",
            "comparison_set": "Ensemble de comparaison : toutes les années pour la",
            "n_years": "Nombre d'années en comparaison",
            "latest_year_value": "Valeur de l'année la plus récente",
            "rank": "Classement (par le plus bas)",
            "median": "Médiane (cette semaine sur toutes les années)",
            "average": "Moyenne (cette semaine sur toutes les années)",
            "latest_ice_area": "Superficie de glace couverte la plus récente",
            "median_ice_area": "Superficie de glace médiane de la semaine",
            "diff_median": "Différence par rapport à la médiane",
            "average_ice_area": "Superficie de glace moyenne de la semaine",
            "diff_average": "Différence par rapport à la moyenne",
            "seven_lowest": "Sept années les plus basses (cette semaine) :",
            "seven_highest": "Sept années les plus hautes (cette semaine) :",
        }
    }

    def generate_climatology_text(language="en"):
        """Generate climatology summary in specified language (en or fr)"""
        t = translations[language]
        n_years = df_filtered['Season'].nunique()
        
        lines = []
        lines.append(t["title"])
        lines.append(f"Analysis / Analyse: {start_year} - {end_date:%Y-%m-%d}")
        lines.append("")
        lines.append("--------------------------")
        lines.append(f"{t['latest_date']}: {latest_date.strftime('%Y-%m-%d')} (YYYY-MM-DD)")
        lines.append(t["week_def"])
        lines.append(f"{t['latest_week']}: {ordinal(wom)} {t['week_of']} {month_name}")
        lines.append("")
        lines.append(f"{t['comparison_set']} {ordinal(wom)} {t['week_of']} {month_name}")
        lines.append("")
        lines.append(f"{t['n_years']}: {n_years}")
        lines.append(f"{t['latest_year_value']} ({latest_year}): {latest_conc:.2f}%")
        lines.append(f"{t['rank']}: {latest_rank_str}")
        lines.append(f"{t['median']}: {median_conc:.2f}%")
        lines.append(f"{t['average']}: {mean_conc:.2f}%")
        lines.append(f"{t['latest_ice_area']}: {latest_ice_area:.2f} km²")
        lines.append(f"{t['median_ice_area']}: {median_ice_area:.2f} km²")
        lines.append(f"{t['diff_median']}: {median_difference:.2f} km²")
        lines.append(f"{t['average_ice_area']}: {average_ice_area:.2f} km²")
        lines.append(f"{t['diff_average']}: {average_difference:.2f} km²")
        lines.append("")
        lines.append(t["seven_lowest"])
        
        for i, r in lowest7.iterrows():
            ice_area = r['Concentration'] * total_area / 100
            lines.append(f"  - {int(r['Season'])}: {r['Concentration']:.2f}% | {ice_area:.2f} km²")

        lines.append("")
        lines.append(t["seven_highest"])
        
        for i, r in highest7.iterrows():
            ice_area = r['Concentration'] * total_area / 100
            lines.append(f"  - {int(r['Season'])}: {r['Concentration']:.2f}% | {ice_area:.2f} km²")

        lines.append("")
        
        return "\n".join(lines)

    # Generate and save both English and French versions
    for lang, lang_code in [("en", "EN"), ("fr", "FR")]:
        output_txt = f"Climatology_{file_name_base}_{latest_date.strftime('%Y%m%d')}_{lang_code}.txt"
        output_txt = f"{Path(output_txt).stem}_from_{start_year}.txt"
        text_content = generate_climatology_text(language=lang)
        with open(OUTPUT_DIR / output_txt, "w", encoding="utf-8") as f:
            f.write(text_content)
        print(f"Weekly climatology text has been saved as '{output_txt}'")



if __name__ == "__main__":
    try:
        file_name, data, start_year, selected_month, selected_wom, OUTPUT_DIR = get_options(
            'Northern Canadian Waters', Path(__file__).resolve().parent, 'Northern_CDN_Waters.csv')
        from anomaly_analysis import select_week
        selected_week, annual, _ = select_week(data, start_year, selected_month, selected_wom)
        end_date = data['DATE'].max()
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        if 'Area' not in data or pd.to_numeric(data['Area'], errors='coerce').dropna().empty:
            raise ValueError('Northern overlays require a numeric Area column.')
        data['Area'] = pd.to_numeric(data['Area'], errors='coerce')
        create_anomaly_chart(data, 'Northern Canadian Waters', start_year, selected_month, selected_wom, OUTPUT_DIR)
    except (ValueError, OSError, ImportError) as exc:
        raise SystemExit(f'Error: {exc}') from exc
    # Overlays cover all months in their seasonal windows, from the selected start year.
    try:
        # Read the data with additional error handling
        df = data.copy()

        # Convert Concentration column safely
        df['Concentration'] = df['Concentration'].apply(safe_convert)

        # Remove any rows with NaN in Concentration
        df = df.dropna(subset=['Concentration'])

        # Get the area from the CSV file
        area = df['Area'].iloc[0]  # Area in km²

        # Convert DATE to datetime and create Season (year) from DATE
        df['DATE'] = pd.to_datetime(df['DATE'], format='%Y%m%d')
        df['Season'] = df['DATE'].dt.year

        # Convert concentration to percentage
        df['Concentration'] = df['Concentration'] * 100

        # Calculate ice area covered in millions of km² (kept for referential use)
        df['Ice_Area_Covered'] = df['Concentration'] * area / 100 / 1e6

        # Filter data up to end date
        df = df[(df['DATE'] >= pd.Timestamp(start_year, 1, 1)) & (df['DATE'] <= end_date)].copy()

        # -------- Plot 1: April–December (original) --------
        df_apr_dec = df.copy()
        df_apr_dec['Month'] = df_apr_dec['DATE'].dt.month
        df_apr_dec = df_apr_dec[df_apr_dec['Month'].between(4, 12)]

        # Month tick positions (centers) and labels for Apr–Dec
        month_centers_apr_dec = [106, 136, 167, 197, 228, 259, 289, 320, 350]
        month_labels_apr_dec = ['Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

        # Week tick positions (approximate) Apr–Dec
        week_ticks_apr_dec = []
        week_ticks_apr_dec.extend([91, 98, 105, 112, 120])   # Apr
        week_ticks_apr_dec.extend([121, 128, 135, 143, 151]) # May
        week_ticks_apr_dec.extend([152, 159, 167, 174, 181]) # Jun
        week_ticks_apr_dec.extend([182, 189, 197, 204, 212]) # Jul
        week_ticks_apr_dec.extend([213, 220, 228, 235, 243]) # Aug
        week_ticks_apr_dec.extend([244, 251, 259, 266, 273]) # Sep
        week_ticks_apr_dec.extend([274, 281, 289, 296, 304]) # Oct
        week_ticks_apr_dec.extend([305, 312, 320, 327, 334]) # Nov
        week_ticks_apr_dec.extend([335, 343, 350, 358, 365]) # Dec
        
        output_filename_apr_dec = f'Ice_Concentration_{os.path.splitext(os.path.basename(file_name))[0]}.png'
        title_text = f'Northern Canadian Waters - Eaux du nord canadien\n {start_year} - {end_date.year}'
        output_suffix = f'_from_{start_year}_through_{end_date:%Y%m%d}'
        make_plot(
            df_apr_dec,
            area,
            title_text,
            f"{Path(output_filename_apr_dec).stem}{output_suffix}.png",
            month_centers_apr_dec,
            month_labels_apr_dec,
            week_ticks_apr_dec,
            91, 366,
            legend_loc='lower left'
        
        )

        # -------- Plot 2: July–December (new) --------
        df_jul_dec = df.copy()
        df_jul_dec['Month'] = df_jul_dec['DATE'].dt.month
        df_jul_dec = df_jul_dec[df_jul_dec['Month'].between(7, 12)]

        # Month tick positions (centers) and labels for Jul–Dec
        month_centers_jul_dec = [197, 228, 259, 289, 320, 350]
        month_labels_jul_dec = ['Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

        # Week tick positions (approximate) Jul–Dec
        week_ticks_jul_dec = []
        week_ticks_jul_dec.extend([182, 189, 197, 204, 212]) # Jul
        week_ticks_jul_dec.extend([213, 220, 228, 235, 243]) # Aug
        week_ticks_jul_dec.extend([244, 251, 259, 266, 273]) # Sep
        week_ticks_jul_dec.extend([274, 281, 289, 296, 304]) # Oct
        week_ticks_jul_dec.extend([305, 312, 320, 327, 334]) # Nov
        week_ticks_jul_dec.extend([335, 343, 350, 358, 365]) # Dec

        output_filename_jul_dec = f'Ice_Concentration_{os.path.splitext(os.path.basename(file_name))[0]}_Jul-Dec.png'
        output_suffix = f'_from_{start_year}_through_{end_date:%Y%m%d}'
        make_plot(
            df_jul_dec,
            area,
            title_text,
            f"{Path(output_filename_jul_dec).stem}{output_suffix}.png",
            month_centers_jul_dec,
            month_labels_jul_dec,
            week_ticks_jul_dec,
            182, 366,
            legend_loc='upper left'
        )

    # -------- Plot 3: October–December (new) --------
        df_oct_dec = df.copy()
        df_oct_dec['Month'] = df_oct_dec['DATE'].dt.month
        df_oct_dec = df_oct_dec[df_oct_dec['Month'].between(10, 12)]

        # Month tick positions (centers) and labels for Oct–Dec
        month_centers_oct_dec = [289, 320, 350]  # Oct 15, Nov 15, Dec 15
        month_labels_oct_dec = ['Oct', 'Nov', 'Dec']

        # Week tick positions (approximate) Oct–Dec
        week_ticks_oct_dec = []
        week_ticks_oct_dec.extend([281, 289, 296, 304]) # Oct
        week_ticks_oct_dec.extend([312, 320, 327, 334]) # Nov
        week_ticks_oct_dec.extend([343, 350, 358, 365]) # Dec

        output_filename_oct_dec = f'Ice_Concentration_{os.path.splitext(os.path.basename(file_name))[0]}_Oct-Dec.png'
        output_suffix = f'_from_{start_year}_through_{end_date:%Y%m%d}'
        make_plot(
            df_oct_dec,
            area,
            title_text,
            f"{Path(output_filename_oct_dec).stem}{output_suffix}.png",
            month_centers_oct_dec,
            month_labels_oct_dec,
            week_ticks_oct_dec,
            274, 365,
            legend_loc='upper left'
        )


    # -------- Plot 4: October–January (new) --------
        df_oct_jan = df.copy()
        df_oct_jan['Month'] = df_oct_jan['DATE'].dt.month
        df_oct_jan['Year'] = df_oct_jan['DATE'].dt.year
        
        # Filter to only include Oct-Dec and January
        df_oct_jan = df_oct_jan[
            (df_oct_jan['Month'].isin([10, 11, 12])) | 
            (df_oct_jan['Month'] == 1)
        ].copy()
        
        # Create PlotSeason: For Oct-Dec use current year, for Jan use previous year
        # This groups Oct-Dec-Jan together (e.g., Oct-Dec 2024 with Jan 2025 = Season 2024)
        def assign_plot_season(row):
            if row['Month'] >= 10:  # Oct, Nov, Dec
                return row['Year']
            else:  # Jan
                return row['Year'] - 1
        
        df_oct_jan['PlotSeason'] = df_oct_jan.apply(assign_plot_season, axis=1)

        # Adjust day of year for January (add 365 to make it continuous after December)
        def adjust_day_of_year(row):
            if row['Month'] >= 10:  # Oct, Nov, Dec
                return row['DATE'].dayofyear
            else:  # Jan
                return row['DATE'].dayofyear + 365
        
        df_oct_jan['AdjustedDayOfYear'] = df_oct_jan.apply(adjust_day_of_year, axis=1)

        # NOW replace Season with PlotSeason (this is the key step)
        df_oct_jan['Season'] = df_oct_jan['PlotSeason']
        
        # Keep all necessary columns
        df_oct_jan_plot = df_oct_jan[['DATE', 'Concentration', 'AdjustedDayOfYear', 'Season', 'Area']].copy()
        
        
        '''
        # Debug output to verify data
        print(f"\n=== Oct-Jan Plot Debug Info ===")
        print(f"Total records in Oct-Jan data: {len(df_oct_jan_plot)}")
        print(f"Date range: {df_oct_jan_plot['DATE'].min()} to {df_oct_jan_plot['DATE'].max()}")
        print(f"Seasons available: {sorted(df_oct_jan_plot['Season'].unique())}")
        max_season = df_oct_jan_plot['Season'].max()
        current_season_data = df_oct_jan_plot[df_oct_jan_plot['Season'] == max_season]
        print(f"Current season ({max_season}) data points: {len(current_season_data)}")
        print(f"Current season date range: {current_season_data['DATE'].min()} to {current_season_data['DATE'].max()}")
        print(f"Latest data point: Date={current_season_data['DATE'].max()}, Concentration={current_season_data[current_season_data['DATE']==current_season_data['DATE'].max()]['Concentration'].values[0]:.2f}%")
        print("================================\n")
        '''
        
        # Month tick positions and labels
        month_centers_oct_jan = [289, 320, 350, 381]
        month_labels_oct_jan = ['Oct', 'Nov', 'Dec', 'Jan']

        # Week tick positions
        week_ticks_oct_jan = []
        week_ticks_oct_jan.extend([281, 289, 296, 304])      # Oct
        week_ticks_oct_jan.extend([312, 320, 327, 334])      # Nov
        week_ticks_oct_jan.extend([343, 350, 358, 365])      # Dec
        week_ticks_oct_jan.extend([373, 380, 387, 396])      # Jan

        output_filename_oct_jan = f'Ice_Concentration_{os.path.splitext(os.path.basename(file_name))[0]}_Oct-Jan.png'
        
        output_suffix = f'_from_{start_year}_through_{end_date:%Y%m%d}'
        make_plot(
            df_oct_jan_plot,
            area,
            title_text,
            f"{Path(output_filename_oct_jan).stem}{output_suffix}.png",
            month_centers_oct_jan,
            month_labels_oct_jan,
            week_ticks_oct_jan,
            274, 396,
            legend_loc='upper left'
        )


        # -------- Weekly Climatology Text (English and French) --------
        # Prepare data for climatology
        latest_date = selected_week['DATE'].max()
        latest_row = df[df['DATE'] == latest_date].iloc[0]
        latest_year = int(latest_row['Season'])
        latest_conc = float(annual.loc[annual['Year'] == latest_year, 'Concentration'].iloc[0] * 100)

        month = latest_date.month
        day = latest_date.day
        wom = week_of_month_from_day(day)
        month_name = latest_date.strftime('%B')

        # Build the "same week" subset (same month and week-of-month across all years)
        df_same_week = df.copy()
        df_same_week['Month'] = df_same_week['DATE'].dt.month
        df_same_week['Day'] = df_same_week['DATE'].dt.day
        df_same_week['WeekOfMonth'] = df_same_week['Day'].apply(week_of_month_from_day)

        same_week = df_same_week[(df_same_week['Month'] == month) & (df_same_week['WeekOfMonth'] == wom)]

        if not same_week.empty:
            # Some years could have >1 record within a week window; aggregate to one per year (mean)
            per_year = (same_week
                        .groupby('Season', as_index=False)
                        .agg(Concentration=('Concentration', 'mean'),
                             SampleCount=('Concentration', 'size'),
                             AnyDate=('DATE', 'max')))

            # Sort ascending for "lowest" ranking
            per_year_sorted_asc = per_year.sort_values('Concentration', ascending=True, kind='mergesort').reset_index(drop=True)

            # Compute rank (lowest first)
            per_year_sorted_asc['RankLowest'] = per_year_sorted_asc['Concentration'].rank(method='min', ascending=True).astype(int)

            # Stats
            n_years = len(per_year_sorted_asc)
            median_conc = per_year_sorted_asc['Concentration'].median()
            mean_conc = per_year_sorted_asc['Concentration'].mean()

            # Latest year's rank and value
            latest_year_row = per_year_sorted_asc[per_year_sorted_asc['Season'] == latest_year]
            if latest_year_row.empty:
                latest_rank_str = "N/A"
            else:
                latest_rank_val = int(latest_year_row['RankLowest'].iloc[0])
                latest_rank_str = f"{ordinal(latest_rank_val)} lowest of {n_years}"

            # Calculate area in km²
            total_area = df['Area'].iloc[0]

            # Calculate ice area covered in km²
            latest_ice_area = latest_conc * total_area / 100
            
            # Calculate median and average ice areas
            median_ice_area = median_conc * total_area / 100
            average_ice_area = mean_conc * total_area / 100

            # Calculate differences
            median_difference = latest_ice_area - median_ice_area
            average_difference = latest_ice_area - average_ice_area

            # Top 7 lowest and highest lists
            lowest7 = per_year_sorted_asc.head(min(7, n_years))
            highest7 = per_year_sorted_asc.sort_values('Concentration', ascending=False, kind='mergesort').head(min(7, n_years))

            # Call the function to generate both English and French climatology files
            base_name_no_ext = os.path.splitext(os.path.basename(file_name))[0]
            write_weekly_climatology(
                same_week, 
                base_name_no_ext, 
                latest_date, 
                latest_year, 
                latest_conc, 
                latest_rank_str,
                month_name, 
                wom, 
                median_conc, 
                mean_conc, 
                latest_ice_area, 
                median_ice_area,
                average_ice_area, 
                median_difference, 
                average_difference, 
                lowest7, 
                highest7, 
                total_area
            )

        # Final info
        print(f"Area used: {area:,.0f} km²")

    except Exception as e:
        raise SystemExit(f"Error while creating overlays: {e}") from e
