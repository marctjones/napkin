# irc-2021: the model-code base layer (no tables loaded)

No tables loaded: fill from your own copy of the 2021 IRC (Tables R602.7(1), (2), (3), R602.3,
R602.10.3 ...) following docs/rules-engine.md.

Napkin **may** ship transcribed code-table values (decided, #157) once they are read from a
primary source and cited (CLAUDE.md, "Data and citations"); that is what #14 and #158 do when
someone obtains a readable copy of the 2021 IRC and transcribes it, reviewed row by row against
the source. Until then, sizing a header returns `NoData` ("base tables not loaded"), never a
guess. If you add a table here, every table needs a matching overlay file in
`../../packs/us-ct-2022/amendments/` (an empty `operations` list if Connecticut does not amend
it; see docs/rules-engine.md).

Separately, a project's own owner may type in one row's answer from their own copy of the code —
a fact about their drawing, not a table napkin ships — see
[`docs/design/manual-code-values.md`](../../../docs/design/manual-code-values.md) (#245).
