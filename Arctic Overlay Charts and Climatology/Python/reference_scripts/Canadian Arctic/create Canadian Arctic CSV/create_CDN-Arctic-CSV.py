import pandas as pd

# Read the data files
eastern_data = pd.read_csv('Eastern_Arctic.csv')
western_data = pd.read_csv('Western_Arctic.csv')

# Print column names to debug
print("Eastern Arctic columns:")
print(eastern_data.columns.tolist())
print("\nWestern Arctic columns:")
print(western_data.columns.tolist())

# Define area values (in km²)
eastern_area = 1265567  # Eastern Arctic area in km²
western_area = 1296172  # Western Arctic area in km²
canadian_arctic_area = eastern_area + western_area  # Total area

# Keep DATE as string for merging, convert to datetime only for comparison
eastern_data['DATE_temp'] = pd.to_datetime(eastern_data['DATE'], format='%Y%m%d')
western_data['DATE_temp'] = pd.to_datetime(western_data['DATE'], format='%Y%m%d')

# Merge the datasets on the temporary datetime column
merged_data = pd.merge(eastern_data, western_data, on='DATE_temp', suffixes=('_East', '_West'))

# Keep the original DATE format from Eastern data
merged_data['DATE'] = merged_data['DATE_East']

# Create a new dataframe for Canadian Arctic
canadian_arctic = pd.DataFrame()

# Copy the DATE column in original YYYYMMDD format
canadian_arctic['DATE'] = merged_data['DATE']

# Get all columns from eastern data except DATE and Area
eastern_cols = [col for col in eastern_data.columns if col not in ['DATE', 'Area', 'DATE_temp']]

# List of columns to round to 4 decimal places
columns_to_round = ['Concentration', 'Old_Ice', 'First-Year-Ice', 'Young_Ice', 'New_Ice']

# Calculate weighted averages for each concentration column
for col in eastern_cols:
    col_east = col + '_East'
    col_west = col + '_West'
    
    # Check if both columns exist in merged data
    if col_east in merged_data.columns and col_west in merged_data.columns:
        # Calculate weighted average concentration
        canadian_arctic[col] = (
            (merged_data[col_east] * eastern_area + 
             merged_data[col_west] * western_area) / canadian_arctic_area
        )
        
        # Round to 4 decimal places if column is in the list
        if col in columns_to_round:
            canadian_arctic[col] = canadian_arctic[col].round(4)
            print(f"Processing column: {col} (rounded to 4 decimal places)")
        else:
            print(f"Processing column: {col}")
    else:
        print(f"Warning: Could not find {col_east} or {col_west}")

# Add the combined area
canadian_arctic['Area'] = canadian_arctic_area

# Reorder columns to match original format (Area first, then DATE, then others)
concentration_cols = [col for col in eastern_cols if col in canadian_arctic.columns]
cols = ['Area', 'DATE'] + concentration_cols
canadian_arctic = canadian_arctic[cols]

# Save to new CSV file
canadian_arctic.to_csv('Canadian_Arctic.csv', index=False)

print("\n" + "="*60)
print("Canadian Arctic CSV file created successfully!")
print("="*60)
print(f"\nFile: Canadian_Arctic.csv")
print(f"Total Area: {canadian_arctic_area:,.0f} km²")
print(f"Eastern Arctic Area: {eastern_area:,.0f} km²")
print(f"Western Arctic Area: {western_area:,.0f} km²")
print(f"Number of dates: {len(canadian_arctic)}")
print(f"\nFirst few rows of the new file:")
print(canadian_arctic.head())
print(f"\nColumn names in new file: {list(canadian_arctic.columns)}")
print(f"\nNew file shape: {canadian_arctic.shape}")