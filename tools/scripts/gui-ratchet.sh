#!/bin/zsh
# Raise only the GUI workflow floor: ratchet update, then restore the committed coverage floors
# so a GUI-only landing never accidentally raises (or masks a drop in) coverage floors.
set -e
cd "$(git rev-parse --show-toplevel)" || exit 3
mkdir -p artifacts

git show HEAD:ratchet/baseline.json > artifacts/gui-ratchet-baseline-head.json 2>/dev/null \
  || git show main:ratchet/baseline.json > artifacts/gui-ratchet-baseline-head.json

nice -n 19 dotnet run --project tools/Napkin.Tools -- ratchet update 2>&1 | tail -4

python3 - <<'PY'
import json
head = json.load(open('artifacts/gui-ratchet-baseline-head.json'))
p = 'ratchet/baseline.json'
now = json.load(open(p))
now['coverage'] = head['coverage']
text = json.dumps(now, indent=2, ensure_ascii=False) + "\n"
open(p, 'w').write(text)
print('gui floor:', now['gui']['workflowsPassed'])
PY

git diff --stat ratchet/baseline.json
