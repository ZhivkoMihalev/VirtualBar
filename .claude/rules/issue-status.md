# Issue status and the project board

`.github/workflows/project-board.yml` (the **Project board** workflow) keeps the **Virtual Bar** board in GitHub Projects in step with the issues' `status: …` labels (`To Do` → `In Progress` → `Ready For Testing` → `In Testing` → `Ready for deployment` → `Done`). The label is the source of truth: change a status by changing the label, and `.github/scripts/sync-project-status.sh` moves the card to the board's Status column of the same name (ignoring case), adding the issue to the board first. An issue is on the board once it has a status label; dragging a card does not change the label. It runs on `opened`/`reopened` and on adding a `status: …` label; run it by hand (Actions → Project board → Run workflow) to sync every issue at once. It needs the `PROJECT_TOKEN` secret, a classic personal access token with only the `project` scope, because the workflow's own token cannot reach a project owned by a user account.

**What each status means:**

| Status | When | Moved by |
|---|---|---|
| `To Do` | The issue is written and not started. | whoever opens it |
| `In Progress` | Work has started on a branch. | the developer |
| `Ready For Testing` | The PR is open or merged and the work waits for a test. Testing happens on `master`, after the merge. | the developer, when opening the PR |
| `In Testing` | Someone is testing it. | the tester |
| `Ready for deployment` | The test passed. The code is in `master` and waits for the next deploy to Azure. | the tester |
| `Done` | It is deployed to Azure. | whoever deploys (manual; there is no deploy workflow yet) |

A test that finds a problem sends the issue back to `In Progress`. An issue stays **open until `Done`**, so a PR never closes it: write `Part of #N` in the PR description, **never** `Closes`/`Fixes`/`Resolves #N`, which would close the issue at merge, before it is tested and deployed. Because testing happens after the merge, everything in `Ready for deployment` is already in `master` and goes out with the next deploy; a future deploy workflow can rely on that to move those issues to `Done` itself.
