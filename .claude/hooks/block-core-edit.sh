#!/bin/bash
# PreToolUse hook (Edit|Write|MultiEdit): PKHeX.Core must stay a 1:1 mirror of
# upstream kwsch/PKHeX. Block edits to it except on a sync branch created by
# the sync-upstream-core skill.
input=$(cat)

# Decode tool_input.file_path with a real JSON parser: jq, else Python (python3 can be the
# Windows Store stub that exits non-zero, so each parser must succeed before it is used),
# else a sed fallback. Windows backslashes are turned into slashes afterwards.
decode_path() {
  local out
  if out=$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty' 2>/dev/null); then
    printf '%s' "$out"; return
  fi
  for py in python3 python; do
    if out=$(printf '%s' "$input" | "$py" -c 'import sys,json; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' 2>/dev/null); then
      printf '%s' "$out"; return
    fi
  done
  printf '%s' "$input" \
    | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"' \
    | head -n 1 \
    | sed -E 's/^"file_path"[[:space:]]*:[[:space:]]*"//; s/"$//; s/\\\\/\\/g'
}

f=$(decode_path | tr '\\' '/')

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
