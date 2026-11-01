---
paths:
  - "CLAUDE.md"
  - ".claude/rules/**"
  - ".claude/agents/**"
  - ".claude/skills/**"
  - "docs/features/**"
---

# Documentation

How the repository's documentation is organised and written. It holds whenever documentation is edited; the
`document-repository` skill (`.claude/skills/document-repository/`) applies it after a change and checks the result.

## Where things go
- **`CLAUDE.md`** is always loaded and stays under 200 lines: what VirtualBar is, the top level of the solution
  structure, the build/run/test commands, CRITICAL RULES, the security rules and the index of rule files. Nothing that
  matters for one area only.
- **`.claude/rules/`** holds every other rule, one topic per file (kebab-case name), in folders by area: `backend/`,
  `database/`, `domain/` (one file per feature: its entities, endpoints and design), `frontend/`, `testing/`, `ci/`.
  The process rules (`bug-issues.md`, `regression-tests.md`, `issue-status.md`) sit at the root.
- **`.claude/agents/`** holds an agent's role, its workflow, a short checklist that names rule files, and its report
  format. Never a copy of a rule: agents receive `CLAUDE.md` and the rules themselves.
- **`docs/features/<feature>/`** holds a feature's spec, written before the code (`00-OVERVIEW.md` first, then numbered
  slices). Once the feature is built, the spec is its design record: its `00-OVERVIEW.md` opens with a Status line that
  names the rule file describing the code, and the rule file wins where they differ. Specs are not rewritten to match
  the code.
- Documents about the whole project (`PRODUCTION.md`) stay in the repo root.

## Always loaded or `paths`
- A rule is always loaded (no frontmatter) only when it is needed with no code file open: planning, writing an issue,
  opening a PR. `CLAUDE.md` → Current rules names those files.
- Every other rule has frontmatter with only `paths:` and a list of double-quoted globs, relative to the repo root.
- A rule with `paths` loads when Claude reads, writes or edits a matching file (also `cat`/`head` of one file), not on
  Grep or Glob. Each pattern is also tried against every folder on the path, so `**/badges*` loads for
  `docs/features/badges/00-OVERVIEW.md`.
- The docs do not say whether matching ignores case, so a pattern names both cases where the frontend uses lower case:
  `**/*Offer*` and `**/offer*`.
- A domain rule matches the feature's names in file names, the pages that show the feature, and its `docs/features/`
  folder.
- Avoid patterns that catch unrelated files: `*Badge*` catches the shadcn `ui/badge.tsx`, `*Catalog*` catches
  `BadgeCatalog.cs`.
- No two rule files have exactly the same `paths`: files that always load together become one file.

## How a rule file is written
- It starts with a `# ` heading and uses `##`/`###` sections. English.
- **Lists, one fact per item.** Wrap prose at about 120 characters; no prose line over 300.
- **Tables only where each cell holds one short fact** (entity properties, status meanings). A long note moves out of
  the cell into a list under the table ("— see below").
- **An entity's property table lists every property of its class**, except navigation properties and collections.
- **Names in backticks are real:** a class, member, file, route or setting that exists. Placeholders use `Xxx` or
  `<…>`. A name mentioned on purpose although the code lacks it goes into the skill's `scripts/known-names.txt`, with
  the reason.
- **Numbers come from the code** (counts, limits, defaults, intervals), never from memory.
- **A reference to a rule with `paths` names its file** (`.claude/rules/testing/http.md`), never just its section: the
  section is not in context until the file loads. Always-loaded content may be named by section
  (`CLAUDE.md` → CRITICAL RULES).
- **Related rules:** a domain rule whose code touches another feature ends with `## Related rules`, naming those rule
  files by path: the notifications it sends, the badges it evaluates, where its matching runs. The other rule names it
  back.
- A domain rule with a spec ends with "Design record (the spec, written before the code): `docs/features/<feature>/`."

## When the code changes
- The rule that describes the changed code changes in the same PR: a property, endpoint, enum member, setting, route,
  i18n namespace, notification type, badge or limit.
- A new feature gets a `domain/` file with `paths`, Related rules both ways, and a line in the `CLAUDE.md` index.
- Every new rule file gets a line in the `CLAUDE.md` index, one line per folder.
- A fact found wrong because the code is wrong is a bug: it gets a bug issue (`bug-issues.md`), not a silent fix.
- The rule describes what the code does. Where that is a bug, the rule names the issue as a known gap (as
  `domain/users.md` names #81) instead of describing the intended behaviour as if it worked.
- Moving, merging, splitting or reformatting documentation must lose nothing: the skill's `compare.mjs` shows every
  name and number that disappeared, and each one is a deliberate removal named in the PR.
