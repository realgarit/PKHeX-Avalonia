# Packaging & distribution

**Installing the app?** Start with the [platform setup instructions](../README.md#download).
For sandbox access and bundle updates, go directly to [Linux Flatpak](#linux-flatpak).
The sections below are primarily for release maintainers.

This document covers how `release.yml` builds installers for each platform,
which secrets unlock real code signing / notarization, and how to submit the
package-manager templates under `packaging/` once signed builds exist.

Everything described here runs automatically on every release. Since 2026-08-28 a release is any
push to `main` that touches something shippable — CI resolves the next version from the highest
existing `v*` git tag, writes `<UIVersion>`, commits, tags, builds and publishes in a single run
(see [`development.md`](development.md#uiversion-semver-convention--ci-owns-the-bump)). A manual
`workflow_dispatch` with `dry_run=false` does the same for main's current tip; the default
`dry_run=true` only prints what *would* be released. There are no manual signing steps — the
workflow signs when it can and clearly labels artifacts as unsigned when it can't.

## Artifact matrix

| Platform | Artifact(s) | Notes |
|---|---|---|
| Windows x64 | `PKHeX-Avalonia-win-x64.zip` (unchanged) + `PKHeX-Avalonia-Setup.exe` (or `PKHeX-Avalonia-Setup-unsigned.exe`) | Installer built with Inno Setup via chocolatey |
| Linux x64 | `PKHeX-Avalonia-linux-x64.zip` + `PKHeX-Avalonia-<version>-x86_64.AppImage` + `.AppImage.zsync` + `PKHeX-Avalonia-linux-x86_64.flatpak` | Flatpak uses the Freedesktop 26.08 runtime; AppImageUpdate and AppStream metadata are included |
| macOS arm64 / x64 | `PKHeX-Avalonia-osx-{arm64,x64}.zip` (unchanged, ad-hoc signed as before) + `PKHeX-Avalonia-osx-{arm64,x64}.dmg` or `-unsigned.dmg` | `.dmg` contains the `.app` bundle plus an `Applications` symlink |

The ZIP naming stays stable. AppImages use the catalog's application/version/architecture
convention; older releases retain their original `PKHeX-Avalonia-linux-x64.AppImage` name.

## macOS: signing & notarization

`release.yml`'s `build` job (macOS legs of the matrix) gates real Developer
ID signing on three secrets:

| Secret | Contents |
|---|---|
| `MACOS_CERT_P12` | Base64-encoded `.p12` export of a **Developer ID Application** certificate (`base64 -i cert.p12 \| pbcopy`) |
| `MACOS_CERT_PASSWORD` | Password used when exporting the `.p12` |
| `MACOS_SIGN_IDENTITY` | The identity string codesign should use, e.g. `Developer ID Application: Your Name (TEAMID)` |
| `APPLE_NOTARY_KEY_ID` | Key ID of an App Store Connect API key with the Developer role |
| `APPLE_NOTARY_KEY` | Base64-encoded `.p8` private key for that API key |
| `APPLE_NOTARY_ISSUER_ID` | Issuer ID (UUID) for the API key, from App Store Connect > Users and Access > Keys |

If `MACOS_CERT_P12`, `MACOS_SIGN_IDENTITY`, and `APPLE_NOTARY_KEY` are all
present, the workflow:

1. Imports the certificate into a temporary keychain.
2. Re-signs the `.app` with `codesign --options runtime` (hardened runtime,
   required for notarization) using the Developer ID identity.
3. Submits it to `notarytool`, waits for the result, and staples the ticket
   with `stapler staple`.
4. Packs the notarized `.app` into `PKHeX-Avalonia-osx-<arch>.dmg`.

If any of those secrets are absent, the workflow skips straight to step 4
and names the output `PKHeX-Avalonia-osx-<arch>-unsigned.dmg` so it's obvious
from the filename (and should be called out in release notes) that
Gatekeeper will still complain.

The existing ad-hoc-signed `.zip` artifacts are untouched by this change.

## macOS: stable self-signed identity (the "tertius" pattern)

Real Developer ID signing needs a paid Apple Developer account. Without one,
`release.yml` falls back to a second tier before giving up and shipping
unsigned: a **stable self-signed identity**.

**Why a stable identity matters.** Gatekeeper's "this app is from an
unidentified developer" prompt, and macOS's re-prompting for TCC permissions
(Accessibility, Full Disk Access, etc.) and keychain access, are keyed off
the app's *codesign designated requirement* — effectively a hash derived
from the signing certificate. An ad-hoc signature (`codesign --sign -`) has
no stable identity: every build gets a new one, so macOS treats every
update as a brand-new, never-before-seen app. That means every single
release re-triggers Gatekeeper's "are you sure?" dialog and drops any TCC
grants the user already approved.

If instead every release is signed with the **same** self-signed
certificate, the designated requirement stays identical release over
release. macOS then recognizes an update as *the same app* upgrading in
place — TCC grants and keychain items persist across `brew upgrade` or a
manual re-download and reinstall, and Gatekeeper only has an opinion about
the app once, not on every update.

This does **not** replace notarization — a self-signed cert isn't trusted by
Apple, so first launch of a freshly downloaded artifact still shows the
Gatekeeper "unidentified developer" prompt once. What it fixes is the
*repeat* prompting on every subsequent update, and it works entirely without
an Apple Developer Program membership.

**Generating the certificate (one-time, by the repo owner):**

```bash
P12_PASSWORD='choose-a-password' Scripts/make-signing-cert.sh ./secrets
base64 -i ./secrets/signing.p12 | pbcopy   # paste into SELFSIGN_CERT_P12_BASE64
```

This produces a 10-year self-signed EC certificate with the `codeSigning`
extended key usage, packaged as `signing.p12`. Keep this file (and the
password) somewhere durable outside the repo — regenerating it produces a
*different* identity, which resets the stability this whole pattern exists
for.

**The three secrets to add** (repo Settings → Secrets and variables →
Actions), used only when the Developer ID secrets above are absent:

| Secret | Contents |
|---|---|
| `SELFSIGN_CERT_P12_BASE64` | Base64 of the `.p12` produced above |
| `SELFSIGN_CERT_PASSWORD` | The `P12_PASSWORD` used to generate it |
| `SELFSIGN_IDENTITY` | The certificate's CN, default `Patrik Lleshaj` (override via `CERT_CN` when running the script) |

When present, `release.yml` imports the cert into a dedicated CI keychain
(`Scripts/import-cert.sh`, same idempotent import-and-unlock-partition-list
flow used for local testing) and re-signs every Mach-O in
`PKHeX.Avalonia.app` with that identity before building the `.dmg`. The
artifact is named `PKHeX-Avalonia-osx-<arch>-selfsigned.dmg` so it's
distinguishable from a Developer ID build and from a fully unsigned one.

**Homebrew users get the whole problem solved for them.** The cask template
(`packaging/homebrew/pkhex-avalonia.rb`) runs a `postflight` block that
strips the quarantine extended attribute Homebrew's downloader adds
(`xattr -dr com.apple.quarantine`), so `brew install --cask pkhex-avalonia`
never shows a Gatekeeper prompt at all, on first install or any later
upgrade.

**Manual `.dmg`/`.zip` downloads** still see the one-time "unidentified
developer" prompt on first launch (self-signed, not notarized). Either
right-click → **Open** once, or clear the quarantine bit yourself:

```bash
xattr -dr com.apple.quarantine /Applications/PKHeX-Avalonia.app
```

After that one time, updating in place (replacing the same `/Applications`
copy) keeps the same designated requirement release to release, so this
manual step should not be needed again as long as the self-signed cert
itself isn't regenerated.

## Windows: installer & code signing

`release.yml`'s Windows leg installs Inno Setup via `choco install
innosetup`, then builds `packaging/windows/installer.iss` into
`PKHeX-Avalonia-Setup.exe` (registers Start Menu / desktop shortcuts and an
Add/Remove Programs entry that uninstalls cleanly).

Code signing (via `signtool.exe`, part of the Windows SDK already present on
`windows-latest`) is gated on:

| Secret | Contents |
|---|---|
| `WINDOWS_CERT_P12` | Base64-encoded `.pfx`/`.p12` of an OV (or EV) code-signing certificate |
| `WINDOWS_CERT_PASSWORD` | Password for that `.pfx` |

If both secrets are present, the workflow signs both the published
`PKHeX.Avalonia.exe` (before packaging) and the final
`PKHeX-Avalonia-Setup.exe` with a SHA-256 signature and a DigiCert RFC 3161
timestamp. If they're absent, the installer is renamed to
`PKHeX-Avalonia-Setup-unsigned.exe` so SmartScreen's "unknown publisher"
warning is expected and self-explanatory from the filename.

**Note:** an OV certificate alone does not eliminate SmartScreen warnings
immediately — Microsoft's reputation system needs download volume to build
trust for a given cert. An EV certificate (or Azure Trusted Signing) avoids
the warning from day one. Either kind of certificate works with the signing
steps above; only the secret contents change.

## Linux distribution

GitHub releases provide portable ZIPs, AppImages built with `appimagetool`, and
Flatpak bundles built from source inside the SDK sandbox. Choose the package
that fits your desktop; [Flatpak installation and maintenance](#linux-flatpak)
are documented below. A Flathub listing requires a separate submission and
reviewer acceptance under its current requirements.

### AppImage catalog metadata and updates

`.github/scripts/prepare-appdir.sh` stages the published application, desktop entry,
icon and AppStream metadata from `packaging/linux/`. Both metadata formats are
validated before packaging. The desktop and AppStream display name is **PKHeX-Avalonia**.

The embedded update string is
`gh-releases-zsync|realgarit|PKHeX-Avalonia|latest|PKHeX-Avalonia-*x86_64.AppImage.zsync`.
`appimagetool` creates the delta-update file, and CI publishes it beside the AppImage.
The release fails if that file or the embedded update information is missing.
This enables external AppImageUpdate tools; the application does not automatically
download updates because this metadata exists.

The AppStream default screenshot uses the README dark-theme capture with the
checked-in legal Pokémon Legends: Z-A save; the light capture is also supplied.
See [screenshot provenance](screenshots/README.md). Image URLs are pinned to the
release's source commit, so future README changes cannot alter an older package's
screenshots. No save file is added to the distributed application.
The catalog [supports supplied AppStream screenshots](https://github.com/AppImage/appimage.github.io#checklist-for-submitting-your-own-appimage);
its entry needs to be re-tested against the new release to refresh cached metadata.

To refresh an existing listing, comment `/retest` on its catalog PR. If the PR
is already merged, the catalog bot opens a new refresh PR. After it passes and
merges, a second test run writes the generated entry in `apps/` and metadata in
`database/`. The PR report still includes a screenshot captured at application
startup; the catalog page instead uses the supplied AppStream default image.

Updating the generated entry does **not** publish the website. As verified on
2026-09-30, the catalog uses GitHub Actions for Pages, and its
[`Build website` workflow](https://github.com/AppImage/appimage.github.io/blob/master/.github/workflows/build-website.yml)
is manual-only. A catalog maintainer must run it before the public page changes.
Check the generated entry's `screenshots` URL and then the live page's image;
a passed re-test or merged refresh PR alone does not prove the screenshot is live.

The .NET runtime is bundled, but Linux system libraries are still required. The
catalog's initial report identified a glibc 2.27 reference; this is not a guarantee
that every distribution with that glibc version supports the bundled .NET runtime.
Use a [supported .NET 10 Linux distribution](https://learn.microsoft.com/dotnet/core/install/linux)
with the runtime's native dependencies. On systems without FUSE, launch with
`--appimage-extract-and-run`.

## Package managers (Homebrew cask, winget)

Publishing to Homebrew/winget means opening a PR against **their**
repositories (`homebrew/homebrew-cask`, `microsoft/winget-pkgs`) — this repo
cannot and does not auto-publish there. `packaging/` contains ready-to-fill
templates plus the exact submission steps:

### Homebrew cask — `packaging/homebrew/pkhex-avalonia.rb`

Prerequisite: a release with signed & notarized `.dmg` files (see above) —
Homebrew cask maintainers reject casks whose binaries fail Gatekeeper.

1. `shasum -a 256 PKHeX-Avalonia-osx-arm64.dmg PKHeX-Avalonia-osx-x64.dmg`
   and fill in `version` + both `sha256` values in the template.
2. `brew bump-cask-pr --cask pkhex-avalonia --version <version>` (once the
   cask already exists upstream), or for the first submission, fork
   `homebrew/homebrew-cask`, copy the file to
   `Casks/p/pkhex-avalonia.rb`, and open a PR.
3. `brew audit --cask --online pkhex-avalonia` locally before submitting.

### winget — `packaging/winget/realgarit.PKHeXAvalonia.*.yaml`

Prerequisite: a release with a signed `PKHeX-Avalonia-Setup.exe` — winget
also flags unsigned installers during validation and Microsoft's manual
review is far more likely to reject them.

1. Replace `{{VERSION}}` in all three files and `{{INSTALLER_SHA256}}` in
   the installer manifest (`Get-FileHash PKHeX-Avalonia-Setup.exe -Algorithm
   SHA256` on Windows, or `sha256sum` elsewhere).
2. Easiest path: `wingetcreate update realgarit.PKHeXAvalonia -u
   https://github.com/realgarit/PKHeX-Avalonia/releases/download/v<version>/PKHeX-Avalonia-Setup.exe
   -v <version> -s` (the `-s` submits a PR directly if you're
   authenticated with `gh`).
3. Manual path: `winget validate --manifest packaging/winget/` then copy the
   three files into a fork of `microsoft/winget-pkgs` under
   `manifests/r/realgarit/PKHeXAvalonia/<version>/` and open a PR.

## Summary: what's automatic vs. gated vs. manual

- **Fully automatic, every release:** zip artifacts (all platforms),
  AppImage, Flatpak bundle, `.dmg` (signed, self-signed, or unsigned), Windows installer
  (signed or unsigned), GitHub Release creation and asset upload.
- **Gated on secrets (automatic once configured):** Developer ID codesigning
  + notarization/stapling for macOS (tier 1), stable self-signed identity
  for macOS (tier 2, the "tertius" pattern — no Apple Developer account
  needed), code signing for the Windows installer and exe.
- **Manual, one-time-per-version, by design (cannot be automated without
  publishing into third-party repos on the maintainer's behalf):**
  submitting the Homebrew cask and winget manifest PRs. Flathub has a separate
  submission and review process; see below.

## Linux Flatpak

GitHub releases include an x86_64 Flatpak bundle built from the exact tagged source,
using the Freedesktop 26.08 runtime and .NET 10 SDK extension. The bundle does not
require a system-wide .NET installation. Install it on a Linux desktop with Flatpak
and an XDG desktop portal backend:

```bash
flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak install --user ./PKHeX-Avalonia-linux-x86_64.flatpak
flatpak run io.github.realgarit.PKHeX-Avalonia
```

The Flathub remote supplies the runtime dependencies; PKHeX-Avalonia itself is not
listed on Flathub. A standalone GitHub bundle has no application update remote:
install the newer release bundle with the same `flatpak install --user` command
to update it. The app keeps release notes available, explains that Flatpak owns
updates, and hides the portable download/install actions. It never replaces `/app`
or starts an update helper inside the sandbox.

File and folder pickers use Avalonia's XDG portal backend. Under Wayland, Avalonia uses
the portal only when the compositor supports the xdg-foreign protocol and otherwise shows
its built-in picker, which inside the sandbox sees only the app's own data. No home, host, or
removable-drive filesystem permission is granted: select individual saves or a
folder through the picker. Persistent portal grants allow reopening selected files;
dragging a host file into the app may require opening it through the picker first.
Use File > Save As to choose a new destination. Settings, backups and other private
data live under `~/.var/app/io.github.realgarit.PKHeX-Avalonia/`. The Flatpak grants the
Wayland socket and, only on desktops without Wayland, the X11 socket (`fallback-x11`). The
app uses Avalonia's native Wayland backend when the compositor accepts it and otherwise
its X11 backend. Avalonia's Wayland backend is new and has no screen-reader (AT-SPI)
support yet; screen-reader users can keep the X11 backend with
`flatpak override --user --nosocket=wayland --socket=x11 io.github.realgarit.PKHeX-Avalonia`
(undo with `flatpak override --user --reset io.github.realgarit.PKHeX-Avalonia`). Network
access supports legality resources, mystery gifts, and release notes; DRI access supports
rendering.

### Community FlatPark repository

[FlatPark](https://flatpark.org/apps/io.github.realgarit.PKHeX-Avalonia) is a
community Flatpak repository for apps that are not on Flathub. Its PKHeX-Avalonia
package downloads the unchanged `PKHeX-Avalonia-linux-x64.zip` from each GitHub
release and grants X11, network, IPC and DRI. Without a Wayland socket, the app uses
its X11 backend (XWayland on Wayland desktops) there. Because `FLATPAK_ID` is set, the
app hands updates to `flatpak update` exactly as it does for the release bundle.

FlatPark is maintained independently. It signs with its own key, builds on the
Freedesktop 26.08 runtime like the bundle built and tested here, and picks up new
releases by its own schedule. It uses the same app ID as the release
bundle, so a system should have only one of the two installed. This project accepts
the listing as a community install option; it is not part of the release pipeline,
and a self-hosted repository signed by this project is still tracked separately.

### Rebuild and maintain

The [Flatpak workflow](../.github/workflows/flatpak.yml) compiles the committed
source inside the SDK sandbox without build-time network access, exports a bundle,
installs that bundle and launches the native application under Xvfb. It records
the sandbox permissions, runtime log and screenshot as CI artifacts.

On Linux, install `flatpak-builder`, `appstream`, `desktop-file-utils`, Python 3,
and the three runtimes, then build a committed revision:

```bash
flatpak install --user flathub org.freedesktop.Platform//26.08 org.freedesktop.Sdk//26.08 org.freedesktop.Sdk.Extension.dotnet10//26.08
python3 .github/scripts/prepare-flatpak.py tmp/flatpak
flatpak-builder --user --force-clean --repo=tmp/flatpak/repo tmp/flatpak/build tmp/flatpak/io.github.realgarit.PKHeX-Avalonia.json
flatpak build-bundle --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo tmp/flatpak/repo tmp/flatpak/PKHeX-Avalonia-linux-x86_64.flatpak io.github.realgarit.PKHeX-Avalonia
```

The preparation script reads version, date, packaging and source files from the
same Git commit and pins the local source archive by SHA-256. It refuses to overlay
an existing destination. Uncommitted edits do not enter the build. Dependencies
come from the checked-in `packaging/flatpak/nuget-sources.json`; each package has a
public NuGet URL and a SHA-512 digest. After changing package versions, run
`python3 .github/scripts/update-flatpak-nuget.py` with the .NET 10 SDK installed,
review the package changes, and commit the refreshed list. Dependency generation
uses the network; the actual Flatpak build does not. NuGet vulnerability auditing
is disabled only for the offline publish; normal application CI retains it.

### Official Flathub route

Publishing on Flathub is a separate step. Check its current
[requirements](https://docs.flathub.org/docs/for-app-authors/requirements), use a
stable source release, perform the required offline build, runtime and linter
checks, and follow the
[submission process](https://docs.flathub.org/docs/for-app-authors/submission)
against `flathub/flathub`'s `new-pr` branch. Reviewers decide acceptance; neither a
local bundle nor a submitted PR establishes a listing. The GitHub-based ID
`io.github.realgarit.PKHeX-Avalonia` matches this repository and permits later
[owner verification](https://docs.flathub.org/docs/for-app-authors/verification)
after acceptance and collaborator access.

The application is GPL-3.0-only with GPL-compatible vendored AutoMod code and
separately attributed assets. Existing third-party sprite/artwork provenance and
the original 64px app icon are retained in the package; this work does not certify
third-party artwork redistribution or trademark approval. Flathub requires a
distinct compliant name/icon and prefers a scalable icon or at least a 256px PNG.
Those publication requirements need the maintainer's independent review before
submission; the installed repository bundle is not described as Flathub-ready.
