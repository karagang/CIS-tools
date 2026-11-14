"""Run with python -m unittest discover -s ."""
from pathlib import Path
import tempfile
import unittest
import hashlib
import pandas as pd
from overlay_analysis import load_csv, merge_canadian, seasonal_data, weekly_stats, write_reports


class AnalysisTests(unittest.TestCase):
    def test_weekly_means_and_zero(self):
        frame = pd.DataFrame({'DATE': pd.to_datetime(['1990-04-16', '1991-04-16', '1991-04-17', '1992-04-16']),
                              'Concentration': [1., 0., .4, .6], 'Area': [1000] * 4})
        filtered = frame[frame.DATE.dt.year >= 1991]
        latest, week, annual = weekly_stats(filtered)
        self.assertEqual(week, 3)
        self.assertEqual(annual.index.tolist(), [1991, 1992])
        self.assertAlmostEqual(annual.loc[1991], 20)
        self.assertAlmostEqual(annual.median(), 40)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            paths = write_reports(filtered, ('Region', 'Région'), 1991, latest, output, 'test',
                                  [('source.csv', 'abc', 4)])
            self.assertEqual(len(paths), 2)
            en, fr = [path.read_text(encoding='utf-8') for path in paths]
            self.assertIn('Median: 40.00%', en)
            self.assertIn('Médiane : 40.00 %', fr)
            self.assertIn('1992 anomaly: +20.00', en)
            self.assertTrue(en.endswith('source.csv\nSHA-256: abc\nRows: 4\n'))
            self.assertTrue(fr.endswith('source.csv\nSHA-256: abc\nLignes: 4\n'))

    def test_weighted_merge_dates_hash_and_duplicates(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            east, west, merged = [root / name for name in ('east.csv', 'west.csv', 'merged.csv')]
            east.write_text('DATE,Concentration,First-Year Ice\n20200101,0,0.2\n20200108,0.2,0.4\n', encoding='utf-8')
            west.write_text('DATE,Concentration,First-Year-Ice\n20200101,1,0.6\n20200115,0.6,0.7\n', encoding='utf-8')
            sources, excluded = merge_canadian(east, west, merged)
            data, info = load_csv(merged)
            self.assertEqual(excluded, (1, 1))
            self.assertEqual(len(data), 1)
            self.assertEqual(data.Area.iloc[0], 2561739)
            self.assertAlmostEqual(data.Concentration.iloc[0], round(1296172 / 2561739, 4))
            self.assertIn('First-Year-Ice', data)
            self.assertEqual(sources[0][1], hashlib.sha256(east.read_bytes()).hexdigest())
            west.write_text('DATE,Concentration\n20200101,1\n20200101,0\n', encoding='utf-8')
            with self.assertRaises(ValueError):
                merge_canadian(east, west, merged)

    def test_calendar_and_new_year(self):
        frame = pd.DataFrame({'DATE': pd.to_datetime(['2019-03-01', '2020-03-01', '2020-02-29',
                                                      '2019-10-01', '2020-01-01'])})
        dates = seasonal_data(frame, 101, 1231)
        self.assertEqual(dates.X.iloc[0], dates.X.iloc[1])
        self.assertEqual(dates.X.iloc[0] - dates.X.iloc[2], 1)
        winter = seasonal_data(frame, 1001, 131)
        self.assertEqual(winter.Season.tolist(), [2019, 2019])
        self.assertLess(winter.X.iloc[0], winter.X.iloc[1])

    def test_invalid_data_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'bad.csv'
            for row in ('20200101,1.1', '20200230,0.1', '20200101,NaN'):
                path.write_text('DATE,Concentration\n' + row + '\n', encoding='utf-8')
                with self.assertRaises(ValueError):
                    load_csv(path)


if __name__ == '__main__':
    unittest.main()
