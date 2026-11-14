# Overlay Charts catalogue

The main catalogue is [docs/README.md](docs/README.md). It renders directly on
GitHub with chart previews, source-code links, complete downloads, and individual
Markdown guides. It needs no separately hosted website or GitHub Pages setup.
The former standalone site has been retired.

The original HTML catalogue is also preserved in `docs/index.html`. Download the
repository and open that file to browse the HTML catalogue offline. Its relative
image, guide, and download links are preserved, and source links point to CIS-tools.

After editing the HTML program guides, run:

```powershell
python tools/rebuild_catalogue.py
```

This rebuilds the main README catalogue, `docs/README.md`, individual Markdown
guides, catalogue metadata, and the SHA-256 package manifest. It does not change
program source, executables, CSV inputs, ZIP packages, or plot images.

CSV data and example charts are snapshots. Regenerate outputs, thumbnails, and
download packages when programs or inputs change. Complete ZIPs preserve each
program's runtime and build layout. If a hosted HTML site is wanted later, deploy
the contents of `Overlay Charts/docs` as the site's document root.
