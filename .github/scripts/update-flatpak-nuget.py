#!/usr/bin/env python3
"""Refresh offline NuGet sources after dependencies change; run before committing."""

import base64
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

# .NET runtime identifier -> Flatpak architecture. Flathub builds both; the GitHub bundle is x86_64.
RUNTIMES = {"linux-x64": "x86_64", "linux-arm64": "aarch64"}


def only_arches(name):
    """RID-specific packs (runtime, apphost) are fetched only by the matching Flatpak architecture."""
    return [arch for rid, arch in RUNTIMES.items() if name.endswith(f".{rid}")]


def main():
    root = Path(__file__).resolve().parents[2]
    # Keep the packages on real disk inside the worktree and never share a mutable global cache.
    scratch = root / "tmp"
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="flatpak-nuget-", dir=scratch) as folder:
        packages = Path(folder)
        for rid in RUNTIMES:
            subprocess.run([
                "dotnet", "restore", "PKHeX.Avalonia/PKHeX.Avalonia.csproj", "-r", rid,
                "-p:Configuration=Release", "-p:SelfContained=false", "--packages", str(packages),
            ], cwd=root, check=True)
        sources = []
        for package in sorted(packages.glob("**/*.nupkg"), key=lambda p: p.name):
            name, version = package.parent.parent.name, package.parent.name
            filename = package.name.lower()
            sha512 = hashlib.sha512(package.read_bytes()).hexdigest()
            # Check NuGet's downloaded integrity sidecar as well as hashing the actual bytes.
            sidecar = package.with_suffix(".nupkg.sha512")
            if sidecar.exists() and base64.b64decode(sidecar.read_text().strip()).hex() != sha512:
                raise ValueError(f"Package integrity mismatch: {filename}")
            source = {
                "type": "file",
                "url": f"https://api.nuget.org/v3-flatcontainer/{name}/{version}/{filename}",
                "sha512": sha512, "dest": "nuget-sources", "dest-filename": filename,
            }
            if arches := only_arches(name):
                source["only-arches"] = arches
            sources.append(source)
        if not sources:
            raise ValueError("Restore produced no packages")
        output = root / "packaging/flatpak/nuget-sources.json"
        output.write_text(json.dumps(sources, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {len(sources)} pinned package sources to {output}")


if __name__ == "__main__":
    main()
