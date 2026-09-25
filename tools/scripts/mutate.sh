#!/bin/zsh
# mutate.sh <file> <old-text> <new-text> <command...>
#
# Applies a one-shot text substitution (old -> new) to <file>, runs <command> — expected to FAIL
# with the mutation applied, proving a test/guard catches the bug — then always restores <file>
# via `git checkout` and reports whether the mutation was caught.
#
# Example: prove a coverage-ratchet-style assertion actually guards something, by breaking it and
# checking the guarding command (a test filter, `ratchet check`, etc.) now fails.
set -o pipefail
cd "$(git rev-parse --show-toplevel)" || exit 3

f="$1"; old="$2"; new="$3"
shift 3 2>/dev/null
cmd=("$@")

if [ -z "$f" ] || [ -z "$old" ] || [ -z "$new" ] || [ ${#cmd[@]} -eq 0 ]; then
  echo "usage: mutate.sh <file> <old-text> <new-text> <command...>" >&2
  exit 2
fi

mkdir -p artifacts

if ! git diff --quiet -- "$f"; then
  echo "refusing: $f has uncommitted changes — commit them first, or the final 'git checkout' would discard them, not just the mutation." >&2
  exit 5
fi

python3 - "$f" "$old" "$new" <<'PY'
import sys
p, o, n = sys.argv[1:4]
s = open(p, newline='').read()
assert o in s, f"mutation target not found in {p}"
open(p, 'w', newline='').write(s.replace(o, n, 1))
PY
apply_status=$?

if [ $apply_status -ne 0 ]; then
  echo "MUTATION NOT APPLIED: target text not found in $f"
  git checkout -q -- "$f"
  exit 4
fi

"${cmd[@]}" > artifacts/mutate-output.log 2>&1
cmd_status=$?
git checkout -q -- "$f"

if [ $cmd_status -eq 0 ]; then
  echo "MUTATION SURVIVED (bad): '${cmd[*]}' exited 0 with the mutation applied to $f — nothing caught it. See artifacts/mutate-output.log."
  exit 1
fi

echo "MUTATION CAUGHT (good): '${cmd[*]}' exited $cmd_status with the mutation applied to $f."
exit 0
