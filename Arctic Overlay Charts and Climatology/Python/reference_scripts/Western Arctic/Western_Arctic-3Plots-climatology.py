import pandas as pd
import matplotlib.pyplot as plt
import datetime as dt
import numpy as np
import os

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
        year_data = df[df['Season'] == year].sort_values('DATE')
        ax1.plot(year_data['DATE'].dt.dayofyear, year_data['Concentration'],
                 color='lightgrey', alpha=0.3, linewidth=1)

    # Plot the previous 5 years with distinct colors
    colors = ['blue', 'purple', 'orange', 'brown', 'cyan']
    previous_5_years = range(current_year - 5, current_year)
    for year, color in zip(previous_5_years, colors):
        if year in years:
            year_data = df[df['Season'] == year].sort_values('DATE')
            ax1.plot(year_data['DATE'].dt.dayofyear, year_data['Concentration'],
                     label=str(year), color=color, alpha=0.7, linewidth=2)

    # Plot the current year in red
    current_year_data = df[df['Season'] == current_year].sort_values('DATE')
    ax1.plot(current_year_data['DATE'].dt.dayofyear, current_year_data['Concentration'],
             label=str(current_year), color='red', linewidth=2.5)

    # Calculate and plot median ice coverage for this filtered period
    median_data = df.groupby(df['DATE'].dt.dayofyear)['Concentration'].median()
    ax1.plot(median_data.index, median_data.values,
             label='Median', color='black', linestyle='--', linewidth=2)

    # Left y-axis: Ice Coverage %
    ax1.set_ylabel('Ice Coverage (%) Couverture de glace', color='black', fontweight='bold')
    ax1.tick_params(axis='y', labelcolor='black')
    ax1.set_ylim(0, 100)
    ax1.grid(True, alpha=0.3)

    # Right y-axis: Ice Area Covered
    ax2 = ax1.twinx()
    ax2.set_ylabel('Ice Area Covered (Million km²)', color='darkgreen', fontweight='bold')
    ax2.tick_params(axis='y', labelcolor='darkgreen')
    ax2.set_ylim(0, area / 1e6)

    # Title
    ax1.set_title(title, fontweight='bold')

    # X-axis ticks
    ax1.set_xticks(month_centers, minor=False)
    ax1.set_xticklabels(month_labels, minor=False)
    ax1.set_xticks(week_ticks, minor=True)
    ax1.tick_params(axis='x', which='minor', length=3)
    ax1.tick_params(axis='x', which='major', length=6)

    # X-axis limits
    ax1.set_xlim(xlim_start, xlim_end)

    # Legend
    ax1.legend(title='Year/Année', loc=legend_loc, framealpha=0.95, fontsize=12, title_fontsize=13, markerscale=1.5)

    plt.tight_layout()
    plt.savefig(output_filename, bbox_inches='tight', dpi=300)
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

def write_weekly_climatology(df_filtered, file_name_base):
    """
    Create a short climatology text file for the latest available date in df_filtered.
    Compares the (month, week-of-month) of the latest date to the same week across all years.
    Outputs rank (by lowest), 7 lowest and 7 highest years, and median/average.
    """
    if df_filtered.empty:
        print("No data available to compute weekly climatology.")
        return

    latest_date = df_filtered['DATE'].max()
    latest_row = df_filtered[df_filtered['DATE'] == latest_date].iloc[0]
    latest_year = int(latest_row['Season'])
    latest_conc = float(latest_row['Concentration'])

    month = latest_date.month
    day = latest_date.day
    wom = week_of_month_from_day(day)  # 1..4
    month_name = latest_date.strftime('%B')

    # Build the "same week" subset (same month and week-of-month across all years)
    df_same_week = df_filtered.copy()
    df_same_week['Month'] = df_same_week['DATE'].dt.month
    df_same_week['Day'] = df_same_week['DATE'].dt.day
    df_same_week['WeekOfMonth'] = df_same_week['Day'].apply(week_of_month_from_day)

    same_week = df_same_week[(df_same_week['Month'] == month) & (df_same_week['WeekOfMonth'] == wom)]

    if same_week.empty:
        print("No comparable weeks found for climatology output.")
        return

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

    # Latest year's rank and value (using the same aggregation logic)
    # If latest year appears multiple times in original week window, we use the mean (consistent).
    latest_year_row = per_year_sorted_asc[per_year_sorted_asc['Season'] == latest_year]
    if latest_year_row.empty:
        # Edge case: latest date exists but got filtered out in same-week logic (shouldn't happen)
        latest_rank_str = "N/A"
    else:
        latest_rank_val = int(latest_year_row['RankLowest'].iloc[0])
        latest_rank_str = f"{ordinal(latest_rank_val)} lowest of {n_years}"
        
     # Calculate area in km²
    total_area = df_filtered['Area'].iloc[0]  # Assuming Area is constant

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
    
    # Get the total area from the first row
    total_area = df_filtered['Area'].iloc[0]


    # Prepare output text
    lines = []
    lines.append("Weekly Climatology Summary for the Western Arctic")
    lines.append("--------------------------")
    lines.append(f"Latest date observed: {latest_date.strftime('%Y-%m-%d')} (YYYY-MM-DD)")
    lines.append(f"Week definition: days 1–7=1st, 8–14=2nd, 15–21=3rd, 22–31=4th")
    lines.append(f"Latest week classification: {ordinal(wom)} week of {month_name}")
    lines.append("")
    lines.append(f"Comparison set: All years for {ordinal(wom)} week of {month_name}")
    lines.append(f"Number of years in comparison: {n_years}")
    lines.append(f"Latest year's value ({latest_year}): {latest_conc:.2f}%")
    lines.append(f"Rank (by lowest): {latest_rank_str}")
    lines.append(f"Median (this week across all years): {median_conc:.2f}%")
    lines.append(f"Average (this week across all years): {mean_conc:.2f}%")
    lines.append(f"Latest ice area covered: {latest_ice_area:.2f} km²")
    lines.append(f"Median week ice area: {median_ice_area:.2f} km²")
    lines.append(f"Difference from median: {median_difference:.2f} km²")
    lines.append(f"Average week ice area: {average_ice_area:.2f} km²")
    lines.append(f"Difference from average: {average_difference:.2f} km²")
    lines.append("")

    lines.append("Seven lowest years (this week):")
    for i, r in lowest7.iterrows():
        ice_area = r['Concentration'] * total_area / 100
        lines.append(f"  - {int(r['Season'])}: {r['Concentration']:.2f}% | {ice_area:.2f} km²")

    lines.append("")
    lines.append("Seven highest years (this week):")
    for i, r in highest7.iterrows():
        ice_area = r['Concentration'] * total_area / 100
        lines.append(f"  - {int(r['Season'])}: {r['Concentration']:.2f}% | {ice_area:.2f} km²")

    lines.append("")

    # Write to file
    output_txt = f"Climatology_{file_name_base}_{latest_date.strftime('%Y%m%d')}.txt"
    with open(output_txt, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"Weekly climatology text has been saved as '{output_txt}'")


# Prompt for data file name
while True:
    file_name = input("Please enter the name of the data file: ")
    if os.path.exists(file_name):
        break
    else:
        print(f"Error: File '{file_name}' not found. Please try again.")

# Prompt for end date
while True:
    try:
        end_date = input("Please enter the end date (YYYYMMDD): ")
        end_date = pd.to_datetime(end_date, format='%Y%m%d')
        break
    except ValueError:
        print("Invalid date format. Please use YYYYMMDD format.")

try:
    # Read the data with additional error handling
    df = pd.read_csv(file_name)

    # Convert Concentration column safely
    df['Concentration'] = df['Concentration'].apply(safe_convert)

    # Remove any rows with NaN in Concentration
    df = df.dropna(subset=['Concentration'])

    # Read the data
    df = pd.read_csv(file_name)

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
    df = df[df['DATE'] <= end_date].copy()

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
    title_text = 'Western Arctic -  Arctique occidental\n 1983 - 2025'
    make_plot(
        df_apr_dec,
        area,
        title_text,
        output_filename_apr_dec,
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
    # Title kept the same for consistency with your original; append note if you prefer.
    make_plot(
        df_jul_dec,
        area,
        title_text,  # or: title_text + ' (Jul–Dec only)'
        output_filename_jul_dec,
        month_centers_jul_dec,
        month_labels_jul_dec,
        week_ticks_jul_dec,
        182, 366,
        legend_loc='lower left'
    )
# -------- Plot 3: October–December (new) --------
    df_oct_dec = df.copy()
    df_oct_dec['Month'] = df_oct_dec['DATE'].dt.month
    df_oct_dec = df_oct_dec[df_oct_dec['Month'].between(10, 12)]

    # Month tick positions (centers) and labels for Oct–Dec
    month_centers_oct_dec = [274, 305, 335]
    month_labels_oct_dec = ['Oct', 'Nov', 'Dec']

    # Week tick positions (approximate) Oct–Dec
    week_ticks_oct_dec = []
    week_ticks_oct_dec.extend([281, 289, 296, 304]) # Oct
    week_ticks_oct_dec.extend([312, 320, 327, 334]) # Nov
    week_ticks_oct_dec.extend([343, 350, 358, 365]) # Dec

    output_filename_oct_dec = f'Ice_Concentration_{os.path.splitext(os.path.basename(file_name))[0]}_Oct-Dec.png'
    make_plot(
        df_oct_dec,
        area,
        title_text,
        output_filename_oct_dec,
        month_centers_oct_dec,
        month_labels_oct_dec,
        week_ticks_oct_dec,
        274, 365,
        legend_loc='lower right'
    )

    
    # -------- Weekly Climatology Text (new) --------
    # Use the entire filtered df (<= end_date) to determine the latest week and compare across years
    base_name_no_ext = os.path.splitext(os.path.basename(file_name))[0]
    write_weekly_climatology(df, base_name_no_ext)

    # Final info
    print(f"Area used: {area:,.0f} km²")

except Exception as e:
    print(f"An error occurred while processing the file: {str(e)}")