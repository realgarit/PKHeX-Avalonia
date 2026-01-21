# Upstream Sync Workflow

This workflow automatically checks for new commits in the upstream [PKHeX.Core](https://github.com/kwsch/PKHeX/tree/master/PKHeX.Core) and creates GitHub issues when sync is needed.

## How it works

1. Runs daily at 08:00 UTC (you can also trigger it manually from the Actions tab)
2. Compares the last synced commit SHA (stored in `last-synced-sha.txt`) with the latest upstream commits
3. If new commits are found, it creates an issue with:
   - List of new commits with links
   - Link to the full diff
   - Instructions for updating the baseline

## After syncing

Once you've manually synced the changes to your repo:

1. Update `last-synced-sha.txt` with the new commit SHA
2. Close the sync issue

## Files

- `last-synced-sha.txt` - Stores the last synced commit SHA
- `../workflows/check-upstream-sync.yml` - The workflow file

## Manual trigger

Go to Actions > "Check PKHeX.Core Upstream Sync" > Run workflow