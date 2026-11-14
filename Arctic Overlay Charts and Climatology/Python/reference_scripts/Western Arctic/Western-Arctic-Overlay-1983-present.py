import pandas as pd
import matplotlib.pyplot as plt
import datetime as dt
import numpy as np
import os

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
    # Read the data
    df = pd.read_csv(file_name)
    
    # Convert DATE to datetime and create Season (year) from DATE
    df['DATE'] = pd.to_datetime(df['DATE'], format='%Y%m%d')
    df['Season'] = df['DATE'].dt.year

    # Convert concentration to percentage
    df['Concentration'] = df['Concentration'] * 100

    # Filter data up to end date
    df = df[df['DATE'] <= end_date]

    # Add month column and filter for April-December
    df['Month'] = df['DATE'].dt.month
    df = df[df['Month'].between(4, 12)]

    # Get unique years
    years = sorted(df['Season'].unique())
    current_year = max(years)

    # Create the plot
    plt.figure(figsize=(12, 8))

    # First plot all older years in grey
    older_years = [year for year in years if year < current_year - 5]
    for year in older_years:
        year_data = df[df['Season'] == year]
        year_data = year_data.sort_values('DATE')
        plt.plot(year_data['DATE'].dt.dayofyear, year_data['Concentration'], 
                 color='lightgrey', alpha=0.3, linewidth=1)

    # Plot the previous 5 years with distinct colors
    colors = ['blue', 'purple', 'orange', 'brown', 'cyan']
    previous_5_years = range(current_year - 5, current_year)
    for year, color in zip(previous_5_years, colors):
        if year in years:  # Check if year exists in dataset
            year_data = df[df['Season'] == year]
            year_data = year_data.sort_values('DATE')
            plt.plot(year_data['DATE'].dt.dayofyear, year_data['Concentration'], 
                     label=str(year), color=color, alpha=0.7, linewidth=2)

    # Plot the current year in red
    current_year_data = df[df['Season'] == current_year]
    current_year_data = current_year_data.sort_values('DATE')
    plt.plot(current_year_data['DATE'].dt.dayofyear, current_year_data['Concentration'], 
             label=str(current_year), color='red', linewidth=2.5)

    # Customize the plot
    plt.ylabel('Ice Coverage (%) Couverture de glace')
    plt.title('Western Arctic -  Arctique occidental\n1983 - 2025')
    plt.grid(True, alpha=0.3)

    # Set y-axis limits
    plt.ylim(0, 100)

    # Set x-axis ticks to months (April to December)
    month_ticks = [91, 121, 152, 182, 213, 244, 274, 305, 335]
    month_labels = ['Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
    plt.xticks(month_ticks, month_labels)

    # Set x-axis limits to show only April-December
    plt.xlim(91, 366)  # Day 91 is April 1st

    # Add legend for only the last 5 years and current year
    plt.legend(title='Year/Année', bbox_to_anchor=(1.05, 1), loc='upper left')

    # Adjust layout to prevent legend cutoff
    plt.tight_layout()

    # Save the plot
    output_filename = f'Ice_Concentration_{file_name.split(".")[0]}.png'
    plt.savefig(output_filename, bbox_inches='tight', dpi=300)
    plt.close()
    
    print(f"Plot has been saved as '{output_filename}'")

except Exception as e:
    print(f"An error occurred while processing the file: {str(e)}")