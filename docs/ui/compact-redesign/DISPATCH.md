# Dispatch, integration and merge order

## Planning deliverable versus implementation

This handoff does not start implementation agents. The user chooses when to dispatch them. Once dispatched, agents implement only their named lane. Keep each in its own worktree; never let agents share a mutable checkout.

Use the commit containing this handoff as `<handoff-sha>` (get it with `git log -1 --format=%H -- docs/ui/compact-redesign`). The initial production snapshot is recorded in README; rebase the handoff onto current origin/main before implementation if main has advanced, then have the integrator recheck file/API names. Do not assume the historical snapshot is still current.

## Recommended low-conflict workflow

1. Start F as coordinator using `agents/F-integrator.md`. It establishes the current base and gives A its assignment. F may inventory bindings/fixtures while A works; F does not edit A's files.
2. Start A using `agents/A-foundation.md`; integrate its passing PR first. F adds the shared localization keys/description fix and any agreed composition-root prerequisite in its own small PR. These shared changes must be accepted before B–E start.
3. Record the resulting `<foundation-sha>`. Create B/C/D/E worktrees from that same commit. With only three worker slots, start C/D/E; start B when D finishes. With four workers, all four may start, but B remains dependent on D for final build/integration.
4. D merges first (PartyStrip exists but is not yet composed); C then E merge; B updates to this accepted base and merges last. No PR may delete unrelated incoming changes to resolve a conflict.
5. F runs the integrated checks and native matrix after B; fixes integration defects in a dedicated branch/PR. A worker's passing branch is not proof the assembled UI passes.
6. Only F coordinates final release verification and any authorized shipped update. No lane announces its partial work as the complete redesign.

Use ordinary PRs to main after checks if each intermediate state is safe. Do not batch-merge concurrent PRs while release verification is required; serialize merge/check state. If intermediate visual states are unsuitable for main, use an integration branch with PRs targeting it and one final PR to main; F must verify required CI actually runs on that target. Never assume PR checks configured only for main also protect another branch. Choose the integration strategy once before starting A and communicate it to every lane.

## Example setup (PowerShell; substitute real accepted SHA)

```powershell
git fetch origin
# Each path must be new. Preserve any existing worktree rather than deleting it.
git worktree add C:/Git/PKHeX-compact-theme -b codex/compact-ui-theme <handoff-sha>
# After foundation and shared resources are accepted:
git worktree add C:/Git/PKHeX-compact-shell -b codex/compact-ui-shell <foundation-sha>
git worktree add C:/Git/PKHeX-compact-editor -b codex/compact-ui-editor <foundation-sha>
git worktree add C:/Git/PKHeX-compact-storage -b codex/compact-ui-storage <foundation-sha>
git worktree add C:/Git/PKHeX-compact-settings -b codex/compact-ui-settings <foundation-sha>
```

Run commit/push/PR commands from the owning feature worktree as required by root AGENTS.md. Do not edit git identity, force-push, reset another agent's work, or delete dirty worktrees. Prefer merging the accepted base into an already-pushed lane branch when updates are needed.

## Copy-ready user dispatch

> Read AGENTS.md and docs/ui/compact-redesign/README.md, DESIGN.md, CONTRACTS.md, VERIFICATION.md, and agents/[YOUR-LANE].md. Implement only that lane in your assigned isolated worktree. The screenshots in reference/ are the approved compact design. You are not alone in the repository: honor file ownership and preserve other agents' work. Do not modify Core, AutoMod or UIVersion. Report prerequisite commits and conflicts to the integrator. Deliver actual rendered light/dark evidence, relevant passing tests, and a precise PR handoff. Do not replace production behavior with the reference program's sample data.

Replace [YOUR-LANE] with A-foundation, B-shell, C-editor, D-storage, E-settings or F-integrator. Send the agent its worktree path, foundation SHA and PR target along with the prompt; do not leave those as placeholders.
