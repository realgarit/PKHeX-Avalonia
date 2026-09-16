---
name: discord-feedback-triage
description: Use when the agent must review PKHeX-Avalonia Discord feedback, prepare ASD-STE100 replies, or create evidence-backed GitHub issues through the authenticated Chrome extension, including continuous scans for new messages.
---

# PKHeX-Avalonia Discord feedback triage

Use this skill for every PKHeX-Avalonia Discord feedback run. The goal is to
collect new feedback, separate facts from assumptions, prepare concise replies,
and act only within the approval boundary.

## Non-negotiable boundary: Discord through Chrome only

- Use the user's existing authenticated Chrome session and the Chrome
  extension.
- Reuse the existing Chrome browser binding when it is available. On first
  setup, bind to Chrome explicitly.
- Do not use the in-app Browser, a web search tool, Discord API calls, HTTP
  requests, cookie inspection, local-storage inspection, or another Discord
  client as a fallback.
- If Chrome is not authenticated, the extension is unavailable, or Discord
  blocks the action, stop the affected action. Keep the draft and report the
  exact boundary. Never bypass login or permissions.
- Confirm the server name and channel in the visible UI before reading or
  posting. Never act on a similarly named server or channel.

The Chrome-only rule applies to reading and posting Discord messages. GitHub
issue work may use the authenticated GitHub connector. If that connector is
blocked, an already authenticated `gh` CLI is an acceptable GitHub-only
fallback. Do not use that fallback to access Discord.

## Approval boundary

Default behavior is read and draft only. Posting a Discord reply or opening a
GitHub issue is an external action.

Treat an explicit user instruction such as “post these replies”, “reply
directly”, or “run it immediately because there are new messages” as approval
to prepare and act on the current batch. Do not extend that approval to
unrelated future messages, private channels, or other systems.

The browser's action-time confirmation requirement still applies to each
representational Discord action. An instruction to run immediately does not
replace that final confirmation. Before sending, show the exact destination,
parent, and text for the imminent batch and obtain the confirmation required
by the browser policy. Group only well-defined imminent replies. If the user
has not approved the exact outgoing payload at that boundary, return drafts and
wait.

## Source scan

For a normal new-message run, inspect these channels first:

1. `#feedback`
2. `#support`
3. `#bug-reports`
4. `#general`
5. `#downloads-and-updates`

For a full audit, also inspect the other visible community channels, such as
announcements, welcome, introductions, showcase, and off-topic. Do not claim
that all Discord feedback was checked when only a subset was visible.

### Notification completeness gate

A message-history scan is not a complete Discord scan. After reading the
channels, inspect the visible server sidebar and Discord Inbox in Chrome:

1. Check server and channel indicators for `unread`, counts, mention markers,
   and `new` markers.
2. Open Inbox and inspect both `Unreads` and `Mentions`.
3. Follow each visible Inbox item to its source channel or thread. Read and
   classify it before treating the notification as resolved.
4. Open every flagged visible server channel, including a channel with a
   `NEW` marker. A `NEW` marker can mean a new channel rather than new
   feedback, but it must be identified and recorded.
5. Do not enter DMs or private channels unless the user explicitly includes
   them. Report an out-of-scope notification instead of inspecting private
   content.

Only claim a complete scan when the relevant channel history, sidebar state,
and Inbox state have all been checked. Otherwise report a partial scan and
name the unresolved indicator. Record the notification result, such as
`Inbox: Unreads empty; Mentions empty; server NEW marker resolved to welcome
channel`, with the scan cutoff.

For each candidate message, record in the working result, without persisting
private save data:

- server, channel, author, timestamp, and message ID or stable visible
  locator;
- complete text, links, and whether an attachment or screenshot is present;
- whether the message is a root message, a reply, or a quoted/nested reply;
- the last-reviewed cutoff used to decide that it is new.

Use the visible Discord UI and accessible DOM. Search by a unique phrase,
author, date, and channel when possible. Do not use an article index as the
message identity. A new reply, a quoted reply, or a GitHub embed can change
article ordering.

Build one outgoing manifest per reply. Include the channel ID, exact parent
message ID or permalink, complete draft text, and a short draft hash. Use the
same values for the final parent check, duplicate check, and post-action audit.
If Discord does not expose a stable ID or permalink and two candidates remain
possible, stop and do not send.

If no checkpoint exists, inspect the recent visible history and clearly mark
the scan as a baseline. Do not reply to old messages as if they were new.

## Triage decisions

Classify every relevant message before drafting:

| Class | Decision |
| --- | --- |
| Clearly wrong behavior | Verify the visible symptom and current issue state. Open an issue only if it is not a duplicate. |
| Ambiguous bug report | Ask for the missing version, OS, save format, steps, or screenshot. Do not create a speculative issue. |
| Feature or UI request | Prepare a short acknowledgement. Do not promise a roadmap or implementation date. |
| Already supported behavior | Verify the current UI or source path, then explain where the option exists. |
| Fixed or confirmed resolved | Acknowledge the confirmation. Do not open a new issue. |
| Existing or duplicate issue | Link the existing issue. Do not create a second issue. |
| No actionable feedback | Do not post. |

“Clearly wrong” means that the report shows an objective mismatch, such as a
selected species displaying another species' sprite, or that current source,
tests, or a reliable reproduction contradicts the observed behavior. A
screenshot can prove the UI result that it shows. It does not prove the root
cause, universal reproduction, save provenance, platform, or app version.
State those limits explicitly.

Before classifying a report as missing functionality, check the actual current
Avalonia path and relevant tests. For example, do not open a form-selector
issue when the Main tab already exposes the Form control for multi-form
species. Preserve the repository rule that `PKHeX.Core/` is an upstream mirror:
triage may inspect it, but must not edit it as a workaround.

## GitHub issue gate

For a clearly wrong, non-duplicate report:

1. Search all GitHub issues, not only open issues. Search by the symptom,
   species or feature, and likely wording.
2. Inspect plausible matches with `gh issue view` or the authenticated GitHub
   connector.
3. Create one factual issue with a precise title and the appropriate labels.
4. Read the created issue back and verify its number, title, body, and URL.

The issue body should contain:

- source Discord channel and stable message link or locator;
- report date, author, and attachment or screenshot description;
- exact observed steps, actual result, and expected result;
- app version, OS, save format, and reproduction status, using “unknown” when
  the report does not provide them;
- evidence limits and an explicit statement when the root cause is unconfirmed;
- a request for the missing details and focused regression coverage.

Never publish private save data, trainer identity, personal data, or a private
attachment. Never state that a bug is reproduced, fixed, or caused by a
specific asset until the evidence supports that statement.

If the GitHub connector returns a permission error, use the authenticated
`gh` CLI for the issue only. Verify the result with `gh issue view`. If both
paths fail, keep the issue draft and report the blocker.

## ASD-STE100 reply rules

For the current PKHeX-Avalonia server, write replies in simple English unless
the user asks for another language. Use ASD-STE100-style wording:

- use short, direct sentences and active voice;
- use one idea per sentence and one instruction per line;
- use concrete technical terms and clear subjects;
- avoid idioms, vague promises, unnecessary context, and speculation;
- do not use the em dash character (`—`), including in pasted links or edits;
- preserve issue links and exact product names;
- do not add emojis or extra claims to an approved draft.

Before posting, scan the complete reply for `—` and remove it. A normal
hyphen in a technical term is not an em dash.

Useful draft shapes:

```text
Thanks for the report. This behavior is incorrect.
I opened [issue link].
Please provide the app version, OS, save format, and exact reproduction steps.
```

```text
Thanks for the suggestion. I recorded this as a UI enhancement.
I cannot promise an implementation date.
```

```text
Small correction to my earlier reply: [verified correction].
```

Keep the draft proportional to the evidence. If the report is ambiguous, ask
for the missing information instead of calling it a confirmed bug.

## Posting a direct Discord reply

Post one reply at a time. For every reply:

 1. Locate the exact parent by the manifest's channel ID and message ID or
    permalink. Re-check full text, author, timestamp, channel, and attachment
    details. Do not select by `nth()` or an unstable article position.
2. Distinguish a root message from a nested reply. If the intended parent is a
   quoted reply, open the reply context and select that specific message.
3. Treat GitHub embeds as content inside the Discord message. Do not reply to
   an issue page or embed instead of the Discord message.
4. Use the message's `Reply` action. Right-clicking the exact article and
   choosing `Reply` is a reliable route when the hover action is ambiguous.
5. Before entering text, verify the composer shows `Replying to` the intended
   author and message. Cancel and reselect if it does not.
6. Check the final text. Confirm that it is the approved draft and contains no
   em dash. Send once.
 7. Wait for the UI to update. Confirm that the new message is in the intended
    channel, is attached to the intended parent, contains the complete text,
    and is not a new root message. Record the message ID or stable locator.
    Confirm that the composer is empty.

Deduplicate by `channel ID + parent message ID + draft hash`. Identical text
under different parents is not a duplicate. Do not resend because the UI is
slow. Refresh or inspect the channel first to check whether the reply already
exists. Do not post a duplicate reply to a message that already has the
intended response.

If a reply lands in the wrong channel, under the wrong parent, or as a root
message, verify the mistake. Delete the incorrect message if the user account
has permission, record that deletion in the result, and repost only after the
correct parent is verified. If deletion is not available, stop and report the
mistake. Do not hide it by posting more messages.

## Result format

Return a compact ledger with one row per relevant message:

| Source | Class | Evidence | Draft or action | Status |
| --- | --- | --- | --- | --- |
| channel, author, time, locator | triage class | verified facts and limits | exact reply or issue action | drafted, posted, issue opened, duplicate, blocked, or no action |

Also report:

- the last-reviewed cutoff for the next run;
- the Chrome notification state, including Inbox `Unreads` and `Mentions`;
- every Discord reply URL or stable locator that was posted;
- every issue URL that was opened or linked;
- unresolved questions and external blockers;
- any corrected or deleted mistaken post.

Do not claim that a message was posted or an issue was created without
post-action verification.

## Common failure modes

| Failure | Required correction |
| --- | --- |
| Using the in-app Browser because it is convenient | Stop and return to the authenticated Chrome extension. |
| Treating channel history as proof that Discord has no notifications | Check sidebar indicators, Inbox `Unreads`, Inbox `Mentions`, and every flagged visible channel. |
| Selecting an article by index | Re-find it by text, author, timestamp, and message ID. |
| Replying to a quoted reply instead of its root, or vice versa | Inspect the reply context and verify the composer target. |
| Opening an issue before checking duplicates | Search open and closed issues first. |
| Treating a screenshot as proof of root cause | Report the visible symptom and evidence limits only. |
| Posting before user approval | Keep the exact draft and wait, unless the current batch was explicitly approved. |
| Sending a slow reply twice | Check the channel before retrying. |
| Posting a normal channel message instead of a reply | Verify, delete the mistaken own message when allowed, and repost correctly. |
| Leaving an em dash in the final text | Search for `—` before send and after verification. |
| Publishing private save data | Remove it from the draft and issue body. |
