#!/bin/bash
# PreToolUse hook (Edit|Write|MultiEdit): PKHeX.Core must stay a 1:1 mirror of
# upstream kwsch/PKHeX. Block edits to it except on a sync branch created by
# the sync-upstream-core skill.
# Reads the hook JSON without jq (not installed on every machine) and accepts
# Windows paths, whose backslashes arrive JSON-escaped.
input=$(cat)
f=$(printf '%s' "$input" \
  | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"' \
  | head -n 1 \
  | sed -E 's/^"file_path"[[:space:]]*:[[:space:]]*"//; s/"$//; s/\\\\/\//g')

case "$f" in
  */PKHeX.Core/*|PKHeX.Core/*)
    branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
    case "$branch" in
      chore/sync-pkhex-core-*)
        echo '{}'
        ;;
      *)
        printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"PKHeX.Core must stay a byte-for-byte mirror of upstream kwsch/PKHeX. Port the fix into PKHeX.Application/Infrastructure/Presentation/Avalonia instead. (Allowed on a chore/sync-pkhex-core-* branch during an upstream sync, see the sync-upstream-core skill.)"}}'
        ;;
    esac
    ;;
  *)
    echo '{}'
    ;;
esac
