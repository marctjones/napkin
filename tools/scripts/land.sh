#!/bin/zsh
# land.sh <branch> <what, for the bump and merge messages>
#
# Bumps the minor version on <branch>, merges it into main with a real merge commit, pushes, and
# deletes the branch. Refuses if the working tree is dirty, or if origin/main moved since the
# branch was cut (merge origin/main into the branch and re-gate first).
#
# The co-author trailer comes from $NAPKIN_COAUTHOR, e.g.
#   export NAPKIN_COAUTHOR='Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>'
# and defaults to a generic line if unset.
set -e
cd "$(git rev-parse --show-toplevel)"

branch=$1
what=$2
coauthor=${NAPKIN_COAUTHOR:-"Co-Authored-By: Claude <noreply@anthropic.com>"}

if [ -z "$branch" ] || [ -z "$what" ]; then
  echo "usage: land.sh <branch> <what, for the bump and merge messages>" >&2
  exit 2
fi

if [ -n "$(git status --porcelain)" ]; then
  echo "working tree dirty: commit or stash before landing." >&2
  exit 1
fi

git fetch -q origin
if ! git merge-base --is-ancestor origin/main "$branch"; then
  echo "origin/main moved: merge it into $branch and rerun the gate first." >&2
  exit 1
fi

current=$(sed -n 's/.*<VersionPrefix>0\.\([0-9]*\)\.0<\/VersionPrefix>.*/\1/p' Directory.Build.props)
next=$((current + 1))

git checkout -q "$branch"
sed -i '' "s/<VersionPrefix>0\.${current}\.0<\/VersionPrefix>/<VersionPrefix>0.${next}.0<\/VersionPrefix>/" Directory.Build.props
git add Directory.Build.props
git commit -q -m "Bump to 0.${next}.0-beta for ${what}

${coauthor}"

git checkout -q main
git merge -q --ff-only origin/main
git merge -q --no-ff "$branch" -m "Merge ${branch}: ${what} (0.${next}.0-beta)

${coauthor}"
git push -q origin main
# -D not -d: git branch -d compares against the branch's upstream-tracking ref (still set from
# the earlier `git push -u`), which we have not re-fetched, so it reports "not merged" even
# though the --no-ff merge above just incorporated it into main. We just did that merge ourselves.
git branch -q -D "$branch"
git push -q origin --delete "$branch" 2>/dev/null || true
git log --oneline -4
