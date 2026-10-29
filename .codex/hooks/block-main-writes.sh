#!/bin/bash
# PreToolUse hook (Bash): block git commit/push while checked out on main.
# Project convention is to always work in a branch and land changes via PR.
# Reads the hook JSON without jq (not installed on every machine).
input=$(cat)
cmd=$(printf '%s' "$input" \
  | grep -oE '"command"[[:space:]]*:[[:space:]]*"([^"\\]|\\.)*"' \
  | head -n 1 \
  | sed -E 's/^"command"[[:space:]]*:[[:space:]]*"//; s/"$//; s/\\n/\n/g; s/\\"/"/g')
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)

# Matches `git commit`/`git push`, also with global options such as `git -c key=value commit`.
if [[ "$branch" == "main" ]] && printf '%s\n' "$cmd" | grep -qE '(^|&&|;|\|)[[:space:]]*git([[:space:]]+-[^[:space:]]+([[:space:]]+[^-[:space:]][^[:space:]]*)?)*[[:space:]]+(commit|push)\b'; then
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Direct commits/pushes to main are blocked. Create a feature branch and open a PR instead (project convention: no direct pushes to main)."}}'
else
  echo '{}'
fi
