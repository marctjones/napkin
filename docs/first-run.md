# Opening napkin for the first time

napkin is a **pre-release beta**, and the builds are **not code-signed and not notarized**. The first time you open one, Windows or macOS will warn you that it does not recognise the program. That is expected: it is the price of an open-source project that does not pay for Microsoft and Apple's signing programmes. This page walks you past the warning, one step at a time.

If you have never opened a program that was not from an app store, that is fine. Nothing below needs a command line unless it says so.

> **A note on how much of this has been checked.** Text marked *(unverified — verify on a real machine)* is wording or behaviour that the people who wrote this page could not confirm from Apple's or Microsoft's own documentation, and have not yet seen on a real machine. The screens may look a little different from what is described here. If they do, please tell us (see "If something goes wrong" at the end).

Everything here uses full web addresses so that it also reads correctly from the copy of this page inside each download.

## 1. Pick the right file

Every release is listed at <https://github.com/marctjones/napkin/releases>. Download **one** zip file, plus `SHA256SUMS.txt` if you want to check it.

| Your computer | Download |
|---|---|
| Windows 10 or 11, 64-bit Intel or AMD | `napkin-0.N.0-beta-win-x64.zip` |
| Mac with Apple silicon (M1 or later) | `napkin-0.N.0-beta-osx-arm64.zip` |
| Mac with an Intel processor | `napkin-0.N.0-beta-osx-x64.zip` |

`0.N.0` stands for the release's version number. Not sure which Mac you have? Apple menu → About This Mac; a "Chip" line that starts with "Apple" means Apple silicon, and "Processor" means Intel. *(unverified — verify on a real machine)*

Windows on ARM is not built yet.

Every release on that page is a pre-release. There is no stable release, and none is planned.

## 2. Check that the download is intact (recommended)

`SHA256SUMS.txt` lists a checksum for each zip: a long string of letters and digits worked out from the file's contents. If the one you compute matches the one in the list, the file arrived complete and unaltered by a bad connection.

The checksum file comes from the same release page as the zip, so this catches a damaged download. It cannot, on its own, prove that the release page itself is genuine.

**On a Mac**, open Terminal (Applications → Utilities → Terminal), and move to the folder that holds both files, for example:

```sh
cd ~/Downloads
shasum -a 256 --ignore-missing -c SHA256SUMS.txt
```

You should see the name of your zip followed by `OK`. `--ignore-missing` stops it complaining about the zips you did not download. Anything other than `OK` means the file is damaged: delete it and download it again.

**On Windows**, open PowerShell (Start menu → type "PowerShell"), move to the folder that holds both files, and print the checksum of your zip and the matching line from the list:

```powershell
cd $HOME\Downloads
Get-FileHash .\napkin-0.N.0-beta-win-x64.zip -Algorithm SHA256
Select-String win-x64 .\SHA256SUMS.txt
```

Put your real file name in the first command. The long `Hash` value from the first command must match the long string at the start of the line the second command prints. Letter case does not matter. *(the PowerShell commands are unverified — verify on a real machine)*

## 3. Unzip it

Unzip the file **before** you run anything from it. You get a folder called `napkin-0.N.0-beta-<your system>` containing the program, this page as `FIRST-RUN.md`, the licence as `LICENSE`, and `SOURCE.txt`, which says where the source code is.

- **Mac:** double-click the zip file in Finder.
- **Windows:** right-click the zip file → **Extract All…**.

## 4. Open it on Windows

Open the folder and double-click `Napkin.App.exe`.

Windows may show a blue window headed **"Windows protected your PC"** saying that Microsoft Defender SmartScreen prevented an unrecognised app from starting. Microsoft's own documentation confirms that SmartScreen warns about a downloaded file that is not on its list of well-known, frequently downloaded files, which a new open-source beta is not ([Microsoft Defender SmartScreen overview](https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/)). To go ahead:

1. Click **More info**. *(the wording is unverified — verify on a real machine)*
2. A **Run anyway** button appears. Click it. *(unverified — verify on a real machine)*

napkin opens.

If no warning appears, there is nothing more to do.

## 5. Open it on a Mac

These steps are for **macOS 15 (Sequoia) and later**. On those versions the older shortcut of Control-clicking a program and choosing Open is no longer a way past this warning, so it is not described here. The way through is in System Settings. *(The Control-click point is the project's own understanding, recorded in its design document; Apple's page, read for this one, does not mention it either way. Unverified — verify on a real machine.)*

In this beta, napkin is a single program file called `Napkin.App` rather than an icon-and-window application, so:

1. Open the unzipped folder and double-click `Napkin.App`. A Terminal window may open along with it; that is expected for a program file, and you can leave it alone. *(unverified — verify on a real machine)*
2. macOS will refuse and say it cannot verify the program. Close that message (the button may be labelled **Done**). *(unverified — verify on a real machine)*
3. Open **System Settings** → **Privacy & Security**, and scroll down to the security section. You will find a note that `Napkin.App` was blocked, with an **Open Anyway** button. Click **Open Anyway**.
4. The warning appears again. This time click **Open**. macOS may ask for your Mac's password or Touch ID to confirm. *(the password prompt is unverified — verify on a real machine)*

Steps 3 and 4 are Apple's own instructions for a program macOS has blocked: open System Settings, click Privacy & Security, scroll down and click **Open Anyway**, then click **Open** on the warning that comes back ([Apple: Safely open apps on your Mac](https://support.apple.com/en-us/102445)). Apple's page also says that the program is then saved as an exception, and that you can open it in future by double-clicking it like any other. Everything around those two steps (the exact wording of the first message, the Terminal window, the position of the note in the settings list) is not from Apple's page.

The **Open Anyway** button is expected to appear only after you have tried to open the program once. If you cannot find it, do step 1 again, then look again. *(unverified — verify on a real machine)*

## 6. What "beta" means here

- **Every version is a pre-release.** napkin has no "stable" version and is not working towards a 1.0. Version numbers look like `0.7.0-beta`, and the number goes up whenever the project changes, not on a schedule.
- **Things change and break.** A design file saved by one beta may not open in the next; you will get a clear "unsupported version" message rather than a silent conversion. Keep the drawings and measurements you care about somewhere else too.
- **What each release can do is small on purpose.** The current milestone is a viewer: open a hand-made sample design, pan, zoom, and read dimensions in feet, inches and fractions. What is planned is in the [project plan](https://github.com/marctjones/napkin/blob/main/PLAN.md).
- **It is not a substitute for a permit office, an inspector, or a licensed engineer.**

## 7. Where the source code is

napkin is free software under the [GNU Affero General Public License v3.0](https://github.com/marctjones/napkin/blob/main/LICENSE). The whole source is at <https://github.com/marctjones/napkin>. The release page names the exact version of the source each build was made from, and so does the `SOURCE.txt` file inside every zip. The licence text is the `LICENSE` file, also inside every zip.

## If something goes wrong

Please open an issue at <https://github.com/marctjones/napkin/issues>, and tell us:

- which file you downloaded (the full name of the zip);
- your version of Windows or macOS;
- what you did, what you expected, and what happened, with a screenshot if you can;
- especially, if a warning screen looked different from the one described above.

Issues on GitHub are public. Please do not attach a design file that shows your home's address or other private details.
