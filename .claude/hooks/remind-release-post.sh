#!/bin/bash
# PostToolUse hook (Bash): after a `gh pr merge` in this repo, remind the agent that every
# release is announced on Discord by release.yml's discord-announce job (see AGENTS.md).
# Reads the raw hook JSON without jq, because jq is not installed on every machine (Windows).
input=$(cat)

if echo "$input" | grep -qE '"command"[[:space:]]*:[[:space:]]*"[^"]*gh[[:space:]]+pr[[:space:]]+merge' \
  && ! echo "$input" | grep -qE -- '--repo[[:space:]=]+[^[:space:]"]*flathub'; then
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"Release announcement (AGENTS.md, Shipped update communication): the discord-announce job in release.yml posts the Discord update automatically from the ## Release note section of each merged PR. Once the release run finishes, confirm that job posted (or correctly found no user-visible change). Only if it failed or was skipped, post the text-only update through the Chrome extension, after checking #downloads-and-updates so nothing is posted twice."}}'
else
  echo '{}'
fi
