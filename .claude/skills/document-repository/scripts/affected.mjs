// Which documentation a change touches: the changed files, the rule files whose paths match them, and hints.
// Usage (from anywhere in the repo):
//   node .claude/skills/document-repository/scripts/affected.mjs                 the branch + uncommitted work vs master
//   node .claude/skills/document-repository/scripts/affected.mjs <base>          since <base> (a branch, tag or commit)
//   node .claude/skills/document-repository/scripts/affected.mjs <base> <to>     the commits base..to only
//   node .claude/skills/document-repository/scripts/affected.mjs --files a b ... the given files
import { git, rules, rulesFor, untracked } from './lib.mjs';

const args = process.argv.slice(2);
const isDoc = f => f === 'CLAUDE.md' || f.startsWith('.claude/') || f.startsWith('docs/') || /(^|\/)[^/]+\.md$/.test(f);

let changed = [];
let scope;
if (args[0] === '--files') {
  changed = args.slice(1).map(f => ({ status: '?', file: f.replaceAll('\\', '/') }));
  scope = 'the given files';
} else {
  const parse = out => out.split('\n').filter(Boolean).map(l => { const [s, ...p] = l.split('\t'); return { status: s[0], file: p.at(-1) }; });
  const fallback = () => { try { return git('git merge-base HEAD origin/master').trim(); } catch { return 'master'; } };
  const base = args[0] ?? fallback();
  if (args[1]) {
    changed = parse(git(`git diff --name-status ${base}..${args[1]}`));
    scope = `${base}..${args[1]}`;
  } else {
    changed = [...parse(git(`git diff --name-status ${base}...HEAD`)), ...parse(git('git diff --name-status HEAD')),
      ...untracked().map(file => ({ status: 'A', file }))];
    scope = `${base.slice(0, 10)}...HEAD plus uncommitted and untracked files`;
  }
}
changed = [...new Map(changed.map(c => [c.file, c])).values()].sort((a, b) => a.file.localeCompare(b.file));

const all = rules();
const code = changed.filter(c => !isDoc(c.file));
const docs = changed.filter(c => isDoc(c.file));

console.log(`Scope: ${scope}`);
console.log(`Changed: ${code.length} code/config files, ${docs.length} documentation files\n`);

const byRule = new Map();
for (const c of code) for (const r of rulesFor(c.file, all)) (byRule.get(r) ?? byRule.set(r, []).get(r)).push(c.file);
console.log('Rule files to review (path-scoped, loaded by the changed files):');
if (!byRule.size) console.log('  none');
[...byRule.entries()].sort((a, b) => b[1].length - a[1].length).forEach(([r, files]) => {
  console.log(`  .claude/rules/${r}  (${files.length}: ${files.slice(0, 4).join(', ')}${files.length > 4 ? ', …' : ''})`);
});

console.log('\nAlways loaded (review only if the change alters what they say):');
console.log('  CLAUDE.md');
all.filter(r => !r.paths.length).forEach(r => console.log(`  .claude/rules/${r.file}`));

const hints = [
  [/^VirtualBar\.Domain\/Entities\/.+\.cs$/, 'An entity changed: its property table in the domain rule; a new DbSet goes into database/app-db-context.md.'],
  [/^VirtualBar\.Domain\/(Enums\/.+|Entities\/.+Type)\.cs$/, 'An enum changed: its members and every "N members" count that names it.'],
  [/AppDbContext\.cs$/, 'AppDbContext changed: the DbSets table and the global rules in database/app-db-context.md.'],
  [/\/Migrations\//, 'A migration: entity tables and indexes; a destructive one needs the db-destructive-approved label (ci/migration-guard.md).'],
  [/Controllers\/.+\.cs$/, 'A controller changed: the endpoint lists; a new [AllowAnonymous] goes into AnonymousEndpoints (testing/http.md); a new mutating action needs an ownership test.'],
  [/Services\/.+Service\.cs$/, 'A service changed: the behaviour in its domain rule, and the Related rules it touches (notifications, badges, catalog).'],
  [/Decorators\/.+\.cs$/, 'A decorator changed: validation limits and error codes in the domain rule.'],
  [/appsettings[^/]*\.json$/, 'Settings changed: every rule that lists the keys of that section, and CLAUDE.md if a command depends on it.'],
  [/Program\.cs$/, 'Program.cs changed: the middleware order (backend/middleware.md) and startup (backend/startup.md).'],
  [/src\/i18n\/.+\.json$/, 'Translations changed: the namespace list in frontend/conventions.md; bg.json and en.json keep the same keys.'],
  [/src\/App\.tsx$/, 'App.tsx changed: the route list in frontend/routing.md.'],
  [/src\/components\/[^/]+\.tsx$/, 'A shared component changed: frontend/shared-components.md.'],
  [/src\/contexts\//, 'A context changed: frontend/conventions.md → Contexts.'],
  [/^\.github\//, 'CI changed: the ci/ rules, and CLAUDE.md → Build & Run if a command changed.'],
  [/(^|\/)package\.json$|\.csproj$/, 'Dependencies or scripts changed: CLAUDE.md → Build & Run, frontend/conventions.md → npm workspaces, ci/codeql-and-dependabot.md.'],
  [/\.claude\/agents\//, 'An agent changed: it keeps its role, workflow, checklist and report format; rules belong in .claude/rules.'],
];
const hinted = hints.filter(([re]) => changed.some(c => re.test(c.file))).map(([, text]) => text);
console.log('\nWhat this kind of change usually needs:');
console.log(hinted.length ? hinted.map(h => '  - ' + h).join('\n') : '  nothing specific');

const uncovered = code.filter(c => c.status !== 'D' && !rulesFor(c.file, all).some(r => r.startsWith('domain/')))
  .filter(c => /\.(cs|ts|tsx)$/.test(c.file) && !/\/(Migrations|obj|bin)\//.test(c.file));
console.log('\nChanged code that no domain rule covers (a new feature may need a rule, or a rule new paths):');
console.log(uncovered.length ? uncovered.map(c => `  ${c.status} ${c.file}`).join('\n') : '  none');

const added = changed.filter(c => c.status === 'A' && !isDoc(c.file)).map(c => c.file);
const newTop = [...new Set(added.map(f => f.split('/')[0]))].filter(top => !git(`git ls-tree --name-only HEAD`).split('\n').includes(top));
if (newTop.length) console.log(`\nNew top-level folders (CLAUDE.md → Solution Structure): ${newTop.join(', ')}`);

console.log('\nDocumentation changed so far:');
console.log(docs.length ? docs.map(c => `  ${c.status} ${c.file}`).join('\n') : '  none');
