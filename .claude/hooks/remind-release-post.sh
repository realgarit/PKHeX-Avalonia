#!/bin/bash
# PostToolUse hook (Bash): after a `gh pr merge` in this repo, remind the agent that every
# verified release gets a Discord announcement (standing approval, see AGENTS.md).
# Reads the raw hook JSON without jq, because jq is not installed on every machine (Windows).
input=$(cat)

if echo "$input" | grep -qE '"command"[[:space:]]*:[[:space:]]*"[^"]*gh[[:space:]]+pr[[:space:]]+merge' \
  && ! echo "$input" | grep -qE -- '--repo[[:space:]=]+[^[:space:]"]*flathub'; then
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"Release announcement required (AGENTS.md, Shipped update communication): once this merge is confirmed and its release run has published the new version with all assets, post the text-only update in the project Discord #downloads-and-updates channel through the Chrome extension, following .agents/skills/discord-feedback-triage. The user gave standing approval: post without asking and do not skip it. Skip it only when the release has no change a user can see."}}'
else
  echo '{}'
fi
