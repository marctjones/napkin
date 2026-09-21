# Cutting a release

For maintainers. What a tag does, how to rehearse one without publishing anything, and what the
pipeline deliberately does not do. The pipeline is [`.github/workflows/release.yml`](../.github/workflows/release.yml)
(issue #38); the instructions the downloader follows are [`first-run.md`](./first-run.md) (issue #27).

Policy this rests on, in [`DESIGN.md` §6.6, §11 and §12](../DESIGN.md#12-versioning-and-releases-beta-policy):
napkin is a pre-1.0 beta indefinitely, every release is a **pre-release**, nothing is ever marked
latest, and nothing is signed or notarized.

## The rule for when

A milestone is tagged when it is worth a public pre-release, which is a decision for the project
owner, not for the pipeline or for a merge. Nothing is tagged on a schedule (`DESIGN.md` §12). A
merged pull request does **not** publish anything.

## Cutting one

1. **The version is already right.** The pull request that landed the work bumped
   `VersionPrefix` in [`Directory.Build.props`](../Directory.Build.props) (`0.N.0`, suffix `beta`),
   as every merged pull request does. That file is the only place the version lives; the pipeline
   reads it and never takes a version from anywhere else.
2. **Tag the commit on `main` that carries that version, with exactly `v` in front of it.** For
   `0.4.0-beta` the tag is `v0.4.0-beta`.

   ```sh
   git switch main && git pull
   git tag -a v0.4.0-beta -m "napkin 0.4.0-beta"
   git push origin v0.4.0-beta
   ```
3. **Watch the Release workflow.** If the tag and `Directory.Build.props` disagree, it stops in its
   first job with an error naming both, before anything is built. Nothing was published, so
   delete the tag (`git push --delete origin <tag>` and `git tag -d <tag>`) and tag the right commit.
4. **Read the release page** when it finishes, and follow [`first-run.md`](./first-run.md) on a
   real Windows machine and a real Mac from the artifacts, as a person who has never seen napkin
   would. Issue #38 is not done until that has happened; the checks in the pipeline cannot
   replace it (see "What the pipeline does not prove").

Never move or delete a tag once its release is public. The release notes tell people the source for
these builds is at that tag, and the AGPL-3.0 offer of source depends on it staying true for as
long as the builds are offered.

## What the workflow does

Triggered by a pushed tag matching `v*`:

| Job | What it does |
|---|---|
| **Version** | Reads `VersionPrefix` and `VersionSuffix` from `Directory.Build.props`. Fails unless they are `0.N.0` and `beta`, and, on a tag, unless the tag is exactly `v` + that version. |
| **Build** (one per runtime) | `dotnet publish src/Napkin.App` as a self-contained single file for `win-x64` (Windows runner), `osx-arm64` and `osx-x64` (macOS runner), with the commit SHA in the informational version. Stages the executable with `LICENSE`, `SOURCE.txt` (the written offer of source, naming the commit) and `FIRST-RUN.md`. Zips it. On macOS, unzips the zip again and checks the signature of what came out (below). |
| **Checksums and notes** | Collects the three zips, writes `SHA256SUMS.txt`, and fills [`.github/release-notes-template.md`](../.github/release-notes-template.md) into `RELEASE_NOTES.md`. |
| **Publish pre-release** | Only for a pushed tag, and the only job with `contents: write`. `gh release create` with `--verify-tag --prerelease --latest=false`, the three zips and `SHA256SUMS.txt`. |

Every other trigger stops after **Checksums and notes** and uploads what it made as workflow
artifacts, so a rehearsal produces exactly the bytes a release would attach.

### The artifacts

```
napkin-0.N.0-beta-win-x64.zip
napkin-0.N.0-beta-osx-arm64.zip
napkin-0.N.0-beta-osx-x64.zip
SHA256SUMS.txt                     "<hash>  <name>", one line per zip
```

and inside each zip, a single folder named like the zip:

```
napkin-0.N.0-beta-<rid>/
  Napkin.App[.exe]                 the self-contained single-file program
  LICENSE                          AGPL-3.0
  SOURCE.txt                       written offer of source, naming the commit
  FIRST-RUN.md                     a copy of docs/first-run.md
```

The build logs print the exact layout of each folder. Anything the publish leaves beside the
executable, other than `*.pdb` symbols, is shipped too; the listing is where to notice that. In the
first dry run (2026-09-21) the publish left nothing else: the executable is one file of about
100 MB, because `IncludeNativeLibrariesForSelfExtract` folds the native libraries (Skia, HarfBuzz,
Avalonia's own) into it.

### The signature check

DESIGN.md §6.6: Apple-silicon binaries carry an automatic **ad-hoc** signature from the build
toolchain. arm64 code does not run without one, it costs nothing, and it is **not** Developer ID
signing. The pipeline therefore does not strip it, and the macOS build job asserts, on the
executable as it comes out of the finished zip (`codesign -dvv`), that

- the arm64 binary is ad-hoc signed (`Signature=adhoc`) and the signature verifies;
- no macOS binary has an `Authority=` line (a certificate chain) or a notarization ticket.

Observed in the first dry run: both `osx-arm64` and `osx-x64` come out of the SDK's single-file
publish with `Signature=adhoc`, `flags=0x2(adhoc)`, `TeamIdentifier=not set`, and `codesign --verify
--strict` reports the arm64 file valid. Nothing in the pipeline signs; the SDK does. If a future SDK
stops signing the bundle, the arm64 check fails, and the fix is an ad-hoc `codesign --sign -` step
(which the workflow test permits) rather than any other identity.

## Rehearsing without publishing

Both of these run the whole pipeline up to and including the checksums and notes, and create no tag,
no release and nothing public:

- **Actions → Release → Run workflow** (`workflow_dispatch`), on any branch. Or
  `gh workflow run release.yml --ref <branch>`.
- **Any pull request that changes `.github/workflows/release.yml`** runs it automatically, and
  only such a pull request does. (A change to the notes template or to `first-run.md` does not, on
  purpose: run it by hand.)

Then download the workflow artifacts from the run page: `zip-<rid>` for each build and
`release-payload` for the zips, `SHA256SUMS.txt` and the rendered `RELEASE_NOTES.md`. Read the
notes as a downloader would, and open the zips on the real machines.

The dry runs cannot create a release even by accident: the publish job's condition is a pushed
tag, and `permissions` are `contents: read` everywhere else.

To rehearse the *real* path, on the real repository, push a throwaway tag of the form `v0.N.0-beta`
that matches `Directory.Build.props` on a branch you are willing to publish from, then delete the
release and the tag afterwards. Issue #38's acceptance calls for exactly this once. It does
publish a (pre-)release for a moment, and it makes the source offer briefly point at that tag, so
it is a decision for the project owner.

## The checks over the workflow

`tests/Napkin.Tools.Tests/ReleaseWorkflowTests.cs` reads `release.yml` as text and fails if:

- `--prerelease` or `--latest=false` is missing from `gh release create`;
- a signing or notarization step appears: `codesign --sign` with any identity but the ad-hoc `-`,
  `notarytool`, `signtool`, `security import`, and similar;
- anything removes a signature (`codesign --remove-signature`, `install_name_tool`);
- `contents: write` appears anywhere but the one job that publishes, or that job can run on
  anything but a tag;
- an action from anywhere but GitHub's own `actions/` organisation is used.

Comment lines are ignored, so a comment can say what the pipeline does not do. The tests also run
each rule against a copy of the workflow with the offending line put back, so the check itself is
tested and cannot quietly stop checking.

## What the pipeline does not prove

- **That the app starts.** The pipeline checks that the artifacts are built, the right architecture,
  correctly signed or unsigned, checksummed and complete. It does not launch a build. The published
  builds have to be launched by a person on a real Windows machine and a real Mac (Apple silicon
  and, ideally, Intel) that has never run napkin, and opened against a sample design. That
  is issue #27's acceptance (PKG-001) and part of #38's.
- **What a first launch does with the bundled native libraries.** Per the
  [.NET docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview), a
  single-file app that embeds native libraries extracts them at start-up, to a directory under
  `$HOME/.net` on macOS and `%TEMP%/.net` on Windows. Whether that extraction, or the extracted
  libraries' signatures, changes what Gatekeeper or antivirus software says has not been seen on a
  real machine.
- **What Gatekeeper and SmartScreen show.** `first-run.md` marks every piece of wording that was
  not taken from Apple's or Microsoft's own documentation as unverified. When someone does the
  above, they should correct the page to match what they saw, and remove the markers.

## What is deliberately not done

- **No code signing and no notarization, on either platform.** DESIGN.md §6.6 and §11 decided this.
  There is no Developer ID certificate, no Apple account, no Authenticode certificate in this
  pipeline, and the workflow test fails if one is added. The friction this buys is what
  `first-run.md` documents. The macOS arm64 ad-hoc signature is the toolchain's, and is the only
  one.
- **No installers.** The artifacts are a program file in a zip, not an `.app` bundle, an MSI or a
  disk image. **Issue #27's installer half stays open**: proper self-contained installers for
  Windows and macOS (Apple silicon and Intel) that start on a machine with no .NET runtime
  installed. A macOS `.app` bundle is the likely next step. It would also give macOS a normal
  double-click experience; a bare executable may open a Terminal window.
- **No Windows on ARM.** Avalonia supports `win-arm64`; nothing here builds it.
- **No universal macOS binary.** Apple silicon and Intel get one zip each.
- **No trimming and no ReadyToRun.** Avalonia relies on reflection; size and start-up time are not
  the problem being solved.
- **No "latest".** Not now and not on the first tag: `--latest=false` is in the workflow and in the
  test.

## Why these runtime identifiers

`win-x64`, `osx-arm64` and `osx-x64` are the ones Avalonia lists as supported on Windows and macOS
([supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)): Windows 10 22H2
and 11 on x64, macOS 14 and later on Apple silicon and Intel. A .NET single-file app is specific to
one operating system and architecture, so each is its own publish
([.NET single-file docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)).
`osx-x64` is built on the Apple-silicon macOS runner, which is a plain cross-architecture publish
with no native compilation. The build job checks the architecture of what it built.
