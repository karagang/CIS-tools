#!/usr/bin/env python3
"""
Great_Lakes_Ice_Chart_Generator.py

Outputs 8 PNG files to the output folder beside this script (4 in English, 4 in French):
1. Combined (3-panel stack)
2. Recent Years (Individual)
3. Heavier Years (Individual)
4. Lighter Years (Individual)

Features:
- User selects Start Year.
- Dynamic Average calculation.
- Saves files instead of displaying on screen.
- Generates both English and French versions of all plots.
"""

import os, sys
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
from matplotlib.ticker import FuncFormatter
import seaborn as sns
from datetime import datetime

# --- Configuration ---
DEFAULT_FILENAME = "superior_with_average.csv"

def get_file_path():
    default_path = os.path.join(os.path.dirname(__file__), DEFAULT_FILENAME)
    if os.path.exists(default_path):
        val = input(f"Found default file. Press Enter to use, or type path:\n[{default_path}]: ").strip()
        path = val if val else default_path
    else:
        path = input("Enter CSV path: ").strip()
    path = path.strip('"').strip("'")
    if not os.path.exists(path):
        print(f"Error: File not found at {path}")
        sys.exit(1)
    return path

def get_start_year(available_years):
    years_int = sorted([int(y) for y in available_years])
    min_y = years_int[0]
    max_y = years_int[-1]
    print("-" * 40)
    print(f"Data available from {min_y} to {max_y}.")
    while True:
        val = input(f"Enter Start Year for comparison [Default: {min_y}]: ").strip()
        if val == "": return min_y
        try:
            val_int = int(val)
            if val_int < min_y or val_int > max_y:
                print(f"Please enter a year between {min_y} and {max_y}.")
                continue
            return val_int
        except ValueError:
            print("Invalid input.")

def prepare_plot_date(date_str):
    try:
        dt = datetime.strptime(date_str + '-2000', '%d-%b-%Y')
        if dt.month >= 8: return dt.replace(year=1999)
        else: return dt.replace(year=2000)
    except ValueError: return None

def get_years_sorted_by_max(df, year_cols):
    max_vals = {}
    for y in year_cols:
        max_vals[y] = df[y].max()
    return max_vals

def draw_subplot(ax, df, plot_years, current_year, avg_col_name, label_text, lang_dict, bg_years=None):
    """
    Standard drawing function used for both combined and individual charts.
    """
    # 1. Background
    if bg_years:
        for y in bg_years:
            if y not in plot_years and y != current_year:
                ax.plot(df['PlotDate'], df[y], color='lightgrey', linewidth=0.8, alpha=0.3)

    # 2. Comparison Years
    colors = sns.color_palette("husl", len(plot_years))
    for i, y in enumerate(plot_years):
        if y == current_year: continue 
        mx = df[y].max()
        label = f"{y} ({lang_dict['max']}: {mx:.1f}%)"
        ax.plot(df['PlotDate'], df[y], color=colors[i], linewidth=1.5, alpha=0.9, label=label)

    # 3. Dynamic Average
    if avg_col_name and avg_col_name in df.columns:
        avg_max = df[avg_col_name].max()
        label_avg = f"{lang_dict['avg']} ({lang_dict['max']}: {avg_max:.1f}%)"
        ax.plot(df['PlotDate'], df[avg_col_name], color='lime', linewidth=2.0, 
                linestyle='--', alpha=1.0, zorder=9, label=label_avg)

    # 4. Current Year
    curr_max = df[current_year].max()
    ax.plot(df['PlotDate'], df[current_year], color='red', linewidth=3.0, 
            alpha=1.0, zorder=10, label=f"{current_year} ({lang_dict['max']}: {curr_max:.1f}%)")

    # 5. Text Box
    ax.text(0.98, 0.96, label_text, transform=ax.transAxes, 
            horizontalalignment='right', verticalalignment='top',
            fontsize=11, fontweight='bold',
            bbox=dict(boxstyle="round,pad=0.3", fc="white", ec="gray", alpha=0.9))

    # 6. Formatting
    ax.xaxis.set_major_formatter(FuncFormatter(lang_dict['fmt_date']))
    ax.xaxis.set_major_locator(mdates.DayLocator(bymonthday=[1, 15]))
    ax.set_xlim(pd.Timestamp('1999-11-01'), pd.Timestamp('2000-06-30'))
    ax.set_ylim(0, 100)
    ax.set_ylabel(lang_dict['y_axis'])
    ax.grid(True, linestyle=':', alpha=0.6)
    ax.legend(loc='upper left', fontsize='small', frameon=True, framealpha=0.9)

def main():
    output_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'output')
    # Support long Windows paths, including deeply nested project folders.
    if os.name == 'nt' and not output_dir.startswith('\\\\?\\'):
        if output_dir.startswith('\\\\'):
            output_dir = '\\\\?\\UNC\\' + output_dir[2:]
        else:
            output_dir = '\\\\?\\' + output_dir
    os.makedirs(output_dir, exist_ok=True)

    csv_path = get_file_path()
    print(f"Reading {csv_path}...")

    # --- Data Prep ---
    df = pd.read_csv(csv_path, na_values=['NA', 'na', ''])
    if 'Date' not in df.columns:
        sys.exit("Error: First column must be 'Date'")

    df['PlotDate'] = df['Date'].apply(lambda x: prepare_plot_date(str(x)))
    df = df.dropna(subset=['PlotDate']).sort_values('PlotDate')

    data_cols = [c for c in df.columns if c not in ['Date', 'PlotDate']]
    year_cols = []
    for c in data_cols:
        if c == 'Average': continue
        try:
            int(c)
            year_cols.append(c)
        except: continue
    year_cols.sort(key=lambda x: int(x))

    # --- User Input & Filtering ---
    start_year_int = get_start_year(year_cols)
    current_year = year_cols[-1]
    history_years = [y for y in year_cols[:-1] if int(y) >= start_year_int]

    print(f"Calculating dynamic average ({start_year_int}-{history_years[-1]})...")
    df['CalculatedAvg'] = df[history_years].mean(axis=1)

    # --- Statistics Logic ---
    relevant_years = history_years + [current_year]
    max_stats = get_years_sorted_by_max(df, relevant_years)
    curr_max = max_stats[current_year]
    
    # Identify Year Categories
    recent_years = history_years[-5:]
    
    greater_years = [y for y in history_years if max_stats[y] > curr_max]
    greater_years.sort(key=lambda y: max_stats[y], reverse=True)
    top_greater = greater_years[:5]
    
    lower_years = [y for y in history_years if max_stats[y] < curr_max]
    lower_years.sort(key=lambda y: max_stats[y])
    top_lower = lower_years[:5]
    if not top_lower: 
        plot_lower = sorted(history_years, key=lambda y: max_stats[y])[:5] # Fallback
    else: 
        plot_lower = top_lower

    # --- Generate Both Languages ---
    for lang in ["EN", "FR"]:
        print(f"\n--- Generating {lang} Plots ---")
        
        # Language-Specific Definitions
        if lang == "EN":
            main_title = "Lake Superior Ice Coverage"
            lang_dict = {
                'y_axis': "Ice Cover (%)",
                'avg': "Average",
                'max': "Max",
                'fmt_date': lambda x, pos: mdates.num2date(x).strftime('%b-%d')
            }
            
            desc1 = f"Most recent Years\n({start_year_int} to {current_year})"
            
            if not top_greater: desc2 = f"Years with Highest Max Coverage\n(None found in {start_year_int} to {current_year})"
            else: desc2 = f"Years with Highest Max Coverage\n({start_year_int} to {current_year})"
            
            if not top_lower: desc3 = f"Years with Lowest Max Coverage\n(Lowest in {start_year_int} to {current_year})"
            else: desc3 = f"Years with Lowest Max Coverage\n({start_year_int} to {current_year})"
            
        elif lang == "FR":
            main_title = "Couverture des glaces du lac Supérieur"
            
            def fr_date_fmt(x, pos):
                dt = mdates.num2date(x)
                months = ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.']
                return f"{months[dt.month - 1]}-{dt.day:02d}"
                
            lang_dict = {
                'y_axis': "Couverture de glace (%)",
                'avg': "Moyenne",
                'max': "Max",
                'fmt_date': fr_date_fmt
            }
            
            desc1 = f"Années les plus récentes\n({start_year_int} à {current_year})"
            
            if not top_greater: desc2 = f"Années avec la couverture \nmaximale la plus élevée\n(Aucune trouvée de {start_year_int} à {current_year})"
            else: desc2 = f"Années avec la couverture \nmaximale la plus élevée\n({start_year_int} à {current_year})"
            
            if not top_lower: desc3 = f"Années avec la couverture \nmaximale la plus basse\n(La plus basse de {start_year_int} à {current_year})"
            else: desc3 = f"Années avec la couverture \nmaximale la plus basse\n({start_year_int} à {current_year})"

        # --- PLOTTING 1: The Combined 3-Panel Chart ---
        print("Generating Combined Chart...")
        sns.set_style("whitegrid")
        fig, (ax1, ax2, ax3) = plt.subplots(3, 1, figsize=(12, 18))
        
        fig.suptitle(main_title, fontsize=16, fontweight='bold', y=0.99)
        
        draw_subplot(ax1, df, recent_years, current_year, 'CalculatedAvg', desc1, lang_dict, bg_years=history_years)
        draw_subplot(ax2, df, top_greater, current_year, 'CalculatedAvg', desc2, lang_dict, bg_years=history_years)
        draw_subplot(ax3, df, plot_lower, current_year, 'CalculatedAvg', desc3, lang_dict, bg_years=history_years)

        plt.tight_layout()
        plt.subplots_adjust(top=0.96)
        
        filename_combined = f"Superior_{current_year}_Combined_{lang}.png"
        plt.savefig(os.path.join(output_dir, filename_combined), dpi=150)
        plt.close(fig) # Close to free memory
        print(f"Saved: {os.path.join(output_dir, filename_combined)}")

        # --- PLOTTING 2: The Individual Charts ---
        print("Generating Individual Charts...")

        configs = [
            (recent_years, desc1, "Recent"),
            (top_greater,  desc2, "Heavier"),
            (plot_lower,   desc3, "Lighter")
        ]

        for (p_years, desc, suffix) in configs:
            fig, ax = plt.subplots(1, 1, figsize=(12, 7)) # Smaller height for single plot
            
            # Draw
            draw_subplot(ax, df, p_years, current_year, 'CalculatedAvg', desc, lang_dict, bg_years=history_years)
            
            # Add Title to individual plot
            ax.set_title(main_title, fontsize=14, fontweight='bold')
            
            plt.tight_layout()
            filename = f"Superior_{current_year}_{suffix}_{lang}.png"
            plt.savefig(os.path.join(output_dir, filename), dpi=150)
            plt.close(fig)
            print(f"Saved: {os.path.join(output_dir, filename)}")

    print("\nAll sets generated successfully.")

if __name__ == "__main__":
    main()