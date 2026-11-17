"""Regenerate documentation examples using the unmodified report implementation.

Run from any directory: python Climatological_Stats_Media/examples/generate_examples.py
Requires plotly, kaleido, Pillow, and Chrome for PNG export.
"""
from pathlib import Path
import hashlib
import importlib.util
import textwrap
from unittest.mock import patch

from PIL import Image, ImageDraw, ImageFont
import plotly.graph_objects as go

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
REGION = ROOT / 'Python' / 'GRTLKS'
spec = importlib.util.spec_from_file_location('ice_reports', REGION / 'ice_reports.py')
reports = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reports)


def run(mode, year, region=REGION, csv='bas_with_average.csv'):
    with patch('builtins.input', side_effect=[csv, '1973' if region == REGION else '1970', str(year)]):
        result = reports.main(mode, region)
    if result is None:
        raise RuntimeError(f'{mode} failed')
    # Keep the report intact except for replacing the machine-specific source path.
    source = region / csv
    content = result.read_text(encoding='utf-8').replace(str(source.resolve()), source.relative_to(ROOT).as_posix())
    content = '\n'.join(line.rstrip() for line in content.split('\n'))
    start = 1973 if region == REGION else 1970
    destination = HERE / f'{source.stem}_{mode}_{year}_from_{start}{result.suffix}'
    destination.write_text(content, encoding='utf-8')
    return content


def font(size, mono=False):
    # Windows fonts are used here; DejaVu provides a portable fallback.
    names = ['C:/Windows/Fonts/consola.ttf', 'DejaVuSansMono.ttf'] if mono else ['C:/Windows/Fonts/segoeui.ttf', 'DejaVuSans.ttf']
    for name in names:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default(size=size)


def preview(filename, title, subtitle, excerpt):
    lines = []
    for line in excerpt:
        lines.extend(textwrap.wrap(line, width=94, subsequent_indent='    ', replace_whitespace=False) or [''])
    height = 175 + len(lines) * 27 + 65
    image = Image.new('RGB', (1320, height), '#f4f7fb')
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, 1320, 125), fill='#16354b')
    draw.text((40, 22), title, font=font(34), fill='white')
    draw.text((40, 74), subtitle, font=font(23), fill='#d3e7f1')
    draw.rounded_rectangle((25, 145, 1295, height - 55), radius=12, fill='white', outline='#d7e0e8')
    for index, line in enumerate(lines):
        draw.text((45, 162 + index * 27), line, font=font(22, True), fill='#172b3a')
    draw.text((40, height - 40), 'Excerpt from generated text output • Full report linked below the preview', font=font(20), fill='#475e70')
    image.save(HERE / filename)


def main():
    top = run('topten', 2014)
    low = run('climatology', 2024)
    monthly = run('monthly', 2024, ROOT / 'Python' / 'South Labrador Coast', 'weekly-SLab-Coast-1970-2025.csv')
    top_lines = top.splitlines()
    start = next(i for i, line in enumerate(top_lines) if line.startswith('Feb 01:'))
    end = next(i for i in range(start + 1, len(top_lines)) if top_lines[i].startswith('Feb 04:'))
    preview('great-lakes-topten.png', 'Top Ten • Great Lakes', 'Target 2014 | Comparison 1973–2014 | bas_with_average.csv', top_lines[start:end])
    low_lines = low.splitlines()
    end = next(i for i, line in enumerate(low_lines) if line.startswith('Jan 08:'))
    preview('great-lakes-climatology.png', 'Climatology rankings • Great Lakes', 'Target 2024 | Comparison 1973–2024 | bas_with_average.csv', low_lines[5:end])
    month_lines = monthly.splitlines()
    start = month_lines.index('February')
    end = month_lines.index('March')
    preview('south-labrador-monthly.png', 'Monthly seasonal maxima • South Labrador Coast', 'Seasons 1970–71 to 2024–25 | February excerpt', month_lines[start:end])
    figures = []
    original = go.Figure.to_html

    def capture(self, *args, **kwargs):
        figures.append(self)
        return original(self, *args, **kwargs)

    with patch.object(go.Figure, 'to_html', capture):
        run('analysis', 2024)
    names = ['mean', 'observations', 'sum', 'standard-deviation']
    assert len(figures) == 4
    for name, figure in zip(names, figures):
        figure.write_image(str(HERE / f'great-lakes-{name}.png'), width=1100, height=480, scale=1.5)
    for source in [REGION / 'bas_with_average.csv', ROOT / 'Python' / 'South Labrador Coast' / 'weekly-SLab-Coast-1970-2025.csv']:
        print(hashlib.sha256(source.read_bytes()).hexdigest(), source.relative_to(ROOT))


if __name__ == '__main__':
    main()
