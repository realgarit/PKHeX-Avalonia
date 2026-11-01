#!/usr/bin/env python3
"""Post a text-only release announcement to the project Discord through a webhook.

Usage: announce-release.py <tag> [--dry-run]

The message lists the user-visible changes of every pull request merged since the
previous published GitHub release:

* A `## Release note` section in the PR description is used as written. A section
  that says `none` (or `-`) leaves the PR out.
* Without that section, a `feat:` or `fix:` PR title is used, minus its prefix, but
  only when the PR changes app code (a top-level PKHeX.* project).
* Every other PR (ci, docs, test, chore, deps, refactor, sync, ...) is left out.

When nothing is left, nothing is posted: releases without a visible change stay
quiet, as the project rules require.

Environment: GH_TOKEN (gh CLI), GH_REPO (owner/name), DISCORD_WEBHOOK_URL (not
needed for --dry-run). Exit codes: 0 posted or nothing to post, 1 error.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.request

PRODUCT = "PKHeX-Avalonia"
DISCORD_LIMIT = 2000
VISIBLE_PREFIXES = ("feat", "fix")
NOTE_HEADING = re.compile(r"^#{1,6}\s*release\s+notes?\s*:?\s*$", re.IGNORECASE)
ANY_HEADING = re.compile(r"^#{1,6}\s")
PREFIX = re.compile(r"^(?P<type>[a-z]+)(?:\([^)]*\))?(?P<bang>!)?:\s*(?P<rest>.+)$", re.IGNORECASE)
MERGE_SUBJECT = re.compile(r"^Merge pull request #(\d+)\b")
SQUASH_SUBJECT = re.compile(r"\(#(\d+)\)\s*$")
EMPTY_NOTES = {"none", "-", "n/a", "no", "nothing"}


def run(*args: str) -> str:
    return subprocess.run(args, check=True, capture_output=True, text=True).stdout


def previous_release(tag: str) -> str | None:
    """Newest published GitHub release older than `tag`.

    Uses releases, not tags: a tag whose release failed (v1.98.5) never reached
    users, so its changes belong to the next announcement.
    """
    releases = json.loads(run("gh", "release", "list", "--limit", "100",
                              "--exclude-drafts", "--json", "tagName,publishedAt"))
    tags = [r["tagName"] for r in releases if r["tagName"] != tag]
    for candidate in tags:  # newest first
        merged = subprocess.run(["git", "merge-base", "--is-ancestor", candidate, tag])
        if merged.returncode == 0:
            return candidate
    return None


def pull_requests(base: str | None, tag: str) -> list[int]:
    rng = f"{base}..{tag}" if base else tag
    numbers: list[int] = []
    for subject in run("git", "log", "--first-parent", "--format=%s", rng).splitlines():
        match = MERGE_SUBJECT.match(subject) or SQUASH_SUBJECT.search(subject)
        if match and int(match.group(1)) not in numbers:
            numbers.append(int(match.group(1)))
    numbers.reverse()  # oldest first, so the message reads in merge order
    return numbers


def release_note(body: str) -> str | None:
    """The PR's `## Release note` section; '' when it opts out; None when absent or blank."""
    body = re.sub(r"<!--.*?-->", "", (body or "").replace("\r\n", "\n"), flags=re.DOTALL)
    lines = body.split("\n")
    for i, line in enumerate(lines):
        if NOTE_HEADING.match(line.strip()):
            section: list[str] = []
            for nxt in lines[i + 1:]:
                if ANY_HEADING.match(nxt.strip()):
                    break
                section.append(nxt.rstrip())
            text = "\n".join(section).strip()
            if not text:
                return None  # template left untouched: fall back to the title
            return "" if text.lower().rstrip(".") in EMPTY_NOTES else text
    return None


def from_title(title: str) -> str | None:
    match = PREFIX.match(title.strip())
    if not match or match.group("type").lower() not in VISIBLE_PREFIXES:
        return None
    rest = match.group("rest").strip()
    rest = rest[0].upper() + rest[1:]
    return rest if rest.endswith((".", "!", "?")) else rest + "."


def touches_app(files: list[dict]) -> bool:
    """True when the PR changes shipped app code, not only tooling around it."""
    for entry in files:
        path = entry.get("path", "")
        top = path.split("/", 1)[0]
        if top.startswith("PKHeX.") or path == "Directory.Build.props":
            return True
    return False


def changes(numbers: list[int]) -> list[str]:
    lines: list[str] = []
    for number in numbers:
        pr = json.loads(run("gh", "pr", "view", str(number), "--json", "title,body,state,files"))
        if pr.get("state") != "MERGED":
            continue
        note = release_note(pr.get("body") or "")
        if note is not None:
            text = note
        elif touches_app(pr.get("files") or []):
            text = from_title(pr["title"])
        else:
            text = None  # a feat/fix of CI, docs, tests or packaging scripts
        print(f"#{number} {pr['title']!r}: " + ("left out" if not text else "included"))
        if text:
            lines.append(text)
    return lines


def message(tag: str, lines: list[str]) -> str:
    header = f"{PRODUCT} {tag} is available."
    body = "\n".join(lines)
    text = f"{header}\n{body}"
    if len(text) > DISCORD_LIMIT:
        text = text[: DISCORD_LIMIT - 1].rsplit("\n", 1)[0] + "\n…"
    return text


def post(url: str, content: str) -> str:
    payload = json.dumps({"content": content, "allowed_mentions": {"parse": []}}).encode()
    request = urllib.request.Request(
        url + ("&" if "?" in url else "?") + "wait=true", data=payload, method="POST",
        headers={"Content-Type": "application/json", "User-Agent": "PKHeX-Avalonia-release"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)["id"]


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--dry-run"]
    dry_run = "--dry-run" in sys.argv[1:]
    if len(args) != 1 or not re.fullmatch(r"v\d+\.\d+\.\d+", args[0]):
        print(__doc__, file=sys.stderr)
        return 1
    tag = args[0]
    if subprocess.run(["git", "rev-parse", "-q", "--verify", f"refs/tags/{tag}"],
                      capture_output=True).returncode != 0:
        print(f"::error::Tag {tag} does not exist.", file=sys.stderr)
        return 1

    base = previous_release(tag)
    print(f"Changes since {base or 'the first commit'} up to {tag}:")
    lines = changes(pull_requests(base, tag))
    if not lines:
        print(f"No user-visible change in {tag}; nothing to announce.")
        return 0

    content = message(tag, lines)
    print("----- message -----\n" + content + "\n-------------------")
    if dry_run:
        print("Dry run: not posted.")
        return 0

    url = os.environ.get("DISCORD_WEBHOOK_URL", "")
    if not url:
        print("::error::DISCORD_WEBHOOK_URL is empty.", file=sys.stderr)
        return 1
    try:
        print(f"Posted Discord message {post(url, content)}.")
    except urllib.error.HTTPError as error:
        print(f"::error::Discord rejected the post: HTTP {error.code}.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
