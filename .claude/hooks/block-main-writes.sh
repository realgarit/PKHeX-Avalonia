#!/bin/bash
# PreToolUse hook (Bash): block git commit/push while checked out on main.
# Project convention is to always work in a branch and land changes via PR.
input=$(cat)

# Decode tool_input.command with a real JSON parser: jq, else Python (python3 can be the
# Windows Store stub that exits non-zero, so each parser must succeed before it is used).
# The sed fallback decodes the escapes that can separate shell words (\n \t \r \").
decode_command() {
  local out
  if out=$(printf '%s' "$input" | jq -r '.tool_input.command // empty' 2>/dev/null); then
    printf '%s' "$out"; return
  fi
  for py in python3 python; do
    if out=$(printf '%s' "$input" | "$py" -c 'import sys,json; print(json.load(sys.stdin).get("tool_input",{}).get("command",""))' 2>/dev/null); then
      printf '%s' "$out"; return
    fi
  done
  printf '%s' "$input" \
    | grep -oE '"command"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"' \
    | head -n 1 \
    | sed -E 's/^"command"[[:space:]]*:[[:space:]]*"//; s/"$//; s/\\[nr]/\n/g; s/\\t/\t/g; s/\\"/"/g'
}

cmd=$(decode_command)
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)

# Matches `git commit`/`git push`, also with global options such as `git -c key=value commit`.
if [[ "$branch" == "main" ]] && printf '%s\n' "$cmd" | grep -qE '(^|&&|;|\|)[[:space:]]*git([[:space:]]+-[^[:space:]]+([[:space:]]+[^-[:space:]][^[:space:]]*)?)*[[:space:]]+(commit|push)\b'; then
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Direct commits/pushes to main are blocked. Create a feature branch and open a PR instead (project convention: no direct pushes to main)."}}'
else
  echo '{}'
fi
