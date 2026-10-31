#!/usr/bin/env python3
"""Stage a version-only update of the Flathub packaging repository for a released tag.

The Flathub repository (flathub/io.github.realgarit.PKHeX-Avalonia) is updated from
``packaging/flatpak/flathub/io.github.realgarit.PKHeX-Avalonia.yaml.in``: the tag's
source archive URL and SHA-256, the source commit, the version and the release date are
filled in, and ``nuget-sources.json`` is copied from the same tag.

Only those values may change automatically. Before writing anything, the script renders
the template with the values found in the Flathub manifest that is already published
and requires the result to be byte-identical to that manifest. Any other difference
(permissions, runtime, build commands, a new template) means the automation would
publish a change that Flathub moderators have not reviewed, so the script stops with
exit code 2 and leaves the checkout untouched. Those updates stay manual.

Exit codes: 0 staged changes, 1 error, 2 manual update required, 3 already current.
"""

import argparse
import hashlib
import re
import subprocess
import sys
import urllib.request
from pathlib import Path

APP_ID = "io.github.realgarit.PKHeX-Avalonia"
ARCHIVE_URL = "https://github.com/realgarit/PKHeX-Avalonia/archive/refs/tags/v{version}.tar.gz"
TOKENS = ("@VERSION@", "@SOURCE_REVISION@", "@RELEASE_DATE@", "@SOURCE_SHA256@")


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args])


def read_lf(path):
    """Read text with LF newlines whatever the checkout's EOL conversion did."""
    return path.read_bytes().decode("utf-8").replace("\r\n", "\n")


def render(template, values):
    for token in TOKENS:
        template = template.replace(token, values[token])
    return template


def published_values(manifest):
    """Recover the four release values from the manifest Flathub currently builds."""
    patterns = {
        "@VERSION@": r"archive/refs/tags/v(\d+\.\d+\.\d+)\.tar\.gz",
        "@SOURCE_SHA256@": r"sha256: ([0-9a-f]{64})",
        "@SOURCE_REVISION@": r"SourceRevisionId=([0-9a-f]{40})",
        "@RELEASE_DATE@": r"install-assets\.sh \d+\.\d+\.\d+ [0-9a-f]{40} (\d{4}-\d{2}-\d{2})",
    }
    values = {}
    for token, pattern in patterns.items():
        found = set(re.findall(pattern, manifest))
        if len(found) != 1:
            sys.exit(f"Could not recover {token} from the published Flathub manifest ({len(found)} candidates)")
        values[token] = found.pop()
    return values


def archive_sha256(url):
    digest = hashlib.sha256()
    with urllib.request.urlopen(url) as response:  # noqa: S310 - fixed https host
        if response.status != 200:
            sys.exit(f"Downloading {url} failed with HTTP {response.status}")
        for chunk in iter(lambda: response.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("tag", help="released tag, for example v1.98.4")
    parser.add_argument("flathub", type=Path, help="checkout of the Flathub packaging repository")
    parser.add_argument("--source", type=Path, default=Path(__file__).resolve().parents[2],
                        help="PKHeX-Avalonia checkout that contains the tag (default: this repository)")
    args = parser.parse_args()

    match = re.fullmatch(r"v(\d+\.\d+\.\d+)", args.tag)
    if match is None:
        sys.exit(f"Tag {args.tag!r} is not a release tag of the form vX.Y.Z")
    version = match[1]
    try:
        revision = git(args.source, "rev-parse", "--verify", "--quiet", f"refs/tags/{args.tag}^{{commit}}").decode().strip()
    except subprocess.CalledProcessError:
        sys.exit(f"Tag {args.tag} is not present in {args.source}; fetch the release tags first")
    props = git(args.source, "show", f"{revision}:Directory.Build.props").decode()
    if f"<UIVersion>{version}</UIVersion>" not in props:
        sys.exit(f"{args.tag} does not carry <UIVersion>{version}</UIVersion>; refusing to publish a mismatched version")
    release_date = git(args.source, "show", "-s", "--format=%cs", revision).decode().strip()

    # The template comes from the checked-out workflow revision, not from the tag, so a
    # reviewed template change applies to the next release without re-tagging.
    template = read_lf(args.source / "packaging" / "flatpak" / "flathub" / f"{APP_ID}.yaml.in")
    manifest_path = args.flathub / f"{APP_ID}.yaml"
    sources_path = args.flathub / "nuget-sources.json"
    if not manifest_path.is_file() or not sources_path.is_file():
        sys.exit(f"{args.flathub} is not a checkout of the Flathub packaging repository")

    published = read_lf(manifest_path)
    old_values = published_values(published)
    if render(template, old_values) != published:
        print("::error::The published Flathub manifest differs from the template beyond the release values.")
        print("Permission, runtime or build changes need a reviewed manual update of the Flathub repository;")
        print("the automation only bumps version, source commit, release date, archive SHA-256 and nuget-sources.json.")
        print(f"Published values: {old_values}")
        return 2
    if old_values["@VERSION@"] == version:
        print(f"Flathub already builds {version}; nothing to do.")
        return 3

    url = ARCHIVE_URL.format(version=version)
    values = {
        "@VERSION@": version,
        "@SOURCE_REVISION@": revision,
        "@RELEASE_DATE@": release_date,
        "@SOURCE_SHA256@": archive_sha256(url),
    }
    manifest_path.write_bytes(render(template, values).encode("utf-8"))
    sources_path.write_bytes(git(args.source, "show", f"{revision}:packaging/flatpak/nuget-sources.json"))
    print(f"Staged Flathub update {old_values['@VERSION@']} -> {version} from {revision} ({release_date}); archive {url}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
