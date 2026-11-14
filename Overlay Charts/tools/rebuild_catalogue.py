"""Build GitHub-readable guides and catalogue from the preserved HTML guides.

Run from any folder: python "Overlay Charts/tools/rebuild_catalogue.py"
Only Python's standard library is required. Original programs and ZIPs are untouched.
"""
from dataclasses import dataclass, field
from html import escape
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit, urlunsplit
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / 'docs'
REPOSITORY = 'https://github.com/karagang/CIS-tools'
SOURCE_PREFIX = REPOSITORY + '/blob/main/Overlay%20Charts/'
RAW_PREFIX = REPOSITORY + '/raw/refs/heads/main/Overlay%20Charts/'


@dataclass
class Node:
    tag: str
    attrs: dict = field(default_factory=dict)
    children: list = field(default_factory=list)

    def descendants(self, tag):
        for child in self.children:
            if isinstance(child, Node):
                if child.tag == tag:
                    yield child
                yield from child.descendants(tag)


class Tree(HTMLParser):
    def __init__(self, text):
        super().__init__(convert_charrefs=True)
        self.root = Node('document')
        self.stack = [self.root]
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        node = Node(tag, dict(attrs))
        self.stack[-1].children.append(node)
        if tag not in ('area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr'):
            self.stack.append(node)

    def handle_endtag(self, tag):
        for index in range(len(self.stack) - 1, 0, -1):
            if self.stack[index].tag == tag:
                self.stack = self.stack[:index]
                break

    def handle_data(self, text):
        self.stack[-1].children.append(text)


def text(node):
    if isinstance(node, str):
        return node
    if node.tag == 'br':
        return ' — '
    return ''.join(text(child) for child in node.children)


def markdown_link(href):
    parsed = urlsplit(href)
    if not parsed.scheme and parsed.path.endswith('.zip'):
        return RAW_PREFIX + 'docs/downloads/' + quote(Path(parsed.path).name)
    if not parsed.scheme and parsed.path.endswith('.html'):
        path = parsed.path[:-5] + '.md'
        if path.endswith('index.md'):
            path = path[:-8] + 'README.md'
        parsed = parsed._replace(path=path)
        if parsed.fragment == 'programs' and path.endswith('README.md'):
            parsed = parsed._replace(fragment='program-catalogue')
    return urlunsplit(parsed)


def markdown(node):
    if isinstance(node, str):
        return re.sub(r'\s+', ' ', node)
    tag = node.tag
    content = ''.join(markdown(child) for child in node.children)
    if tag == 'div' and node.attrs.get('class') == 'actions':
        return '\n\n' + ' · '.join(markdown(child).strip() for child in node.children if isinstance(child, Node)) + '\n\n'
    if tag == 'div' and node.attrs.get('class') == 'meta':
        return '\n\n' + ' · '.join(text(child).strip() for child in node.children if isinstance(child, Node)) + '\n\n'
    if tag in ('head', 'header', 'footer', 'nav', 'script', 'style'):
        return ''
    if tag in ('h1', 'h2', 'h3', 'h4'):
        return '\n\n' + '#' * int(tag[1]) + ' ' + re.sub(r'\s+', ' ', text(node)).strip() + '\n\n'
    if tag == 'pre':
        return '\n\n```powershell\n' + text(node).strip() + '\n```\n\n'
    if tag == 'code':
        return '`' + text(node).strip() + '`'
    if tag == 'a':
        return '[' + content.strip() + '](' + markdown_link(node.attrs.get('href', '')) + ')'
    if tag == 'img':
        alt = node.attrs.get('alt') or 'Example ice coverage chart'
        return '![' + alt.replace(']', '\\]') + '](' + node.attrs['src'] + ')'
    if tag in ('strong', 'b'):
        return '**' + content.strip() + '**'
    if tag in ('em', 'i'):
        return '*' + content.strip() + '*'
    if tag == 'br':
        return '\n\n'
    if tag == 'li':
        return '\n- ' + content.strip() + '\n'
    if tag == 'dt':
        return '\n\n**' + content.strip() + '**\n\n'
    if tag in ('p', 'div', 'section', 'figure', 'figcaption', 'main', 'dd', 'dl', 'ul', 'ol'):
        return '\n\n' + content.strip() + '\n\n'
    return content


def clean_markdown(value):
    return re.sub(r'\n[ \t]*\n(?:[ \t]*\n)+', '\n\n', value).strip() + '\n'


def relative(path):
    return quote(path, safe='/')


def save(path, content):
    # Retain existing document line endings; new guides use LF.
    newline = '\r\n' if path.exists() and b'\r\n' in path.read_bytes() else '\n'
    path.write_text(content, encoding='utf-8', newline=newline)


def build():
    catalogue = json.loads((ROOT / 'catalogue.json').read_text(encoding='utf-8'))
    table = ['| Program | Language | Source code | Instructions | PNG plot | Complete download |',
             '| --- | --- | --- | --- | --- | --- |']
    gallery = ['# Overlay Charts catalogue', '',
               'Great Lakes and East Coast programs, source code, instructions, PNG plots, and complete downloads. This catalogue works directly on GitHub and does not depend on the retired website.', '',
               '[Project instructions](../README.md) · [All PNG plots](assets/charts/) · [All downloads](downloads/)', '',
               '## Program catalogue', '']
    records = []
    for item in catalogue:
        html_path = DOCS / item['page']
        tree = Tree(html_path.read_text(encoding='utf-8'))
        main = next(tree.root.descendants('main'))
        links = [n.attrs.get('href', '') for n in main.descendants('a')]
        source = item.get('source') or unquote(next(h for h in links if h.startswith(SOURCE_PREFIX))[len(SOURCE_PREFIX):])
        archive_href = next(h for h in links if h.endswith('.zip'))
        archive = (html_path.parent / unquote(archive_href)).resolve().relative_to(ROOT).as_posix()
        plot = 'docs/assets/charts/' + item['image'] + '.png'
        guide = 'docs/' + str(Path(item['page']).with_suffix('.md')).replace('\\', '/')
        title = text(next(main.descendants('h1'))).strip()
        for path in (source, archive, plot):
            assert (ROOT / path).is_file(), path
        content = markdown(main)
        content += '\n\n[← All programs, source, and chart previews](../README.md)\n'
        save(ROOT / guide, clean_markdown(content))
        record = dict(item, source=source, guide=guide, archive=archive, plot=plot, title=title)
        records.append(record)
        table.append(f'| {item["stem"]} | {item["language"]} | [Code]({relative(source)}) | [Guide]({relative(guide)}) | [PNG]({relative(plot)}) | [ZIP]({relative(archive)}) |')
        gallery.extend([
            '### ' + title + ' (' + item['language'] + ')', '', item['description'], '',
            f'[Source code](../{relative(source)}) · [Instructions]({relative(guide.removeprefix("docs/"))}) · [Full-size PNG]({relative(plot.removeprefix("docs/"))}) · [Download ZIP]({RAW_PREFIX}{relative(archive)})', '',
            f'[![{escape(title)}](assets/thumbs/{relative(item["image"])}.webp)](assets/charts/{relative(item["image"])}.png)', '',
        ])
    save(DOCS / 'README.md', '\n'.join(gallery))
    introduction = '''# Overlay Charts — East Coast and Great Lakes

Python and C# programs for East Coast and Great Lakes ice-coverage overlays, with source code, CSV inputs, example PNG charts, and complete download packages.

**[Browse the full catalogue with chart previews](docs/README.md)**

The catalogue and all program guides now open directly within this repository. The earlier standalone website has been retired; no links to that site are required.

## Quick start

1. Select a program below or browse the [chart preview catalogue](docs/README.md).
2. Open its **Guide** for instructions or **Code** for the Python/C# source.
3. Open its **ZIP**, select GitHub's **Download raw file** button, and extract the complete package. The catalogue's Download ZIP links also download directly.
4. Keep the executable or Python script with its CSV inputs and configuration files, then follow the included README.

C# programs use Windows .NET Framework; Python packages include their dependency instructions. Update the supplied CSV snapshots when newer observations are available.

## Catalogue

Each row links directly to the source, a GitHub-readable guide, a full-size PNG example, and the complete runnable/rebuildable package.

'''
    ending = '''
## Source folders and downloads

- East Coast Python/C# programs and regional CSVs: [Gulf](Gulf/), [NewFoundLand](NewFoundLand/), and [SouthLabCoast](SouthLabCoast/).
- Shared C# charting engine: [Shared/OverlayCharts.cs](Shared/OverlayCharts.cs). Rebuild the six regional executables using [Build.ps1](Build.ps1); see [EAST_COAST.md](EAST_COAST.md).
- Great Lakes and legacy C# sources: [OtherCSharpSources](OtherCSharpSources/).
- Great Lakes and legacy Python sources: [PythonSources](PythonSources/).
- Complete distributions, original layouts, CSVs, and build scripts: [docs/downloads](docs/downloads/).
- All full-size PNG examples: [docs/assets/charts](docs/assets/charts/); [browse previews](docs/README.md).

Source snapshots in OtherCSharpSources and PythonSources use unique names for browsing. Use the ZIP downloads for their original folder layout, dependencies, data, and build scripts. All eight original ZIPs, six regional executables, source files, data, and image assets are preserved.

## What is included

- Six East Coast C# programs and six Python equivalents for Gulf, NE Newfoundland, and South Labrador Coast, with chosen-period and automatic-from-1990/91 modes.
- Six lake-specific C# overlay programs for Erie, Huron, Michigan, Ontario, Superior, and combined Great Lakes, producing English, French, and bilingual charts.
- A Python companion producing eighteen plots across all six Great Lakes regions.
- Great Lakes twelve-chart batch programs in C# and Python.
- Original combined-Great-Lakes three-chart generators in both languages.

The chart images and CSVs are snapshots. Programs use the supplied Average values; the catalogue does not fetch new observations or execute the programs in the browser.

## Catalogue maintenance

The GitHub-readable catalogue and guides are generated from the preserved HTML documentation. Run `python tools/rebuild_catalogue.py` after updating program documentation. The HTML catalogue remains available for offline viewing by downloading the repository and opening `docs/index.html`. See [PUBLISHING.md](PUBLISHING.md).
'''
    save(ROOT / 'README.md', introduction + '\n'.join(table) + '\n' + ending)
    save(ROOT / 'catalogue.json', json.dumps(records, indent=2, ensure_ascii=False) + '\n')
    manifest = []
    for path in sorted(ROOT.rglob('*')):
        if path.is_file() and path.name != 'PACKAGE_MANIFEST.json' and '__pycache__' not in path.parts:
            raw = path.read_bytes()
            manifest.append({'path': path.relative_to(ROOT).as_posix(), 'size': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()})
    save(ROOT / 'PACKAGE_MANIFEST.json', json.dumps(manifest, indent=2) + '\n')
    print(f'Rebuilt catalogue and {len(records)} Markdown guides. Manifest: {len(manifest)} files.')


if __name__ == '__main__':
    build()
