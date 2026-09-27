#!/bin/zsh
# Pin napkin to a new Excise.Core (#25): pack it from a commit that is on GitHub and vendor it.
#
#   tools/scripts/excise-pack.sh            # the head of excise's develop branch on GitHub
#   tools/scripts/excise-pack.sh <commit>   # a specific pushed commit (full or abbreviated sha)
#
# Excise.Core is deliberately not published to nuget.org (excise's Packaging.props, excise
# #383/#384), so napkin vendors one nupkg in vendor/excise, and nuget.config maps Excise.* to that
# folder and nowhere else.
#
# The commit is fetched from GitHub into a throwaway clone, never taken from (or built inside) a
# local excise checkout: a pin can then never name a commit that exists on one machine only, and
# nothing is written into a working tree other sessions may be using. The package version carries
# the commit (3.1.0-napkin.g<sha7>; the "g" keeps the label valid SemVer when a sha is all digits
# with a leading zero): NuGet caches packages by version, so re-packing under an unchanged version
# would silently keep restoring the old build. The nuspec's <repository commit="…"> names the full
# sha, so the vendored file says where it came from. The old nupkg is removed and the one
# PackageReference (src/Napkin.Interop.Pdf) is moved to the new version.
#
# Afterwards, by hand: build, run `dotnet run --project tools/Napkin.Tools -- licenses check`
# (Excise.Core's dependencies come from nuget.org and must pass the policy), re-read the notices
# in docs/third-party-notices.md against the new commit's Excise.Core.csproj, and commit.
set -euo pipefail
setopt null_glob
cd "$(git rev-parse --show-toplevel)"

repo=https://github.com/marctjones/excise.git
commit=${1:-$(git ls-remote "$repo" refs/heads/develop | cut -f1)}
[[ -n $commit ]] || { echo "excise-pack: could not resolve excise develop on GitHub" >&2; exit 2; }

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
git -C "$work" init -q
git -C "$work" remote add origin "$repo"
if ! git -C "$work" fetch -q --depth 1 origin "$commit" 2>/dev/null; then
  # An abbreviated sha cannot be fetched by name; fetch develop's history and resolve it there.
  git -C "$work" fetch -q origin develop
fi
git -C "$work" checkout -q "$(git -C "$work" rev-parse --verify "$commit^{commit}" 2>/dev/null || echo FETCH_HEAD)"
sha=$(git -C "$work" rev-parse HEAD)
short=${sha[1,7]}

base=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$work/Excise.Core/Excise.Core.csproj" | head -1)
version="$base-napkin.g$short"

nice -n 19 dotnet pack "$work/Excise.Core/Excise.Core.csproj" -c Release \
  -p:Version="$version" -p:RepositoryCommit="$sha" -p:ContinuousIntegrationBuild=true \
  -o "$work/out" | tail -2

mkdir -p vendor/excise
rm -f vendor/excise/Excise.Core.*.nupkg
cp "$work/out/Excise.Core.$version.nupkg" vendor/excise/
sed -i '' -E "s|(<PackageReference Include=\"Excise.Core\" Version=\")[^\"]*|\1$version|" \
  src/Napkin.Interop.Pdf/Napkin.Interop.Pdf.csproj

echo "Excise.Core pinned to $version (marctjones/excise@$sha)"
