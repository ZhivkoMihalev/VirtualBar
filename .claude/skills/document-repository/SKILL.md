---
name: document-repository
description: Update VirtualBar's documentation after a change so it matches the code and keeps the repository's documentation structure. Finds the rule files in .claude/rules that the changed files load, updates them (and CLAUDE.md, an agent or a docs/features status line only where needed), creates a new rule file for a new feature or area, and verifies everything with bundled checks (names and paths exist, entity tables, enums, i18n namespaces, settings, format, index). Use it whenever the user runs /document-repository or asks to document, update or check the docs, rules or CLAUDE.md for a change, a branch, a commit range or a PR; after implementing a feature or a fix, before its PR; and when asked to audit the documentation against the code — even if they only say "update the docs" or "are the rules still right?".
argument-hint: "[base ref | <from> <to> | --all]"
---

# Document the repository

Bring the documentation in line with what changed in the code, the way the repository organises it. The conventions
live in `.claude/rules/documentation.md`; this skill is the workflow around them, plus scripts that find what a change
touches and check the result.

Edit documentation only. Do not change code, commit, push or open a PR unless the user asks: the user decides how the
documentation lands, usually in the same PR as the change. A fact that turns out wrong because the **code** is wrong is
a bug: file it as `.claude/rules/bug-issues.md` says and name it in the report, instead of documenting the bug or
fixing the code on the side.

All scripts run with Node from anywhere inside the repo: `node .claude/skills/document-repository/scripts/<script>.mjs`.

## 1. Read the conventions
Read `.claude/rules/documentation.md` first. It says where each kind of fact goes, when a rule is always loaded or has
`paths`, and how a rule file is written. Every edit below follows it.

## 2. Find what the change touches
Run `affected.mjs` with the skill's argument:
- no argument: the current branch plus uncommitted and untracked work, against `master`;
- one ref (`master`, a commit): everything since it; two refs (`<from> <to>`): only those commits;
- `--all` (a full audit): skip this step, run `check.mjs` (step 4) and then review every rule file against its code.

It prints:
- **Rule files to review**, each with the changed files that load it. These are the candidates; a rule is affected only
  if the change alters something it says.
- **Always loaded** files: review them only if the change alters a command, a CRITICAL rule, the structure or the
  process.
- **Hints** for this kind of change (an entity, a controller, settings, translations, routes, CI…).
- **Changed code that no domain rule covers**: a new feature may need a rule, or an existing rule new `paths`.
- **Documentation changed so far**, so you can see what the change already updated.

Then read the change itself (`git diff <base>...HEAD -- <file>`, plus `git diff HEAD` for uncommitted work) until you
know what behaviour changed. The rule files describe behaviour, so a refactor that changes nothing visible may need no
documentation at all; say so rather than editing for the sake of it.

## 3. Update the documentation
For each affected rule file, open it next to the changed code and make the smallest edit that makes it true:
- change the facts that changed, add the new ones, remove the ones that no longer hold;
- keep the file's structure and wording elsewhere; do not rewrite parts the change does not touch;
- take every name and number from the code, not from the diff's description or from memory.

What usually needs checking, by kind of change:
- **Entity:** its property table (every property except navigation and collections), indexes and invariants; a new
  `DbSet` in `database/app-db-context.md`.
- **Enum:** the member list and every "N members" or "(N)" count that names it.
- **Controller:** the endpoint list (route, verb, `[Authorize]`/`[AllowAnonymous]`, status codes); a new
  `[AllowAnonymous]` also goes into `AnonymousEndpoints` in the HTTP tests (`testing/http.md`).
- **Service or decorator:** the behaviour and limits in the feature's domain rule; new cross-feature effects
  (a notification, a badge hook, catalog linking) in Related rules, on both sides.
- **Settings, translations, routes, components, CI, commands:** the lists in the rule the hint names; commands and
  structure in `CLAUDE.md`.

**A new feature or area** (new entity, service, endpoints or page with no domain rule): create
`.claude/rules/domain/<feature>.md` (or the folder that fits), shaped like the existing ones:

```markdown
---
paths:
  - "**/*Feature*"
  - "**/feature*"
---

# Feature

## FeatureEntity
| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | … |

## Design
- …

## API endpoints (`/api/feature`)
- `GET /api/feature` — …

## Related rules
- … (`.claude/rules/domain/<other>.md`)
```

Then add Related rules back from the other side, add the file to the `CLAUDE.md` index, and check with
`affected.mjs --files <a file it should load for> <one it should not>` that the `paths` load where they should.
A new file of an existing feature that its rule's `paths` miss: extend those `paths` instead.

**When the code contradicts its own intent** (its XML doc, its spec, the issue it implements): describe what the code
does, not what it was meant to do. File the bug, and name it in the rule as a known gap, the way
`.claude/rules/domain/users.md` names #81. Documenting the intended behaviour as if it worked hides the bug from the
next reader. If it is a mistake in the very change being documented (not yet on `master`), tell the user instead of
filing it (`bug-issues.md` → Not a new issue).

**Agents** (`.claude/agents/`): change one only if its role, workflow or report changes. Never copy a rule into it.

**`docs/features/`:** do not rewrite a spec. When a planned feature has now been built, give its `00-OVERVIEW.md` the
Status line (see `documentation.md`) and link the spec from the rule file.

**`CLAUDE.md`:** only for facts every session needs. If it nears 200 lines, move detail into the rule of its area.

## 4. Check the result
1. Run `check.mjs` and fix every error. It checks:
   - every name and path in backticks exists in the code;
   - entity property tables match their classes, and the DbSets table matches `AppDbContext`;
   - enums: every "N members" or "(N)" count, and every line or table that names an enum and lists its members lists
     all of them;
   - endpoints: every documented `VERB /api/…` exists, and a controller whose endpoints are listed (two or more) has
     all of them documented; a controller with one incidental mention is only a warning;
   - the i18n namespaces match `bg.json`, and a settings list (a bullet that starts with the backticked section name and
     a dash) names every key of that `appsettings.json` section;
   - format: no prose line over 300 characters, no two rule files with the same `paths`, every rule file in the
     `CLAUDE.md` index, frontmatter only `paths`, `CLAUDE.md` under 200 lines.

   Read its warnings too. A name the documentation mentions on purpose although the code lacks it (an API Claude must
   not use, something removed, a negation like "No `POST /api/distilleries`") goes into `scripts/known-names.txt` with
   the reason; never add one there to silence a fact that went stale.
2. After moving, merging, splitting or reformatting documentation, run `compare.mjs [base]`. Every name or number it
   reports as lost must be a deliberate removal (a stale fact, a renamed file) that the report names.
3. Re-read what you wrote about behaviour against the code once more: the scripts check names and lists, not whether a
   sentence describes the code correctly.

## 5. Report
Tell the user, briefly:
- each file changed and why (one line each), and files reviewed that needed no change;
- the result of `check.mjs` (and `compare.mjs` if run);
- bugs found and the issues filed;
- anything not verified, and any decision left to the user (for example, a fact the code leaves ambiguous).
