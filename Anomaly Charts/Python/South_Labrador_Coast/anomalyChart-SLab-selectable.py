"""South Labrador Coast: run this script and select a start year, month, and week."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from anomaly_analysis import run

if __name__ == "__main__":
    run('South Labrador Coast', Path(__file__).resolve().parent, 'weekly-SLab-Coast-1970-2025.csv')
