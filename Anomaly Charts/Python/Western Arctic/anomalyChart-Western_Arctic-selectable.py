"""Western Arctic: run this script and select a start year, month, and week."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from anomaly_analysis import run

if __name__ == "__main__":
    run('Western Arctic', Path(__file__).resolve().parent, 'Western_Arctic.csv')
