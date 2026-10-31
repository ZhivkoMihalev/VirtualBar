# Every bug gets a bug issue

Every bug you find gets a GitHub issue, right away, so it is never forgotten. It does not matter where it turns up:
in code you are changing or only reading, in a test run, in CI, in a review, or while testing another issue.

- **Before moving on**, search the open issues for a duplicate. If there is none, open one with the labels `bug` and
  `status: To Do`.
- **Write it like the existing bug issues** (#41, #66):
  - **Problem:** what goes wrong and how to reproduce it;
  - **Relevant code;**
  - **Proposed fix;**
  - **Done when**, which always includes a test that fails without the fix (see `regression-tests.md`);
  - **Status.**
- **Do this even when the fix looks trivial**, or is out of scope for the current task.
- **Never fix an unrelated bug silently inside another PR.** The PR names the new issue (`#N`) as a known gap instead.

## Not a new issue
- **A mistake in the change you are still making** is part of that work.
- **A test that fails the issue under test** sends that issue back to `In Progress` (see the status table under CI in
  CLAUDE.md).
