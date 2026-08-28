#!/usr/bin/env bash
#
# Resolve the next PKHeX-Avalonia release version.
#
# SOURCE OF TRUTH IS THE GIT TAG SET, NOT Directory.Build.props.
#
# Reading the file is exactly what broke on 2026-08-28: four PRs each read
# UIVersion 1.48.3, each computed "patch + 1" = 1.48.4, and each wrote the
# byte-identical line. Git auto-merged all four with zero conflict, so four
# distinct fixes shipped under one version number and release.yml's
# `paths: [Directory.Build.props]` trigger never fired for three of them.
# Tags cannot collide that way: a tag either exists on the remote or it does
# not, and `git push --atomic` makes claiming one a single server-side check.
#
# Usage:
#   REPO=owner/name HEAD_SHA=<sha> .github/scripts/resolve-release-version.sh
#
# Environment:
#   REPO           owner/name. Defaults to $GITHUB_REPOSITORY, then `gh repo view`.
#   HEAD_SHA       Commit to classify. Defaults to $GITHUB_SHA, then HEAD.
#   MAX_RANGE      Max first-parent commits to classify (default 25).
#   GITHUB_OUTPUT  If set, key=value pairs are appended for GitHub Actions.
#
# Emits (stdout is a human-readable log; GITHUB_OUTPUT gets the machine values):
#   current_tag current_version next_version next_tag bump rule
#   pr_number pr_title head_pr_bump head_pr_rule
#   tag_exists head_already_tagged should_release
#
# Exit status is 0 whenever resolution succeeded, including when the caller
# should skip; read `should_release` rather than the exit code.

set -euo pipefail

MAX_RANGE="${MAX_RANGE:-25}"

if [[ -z "${REPO:-}" ]]; then
  REPO="${GITHUB_REPOSITORY:-}"
fi
if [[ -z "${REPO:-}" ]]; then
  REPO="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
fi

if [[ -z "${HEAD_SHA:-}" ]]; then
  HEAD_SHA="${GITHUB_SHA:-}"
fi
if [[ -z "${HEAD_SHA:-}" ]]; then
  HEAD_SHA="$(git rev-parse HEAD)"
fi
# Normalise to a full SHA: the commits/{sha}/pulls endpoint rejects short SHAs
# it cannot resolve unambiguously.
HEAD_SHA="$(git rev-parse "$HEAD_SHA")"

emit() {
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    # Flatten any newline: a multi-line value in the plain key=value form would
    # let a crafted PR title inject extra step outputs.
    printf '%s=%s\n' "$1" "$(printf '%s' "$2" | tr '\r\n' '  ')" >> "$GITHUB_OUTPUT"
  fi
}

lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }

rank_of() {
  case "$1" in
    major) echo 3 ;;
    minor) echo 2 ;;
    *)     echo 1 ;;
  esac
}

# ---------------------------------------------------------------------------
# Conventional-commit prefix -> SemVer bump, per the repo's documented policy.
# Prints "<bump>|<rule>". `rule` names the branch that matched so the run log
# says loudly why a version moved the way it did.
# ---------------------------------------------------------------------------
classify_title() {
  local title="$1" labels="${2:-}"

  # A `breaking` label outranks any prefix.
  if printf '%s' "$labels" | tr ',' '\n' | grep -qix 'breaking'; then
    echo "major|label:breaking"
    return 0
  fi

  if [[ "$title" =~ ^([A-Za-z]+)(\(([^\)]*)\))?(!)?: ]]; then
    local type bang
    type="$(lower "${BASH_REMATCH[1]}")"
    bang="${BASH_REMATCH[4]}"

    if [[ -n "$bang" ]]; then
      echo "major|prefix-bang:${type}!"
      return 0
    fi

    case "$type" in
      feat|feature)
        echo "minor|prefix:${type}" ;;
      fix|chore|deps|dep|refactor|docs|doc|test|tests|ci|build|perf|style|sync|revert)
        echo "patch|prefix:${type}" ;;
      *)
        echo "patch|default:unmapped-prefix(${type})" ;;
    esac
    return 0
  fi

  echo "patch|default:no-conventional-prefix"
}

# ---------------------------------------------------------------------------
# Resolve the merged PR for a commit. Three independent lookups so a single
# API shape change cannot silently degrade every release to "default patch".
# Prints "<number>\t<title>\t<comma-separated labels>", or nothing.
#
# All filtering goes through gh's own `--jq`, which uses gh's embedded jq. The
# script therefore needs no external `jq` binary and runs identically on a
# GitHub runner and on a maintainer's machine.
# ---------------------------------------------------------------------------
PR_TSV_FILTER='[ (.number // "" | tostring),
                 (.title  // ""),
                 ([ (.labels // [])[] | (.name // .) ] | join(",")) ] | @tsv'

resolve_pr() {
  local sha="$1" row=""

  row="$(gh api "repos/${REPO}/commits/${sha}/pulls" \
           --jq "([.[] | select(.merged_at != null)] | sort_by(.number) | last) // empty | ${PR_TSV_FILTER}" \
         2>/dev/null || true)"

  if [[ -z "$row" ]]; then
    row="$(gh pr list --repo "$REPO" --search "$sha" --state merged --limit 5 \
             --json number,title,labels --jq ".[0] // empty | ${PR_TSV_FILTER}" 2>/dev/null || true)"
  fi

  if [[ -z "$row" ]]; then
    local num
    num="$(git log -1 --format=%s "$sha" 2>/dev/null \
           | sed -nE 's/^Merge pull request #([0-9]+).*/\1/p')"
    if [[ -n "$num" ]]; then
      row="$(gh pr view "$num" --repo "$REPO" --json number,title,labels \
               --jq "${PR_TSV_FILTER}" 2>/dev/null || true)"
    fi
  fi

  printf '%s' "$row"
}

# ---------------------------------------------------------------------------
# 1. Current version: the highest existing vMAJOR.MINOR.PATCH tag.
# ---------------------------------------------------------------------------
CURRENT_TAG="$(git tag -l 'v*' --sort=-v:refname \
               | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' \
               | head -n 1 || true)"

if [[ -n "$CURRENT_TAG" ]]; then
  CURRENT_VERSION="${CURRENT_TAG#v}"
  VERSION_SOURCE="tag ${CURRENT_TAG}"
else
  # Bootstrap only. Once one tag exists this branch is never taken again.
  CURRENT_VERSION="$(grep -oE '<UIVersion>[^<]+' Directory.Build.props | sed 's/<UIVersion>//' || true)"
  CURRENT_VERSION="${CURRENT_VERSION:-0.0.0}"
  VERSION_SOURCE="BOOTSTRAP fallback to Directory.Build.props (no v*.*.* tag exists)"
fi

if [[ ! "$CURRENT_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "::error::Could not parse a SemVer baseline (got '${CURRENT_VERSION}' from ${VERSION_SOURCE})." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# 2. Classify. The pushed commit's PR is the headline rule; every other
#    first-parent commit added since the current tag is classified too and the
#    strongest bump wins.
#
#    Why the range matters: `concurrency` serialises runs, but GitHub cancels a
#    *pending* run when a newer one queues behind an in-progress one. Several
#    merges landing at once can therefore collapse into one release run. Taking
#    the max over the range keeps a coalesced `feat:` from shipping as a patch.
# ---------------------------------------------------------------------------
echo "=============================================================="
echo " Release version resolution"
echo "=============================================================="
echo "repo             : ${REPO}"
echo "head sha         : ${HEAD_SHA}"
echo "head subject     : $(git log -1 --format=%s "$HEAD_SHA")"
echo "current version  : ${CURRENT_VERSION}   (source: ${VERSION_SOURCE})"
echo

PR_NUMBER=""
PR_TITLE=""
PR_LABELS=""
HEAD_BUMP="patch"
HEAD_RULE="default:no-pr-resolved"

if row="$(resolve_pr "$HEAD_SHA")" && [[ -n "$row" ]]; then
  IFS=$'\t' read -r PR_NUMBER PR_TITLE PR_LABELS <<< "$row"
  IFS='|' read -r HEAD_BUMP HEAD_RULE <<< "$(classify_title "$PR_TITLE" "$PR_LABELS")"
  echo "resolved PR      : #${PR_NUMBER} — ${PR_TITLE}"
  echo "PR labels        : ${PR_LABELS:-<none>}"
else
  # No PR anywhere: classify the commit subject so a direct push is still typed.
  PR_TITLE="$(git log -1 --format=%s "$HEAD_SHA")"
  IFS='|' read -r HEAD_BUMP HEAD_RULE <<< "$(classify_title "$PR_TITLE" "")"
  HEAD_RULE="${HEAD_RULE} (from commit subject; no merged PR found for ${HEAD_SHA:0:9})"
  echo "resolved PR      : NONE — falling back to the commit subject"
fi
echo "MATCHED RULE     : ${HEAD_RULE}  ->  ${HEAD_BUMP}"
echo

BUMP="$HEAD_BUMP"
RULE="$HEAD_RULE"
BEST_RANK="$(rank_of "$BUMP")"

if [[ -n "$CURRENT_TAG" ]] && git rev-parse -q --verify "refs/tags/${CURRENT_TAG}" >/dev/null 2>&1; then
  RANGE="$(git rev-list --first-parent "${CURRENT_TAG}..${HEAD_SHA}" 2>/dev/null | head -n "$MAX_RANGE" || true)"
else
  RANGE=""
fi

if [[ -n "$RANGE" ]]; then
  echo "first-parent commits since ${CURRENT_TAG} (max ${MAX_RANGE}):"
  while read -r sha; do
    [[ -z "$sha" ]] && continue
    if [[ "$sha" == "$HEAD_SHA" ]]; then
      echo "  ${sha:0:9}  [head]  ${HEAD_RULE} -> ${HEAD_BUMP}"
      continue
    fi
    r_num=""; r_title=""; r_labels=""
    if r_row="$(resolve_pr "$sha")" && [[ -n "$r_row" ]]; then
      IFS=$'\t' read -r r_num r_title r_labels <<< "$r_row"
    else
      r_title="$(git log -1 --format=%s "$sha")"
    fi
    IFS='|' read -r r_bump r_rule <<< "$(classify_title "$r_title" "$r_labels")"
    echo "  ${sha:0:9}  ${r_num:+#${r_num} }${r_rule} -> ${r_bump}"
    r_rank="$(rank_of "$r_bump")"
    if (( r_rank > BEST_RANK )); then
      BEST_RANK="$r_rank"
      BUMP="$r_bump"
      RULE="${r_rule} (from ${sha:0:9}${r_num:+ #${r_num}}, outranks the head commit)"
    fi
  done <<< "$RANGE"
  echo
fi

if [[ "$BUMP" != "$HEAD_BUMP" ]]; then
  echo "::warning::Head commit classified '${HEAD_BUMP}' but an unreleased commit in the range needs '${BUMP}'. Using the stronger bump."
fi
echo "EFFECTIVE RULE   : ${RULE}"
echo "EFFECTIVE BUMP   : ${BUMP}"

# ---------------------------------------------------------------------------
# 3. Apply the bump.
# ---------------------------------------------------------------------------
IFS='.' read -r MAJ MIN PAT <<< "$CURRENT_VERSION"
case "$BUMP" in
  major) MAJ=$((MAJ + 1)); MIN=0; PAT=0 ;;
  minor) MIN=$((MIN + 1)); PAT=0 ;;
  patch) PAT=$((PAT + 1)) ;;
  *) echo "::error::Unknown bump type '${BUMP}'." >&2; exit 1 ;;
esac
NEXT_VERSION="${MAJ}.${MIN}.${PAT}"
NEXT_TAG="v${NEXT_VERSION}"

# ---------------------------------------------------------------------------
# 4. Skip conditions.
# ---------------------------------------------------------------------------
TAG_EXISTS=false
if git rev-parse -q --verify "refs/tags/${NEXT_TAG}" >/dev/null 2>&1; then
  TAG_EXISTS=true
elif git ls-remote --exit-code --tags origin "refs/tags/${NEXT_TAG}" >/dev/null 2>&1; then
  TAG_EXISTS=true
fi

HEAD_ALREADY_TAGGED=false
if [[ -n "$CURRENT_TAG" ]] && [[ "$(git rev-list -n1 "$CURRENT_TAG" 2>/dev/null || true)" == "$HEAD_SHA" ]]; then
  HEAD_ALREADY_TAGGED=true
fi

SHOULD_RELEASE=true
if [[ "$TAG_EXISTS" == "true" ]]; then
  SHOULD_RELEASE=false
  echo "::warning::${NEXT_TAG} already exists — skipping. Resolve by hand: the tag set and this commit disagree."
fi
if [[ "$HEAD_ALREADY_TAGGED" == "true" ]]; then
  SHOULD_RELEASE=false
  echo "::notice::${HEAD_SHA:0:9} is already released as ${CURRENT_TAG} — nothing new to ship."
fi

echo
echo "--------------------------------------------------------------"
echo " current tag      : ${CURRENT_TAG:-<none>}"
echo " current version  : ${CURRENT_VERSION}"
echo " bump             : ${BUMP}"
echo " NEXT VERSION     : ${NEXT_VERSION}"
echo " next tag         : ${NEXT_TAG}"
echo " tag exists       : ${TAG_EXISTS}"
echo " head already tag: ${HEAD_ALREADY_TAGGED}"
echo " should release   : ${SHOULD_RELEASE}"
echo "--------------------------------------------------------------"

emit current_tag "${CURRENT_TAG}"
emit current_version "${CURRENT_VERSION}"
emit next_version "${NEXT_VERSION}"
emit next_tag "${NEXT_TAG}"
emit bump "${BUMP}"
emit rule "${RULE}"
emit pr_number "${PR_NUMBER}"
emit pr_title "${PR_TITLE}"
emit head_pr_bump "${HEAD_BUMP}"
emit head_pr_rule "${HEAD_RULE}"
emit tag_exists "${TAG_EXISTS}"
emit head_already_tagged "${HEAD_ALREADY_TAGGED}"
emit should_release "${SHOULD_RELEASE}"
