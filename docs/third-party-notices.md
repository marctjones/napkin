# Third-party notices

napkin's own code is AGPL-3.0 ([`LICENSE`](../LICENSE)). It bundles the following third-party
material. Each keeps its own license, and each license's notice travels with every copy.

## IBM Plex (fonts)

`src/Napkin.App/Assets/Fonts/` bundles IBM Plex Mono (Regular, Medium), IBM Plex Serif (Regular,
Italic) and IBM Plex Sans (Regular, Medium, SemiBold), unmodified, from IBM's official releases
(`github.com/IBM/plex`: plex-mono 2.5.0, plex-serif 2.0.0, plex-sans 1.1.0).

- **License:** SIL Open Font License 1.1. Copyright © 2017 IBM Corp., with Reserved Font Name
  "Plex". The full text is in `LICENSE-IBM-Plex-mono.txt`, `-serif.txt` and `-sans.txt` beside the
  fonts, and the build copies those three files into a `licenses/` folder next to the executable.
- **What it lets us do:** use, embed and bundle the fonts in software, including software that is
  sold; not sell the font files by themselves; keep any derivative under the OFL and off the name
  "Plex". napkin ships them unmodified.
- **Where they come from:** the design system repository, `marctjones/skepticalengineering-design`
  (`fonts/`), which carries the same license files.

## Skeptical Engineering design system

`src/Napkin.App/Theme/` and `Assets/napkin*` are vendored from
`marctjones/skepticalengineering-design` (the commit is in each file's first line). They are the
same author's work and follow napkin's license.
