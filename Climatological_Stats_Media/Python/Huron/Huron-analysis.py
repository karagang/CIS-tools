"""Interactive analysis report; implementation is in ice_reports.py beside this file."""
from pathlib import Path
from ice_reports import main

if __name__ == '__main__':
    main('analysis', Path(__file__).resolve().parent)
