# amendments: Connecticut's operations on IRC tables

Two files, one per table Connecticut amends a header footnote of, both from Connecticut's own
document (2022 CSBC w/ Errata #1, p. 145), verbatim and cited:

- `r602.7-1.json`: Table R602.7(1) footnote e.
- `r602.7-3.json`: Table R602.7(3) footnote b.

Each is an `amend-footnote` operation whose footnote is encoded `as-operations`: a
`substitute-input` (a ground snow load below 30 psf is taken as 30 psf when the roof live load is
at most 20 psf) and an `interpolate` (the span, strictly between the 30 and 50 psf columns). The
same text is recorded in `../ct-overlay-data.json`.

No IRC table is loaded yet, so both amendments are **pending**: the pack loads valid and lists
them on `LoadedPack.Pending`. They take effect the moment `layers/irc-2021/tables/` holds the
tables. When you fill that directory, every table you add needs an overlay file here too (an
empty `operations` list if Connecticut does not amend it); see `docs/rules-engine.md`.
