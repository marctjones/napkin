**napkin {{VERSION}} is a pre-release beta.** napkin is a beta indefinitely: nothing here is a stable release, breaking changes are always allowed, and there is no upgrade path between betas. It is not a substitute for a permit office, an inspector, or a licensed engineer.

## Download

| Your computer | File |
|---|---|
| Windows (64-bit Intel/AMD) | `napkin-{{VERSION}}-win-x64.zip` |
| Mac with Apple silicon (M1 or later) | `napkin-{{VERSION}}-osx-arm64.zip` |
| Mac with an Intel processor | `napkin-{{VERSION}}-osx-x64.zip` |

`SHA256SUMS.txt` holds the checksum of each file so you can confirm a download is intact.

## First run: read this before you open it

These builds are **unsigned and not notarized**, so Windows and macOS will warn you the first time. That is expected, and there is a way past the warning. Step-by-step instructions for both systems, and for checking your download, are in [`docs/first-run.md`]({{SERVER}}/{{REPO}}/blob/{{TAG}}/docs/first-run.md). The same file is inside each zip as `FIRST-RUN.md`.

## Source code (GNU Affero General Public License v3.0)

napkin is free software under the AGPL-3.0. The complete Corresponding Source for the builds attached to this release is available to anyone, at no charge, from the place these builds are offered:

- Commit `{{SHA}}`, which the tag `{{TAG}}` points at: [{{SERVER}}/{{REPO}}/tree/{{TAG}}]({{SERVER}}/{{REPO}}/tree/{{TAG}})
- As a zip: [{{SERVER}}/{{REPO}}/archive/refs/tags/{{TAG}}.zip]({{SERVER}}/{{REPO}}/archive/refs/tags/{{TAG}}.zip)
- The licence text is the file [`LICENSE`]({{SERVER}}/{{REPO}}/blob/{{TAG}}/LICENSE) in that source, and inside each zip.

Each zip also carries a `SOURCE.txt` naming the commit it was built from.

## Problems

Please report them at [{{SERVER}}/{{REPO}}/issues]({{SERVER}}/{{REPO}}/issues), saying which file you downloaded and which version of Windows or macOS you are on.
