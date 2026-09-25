# irc-2021: the model-code base layer (no tables loaded)

No tables loaded: fill from your own copy of the 2021 IRC (Tables R602.7(1), (2), (3), R602.3,
R602.10.3 ...) following docs/rules-engine.md.

napkin does not ship transcribed code-table values (that decision is not made; see the decision
issue on GitHub). Until a person adds `tables/<table>.json` files here, sizing a header returns
`NoData` ("base tables not loaded"), never a guess. If you add a table here, every table needs a
matching overlay file in `../../packs/us-ct-2022/amendments/` (an empty `operations` list if
Connecticut does not amend it; see docs/rules-engine.md).
