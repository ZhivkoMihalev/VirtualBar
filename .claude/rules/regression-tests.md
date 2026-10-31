# Every bug fix comes with a test that catches the bug

A bug that was fixed once must never come back unnoticed. So every bug fix ships with a test that reproduces the bug:
it fails on the code before the fix and passes after it. A bug fix without such a test is not done, however small the
fix.

## What the test must do
- **Reproduce the bug as the issue describes it:** the same input or steps, asserting the right behaviour where the bug
  gave the wrong one. A test that only raises coverage, or checks something next to the bug, does not count.
- **Fail without the fix.** Check it once: run the test with the fix reverted (or before writing the fix), watch it go
  red for the reason in the issue, then restore the fix and watch it go green. A test that passes on the broken code
  proves nothing.
- **Name the issue** in the test's doc comment (e.g. `(#66)`), so a later reader knows which bug it guards and does not
  delete it as redundant.
- Follow the conventions of the test project it goes into (Testing Conventions in CLAUDE.md).

## Where it goes
At the lowest layer that still fails on the bug, which is the fastest test that can catch it:

| Where the bug lives | Test |
|---|---|
| A service or decorator: logic, validation, a wrong `Result` | `VirtualBar.Tests/Services/<Service>Tests` |
| A chain across services (trigger → notification → badge) | `VirtualBar.Tests/Integration/` |
| Between the request and the service: routing, `[Authorize]`/`[AllowAnonymous]`, binding, status code, JSON shape, middleware, startup configuration | `VirtualBar.HttpTests` |
| A frontend function, hook, component or page | `VirtualBar.Web.Tests/unit/` |
| A flow that goes wrong only in the real browser against the real stack | `VirtualBar.Web.Tests/e2e/` |

## In the issue and the PR
- The bug issue's **Done when** lists the test: a test that fails without the fix (see `bug-issues.md`).
- The PR that fixes the bug names its test(s) and says that each was seen failing without the fix.

## Never
- Never delete, skip or loosen a regression test to make a later change pass. If the behaviour it pins really has to
  change, the issue or PR of that change says so explicitly.
- Never skip the test silently. If a bug really cannot be caught by an automated test, stop and ask the user before
  fixing it without one.

## Not covered
- **A mistake in the change you are still making** (not yet on `master`): the tests of that change cover it.
