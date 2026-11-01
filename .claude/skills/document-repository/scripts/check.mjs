// Checks CLAUDE.md, .claude/rules and .claude/agents against the code and against the documentation conventions.
// Usage: node .claude/skills/document-repository/scripts/check.mjs [--verbose]
// Exits 1 when an error is found. Warnings are things to look at, not failures.
import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { docFiles, exists, git, matches, proseLines, read, repo, rules, RULES, tracked } from './lib.mjs';

const verbose = process.argv.includes('--verbose');
const errors = [];
const warnings = [];
const error = (where, text) => errors.push(`${where}: ${text}`);
const warn = (where, text) => warnings.push(`${where}: ${text}`);

const files = tracked().concat(git('git ls-files --others --exclude-standard').split('\n').filter(Boolean));
const folders = new Set(files.flatMap(f => f.split('/').slice(0, -1).map((_, i, p) => p.slice(0, i + 1).join('/'))));
const sourceExt = /\.(cs|ts|tsx|js|mjs|json|yml|yaml|csproj|sh|css|html|props)$/;
const sources = files.filter(f => sourceExt.test(f) && !/seed\.json$/.test(f) && !/^(\.claude|docs)\//.test(f) && exists(f));
// Words of the source code, plus the parts of every file name (`products.brandy.seed.json` names `brandy`).
const words = new Set([...sources.map(read).join('\n').match(/[A-Za-z_][A-Za-z0-9_]*/g), ...files.join('\n').match(/[A-Za-z_][A-Za-z0-9_]*/g)]);
const topFolders = new Set(files.filter(f => f.includes('/')).map(f => f.split('/')[0]));
const here = dirname(fileURLToPath(import.meta.url));
const known = new Set(readFileSync(join(here, 'known-names.txt'), 'utf8').split('\n').map(l => l.replace(/#.*/, '').trim()).filter(Boolean));
const ruleList = rules();
const docs = docFiles();

// 1. Every name and path in backticks exists in the code.
const roots = ['', 'VirtualBar.Web/', 'VirtualBar.Web.Tests/'];
const pathExists = token => {
  const t = token.replace(/\/$/, '');
  if (roots.some(r => files.includes(r + t) || folders.has(r + t))) return true;
  if (files.some(f => f.endsWith('/' + t)) || [...folders].some(f => f.endsWith('/' + t))) return true;
  // A gitignored path (coverage-results/, a log under VirtualBar.Api/logs/) is documented on purpose.
  return ['', ...[...topFolders].map(f => f + '/')].some(prefix => {
    try { execSync(`git check-ignore -q --no-index "${prefix}${token}"`, { cwd: repo, stdio: 'ignore' }); return true; } catch { return false; }
  });
};
const looksLikePath = token => {
  const last = token.split('/').at(-1);
  return token.endsWith('/') || /\.[A-Za-z]{1,6}$/.test(last) || topFolders.has(token.split('/')[0]) || /^(src|unit|e2e)\//.test(token);
};
let checkedNames = 0;
for (const doc of docs) {
  for (const { line, number } of proseLines(read(doc))) {
    for (const [, raw] of line.matchAll(/`([^`]+)`/g)) {
      const trimmed = raw.trim().replace(/^\[|\]$/g, '').replace(/\(\)$/, '');
      const token = trimmed.replace(/<[^<>]*>/g, '');
      if (known.has(trimmed) || known.has(token) || /[*{}?<>\s'"()]/.test(token) || /Xxx/.test(token)) continue;
      // Not paths: aliases and relative imports (`@/`, `./client`), bare extensions (`.ts`), packages and
      // slash-separated lists (`@tanstack/react-query`, `try/catch`, `Liked10/50/100`).
      if (/^(@|\.\.?\/)/.test(token) || /^\.\w+$/.test(token)) continue;
      const isPath = token.includes('/') ? looksLikePath(token) : /\.(cs|ts|tsx|json|md|yml|csproj|sh|png|mjs)$/.test(token);
      if (token.includes('/') && !isPath) continue;
      if (isPath) {
        if (/^(https?:|\/)/.test(token) || /</.test(trimmed)) continue;
        checkedNames++;
        if (!pathExists(token)) error(`${doc}:${number}`, `path not found: ${token}`);
        continue;
      }
      // A name starting with `_` is a fragment (`_one` plurals, `<MethodName>_When<Condition>_…`), not an identifier.
      if (!/^[A-Za-z][\w.]*$/.test(token)) continue;
      checkedNames++;
      const absent = token.split('.').filter(p => p && !words.has(p));
      if (absent.length) error(`${doc}:${number}`, `name not in the code: ${token}`);
    }
  }
}

// 2. Entity property tables match the entity classes.
const entityDir = 'VirtualBar.Domain/Entities';
const entityFiles = files.filter(f => f.startsWith(entityDir + '/') && f.endsWith('.cs') && exists(f));
const classes = new Set(entityFiles.filter(f => /\bclass\s/.test(read(f))).map(f => f.slice(entityDir.length + 1, -3)));
let tables = 0;
for (const r of ruleList) {
  const lines = r.body.split('\n');
  lines.forEach((l, i) => {
    const name = l.match(/^#+ (\w+)/)?.[1];
    if (!name || !classes.has(name)) return;
    let t = i + 1;
    while (t < lines.length && !lines[t].startsWith('#') && !lines[t].startsWith('| Property')) t++;
    if (!lines[t]?.startsWith('| Property')) return;
    tables++;
    const documented = [];
    for (let j = t + 2; j < lines.length && lines[j].startsWith('|'); j++) documented.push(...[...lines[j].split('|')[1].matchAll(/`(\w+)\??`/g)].map(m => m[1]));
    const src = read(`${entityDir}/${name}.cs`);
    const actual = [...src.matchAll(/public\s+(?!sealed|class|abstract|static)([\w<>?,\[\]\s]+?)\s+(\w+)\s*\{\s*get;/g)]
      .filter(m => !/ICollection|IList|List</.test(m[1]) && !classes.has(m[1].replace('?', '').trim()))
      .map(m => m[2]);
    const missing = actual.filter(p => !documented.includes(p));
    const extra = documented.filter(p => !actual.includes(p));
    if (missing.length) error(`${RULES}/${r.file}`, `${name}: in the class, not in the table: ${missing.join(', ')}`);
    if (extra.length) error(`${RULES}/${r.file}`, `${name}: in the table, not in the class: ${extra.join(', ')}`);
  });
}

// 3. The DbSets table matches AppDbContext.
const dbRule = ruleList.find(r => r.file === 'database/app-db-context.md');
const dbContextPath = 'VirtualBar.Infrastructure/Persistence/AppDbContext.cs';
if (dbRule && exists(dbContextPath)) {
  const actual = [...read(dbContextPath).matchAll(/DbSet<\w+>\s+(\w+)/g)].map(m => m[1]);
  const documented = [...dbRule.body.matchAll(/^\| `(\w+)` \|/gm)].map(m => m[1]);
  const missing = actual.filter(x => !documented.includes(x));
  const extra = documented.filter(x => !actual.includes(x));
  if (missing.length) error(`${RULES}/database/app-db-context.md`, `DbSets not documented: ${missing.join(', ')}`);
  if (extra.length) error(`${RULES}/database/app-db-context.md`, `documented DbSets that do not exist: ${extra.join(', ')}`);
}

// 4. Every "`Enum` … N members" / "`Enum` (N)" count matches the enum.
const enumFiles = files.filter(f => /^VirtualBar\.Domain\/.+\.cs$/.test(f) && exists(f) && /\benum\s+\w+/.test(read(f)));
const enums = enumFiles.map(f => {
  const m = read(f).match(/enum\s+(\w+)\s*\{([\s\S]*?)\}/);
  const members = m[2].replace(/\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '').split(',').map(s => s.trim()).filter(Boolean);
  return { name: m[1], count: members.length, members: members.map(x => x.replace(/\s*=.*$/, '')) };
});
// A line that names an enum and lists two or more of its members lists all of them; so does the first table under a
// heading that names the enum (first column). A heading or a line ending in ':' takes the next line with it.
for (const doc of docs) {
  const lines = proseLines(read(doc));
  lines.forEach(({ line, number }, i) => {
    for (const { name, members } of enums) {
      if (!new RegExp(`\\b${name}\\b`).test(line)) continue;
      const next = lines.slice(i + 1).find(l => l.line.trim());
      const takesNext = /^#/.test(line) || /:\**\s*$/.test(line);
      const segment = line + (takesNext && next && !next.line.startsWith('|') ? ' ' + next.line : '');
      const named = members.filter(m => new RegExp(`(?<![\\w.])${m}(?![\\w])`).test(segment.replace(new RegExp(`\\b${name}\\b`, 'g'), '')));
      if (named.length >= 2 && named.length < members.length) {
        error(`${doc}:${number}`, `lists ${named.length} of the ${members.length} ${name} members; missing ${members.filter(m => !named.includes(m)).join(', ')}`);
      }
      if (/^#/.test(line)) {
        const table = [];
        for (let j = i + 1; j < lines.length && !lines[j].line.startsWith('#'); j++) {
          if (lines[j].line.startsWith('|')) table.push(lines[j].line); else if (table.length) break;
        }
        const first = table.slice(2).map(r => r.split('|')[1]?.match(/`(\w+)`/)?.[1]).filter(Boolean);
        if (first.length >= 2 && first.every(v => members.includes(v)) && first.length < members.length) {
          error(`${doc}:${number}`, `the ${name} table lists ${first.length} of ${members.length} members; missing ${members.filter(m => !first.includes(m)).join(', ')}`);
        }
      }
    }
  });
}
for (const doc of docs) {
  const text = read(doc);
  for (const { name, count } of enums) {
    for (const m of text.matchAll(new RegExp('`' + name + '`[^\\n`]{0,40}?\\(?\\**(\\d+)\\**(?= members|\\)| keys)', 'g'))) {
      if (Number(m[1]) !== count) error(doc, `${name} has ${count} members, the text says ${m[1]}`);
    }
  }
}

// 5. The i18n namespace list matches bg.json.
const bgPath = 'VirtualBar.Web/src/i18n/bg.json';
const i18nRule = ruleList.find(r => r.body.includes('Current translation namespaces:'));
if (i18nRule && exists(bgPath)) {
  const keys = Object.keys(JSON.parse(read(bgPath)));
  const section = i18nRule.body.slice(i18nRule.body.indexOf('Current translation namespaces:')).split('\n## ')[0];
  const bullets = section.split('\n').reduce((all, l) => {
    if (l.startsWith('- ')) all.push(l.slice(2)); else if (l.startsWith('  ') && all.length) all[all.length - 1] += ' ' + l.trim();
    return all;
  }, []);
  const documented = bullets.flatMap(b => [...b.split(': ')[0].matchAll(/`(\w+)`/g)].map(m => m[1]));
  const missing = keys.filter(k => !documented.includes(k));
  const extra = documented.filter(k => !keys.includes(k));
  if (missing.length) error(`${RULES}/${i18nRule.file}`, `i18n namespaces not documented: ${missing.join(', ')}`);
  if (extra.length) error(`${RULES}/${i18nRule.file}`, `documented namespaces not in bg.json: ${extra.join(', ')}`);
}

// 6. A "- `Section` — `Key`, …" list of appsettings.json keys names every key of that section.
const settingsPath = 'VirtualBar.Api/appsettings.json';
if (exists(settingsPath)) {
  const settings = JSON.parse(read(settingsPath));
  for (const r of ruleList) {
    const lines = r.body.split('\n');
    lines.forEach((l, i) => {
      const section = l.match(/^- `(\w+)` — /)?.[1];
      if (!section || typeof settings[section] !== 'object' || Array.isArray(settings[section])) return;
      let text = l;
      for (let j = i + 1; lines[j]?.startsWith('  '); j++) text += ' ' + lines[j].trim();
      const documented = [...text.matchAll(/`(\w+)`/g)].map(m => m[1]);
      const missing = Object.keys(settings[section]).filter(k => !documented.includes(k));
      if (missing.length) error(`${RULES}/${r.file}`, `${section} keys not listed: ${missing.join(', ')}`);
    });
  }
}

// 7. Endpoints: every documented `VERB /api/…` exists, and a controller whose endpoints are listed (two or more
// documented) has all of them documented. A controller with one incidental mention gets a warning.
const normRoute = p => p.toLowerCase().replace(/\?.*$/, '').replace(/\{[^}]*\}/g, '{}').replace(/\/+$/, '');
const actions = [];
for (const f of files.filter(x => /^VirtualBar\.Api\/Controllers\/.+Controller\.cs$/.test(x) && exists(x))) {
  const src = read(f);
  const cls = src.match(/class\s+(\w+)Controller\b/)?.[1];
  if (!cls) continue;
  const base = (src.match(/^\[Route\("([^"]+)"\)\]/m)?.[1] ?? '').replace('[controller]', cls);
  for (const m of src.matchAll(/\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]/g)) {
    const route = '/' + [base, m[2] ?? ''].filter(Boolean).join('/');
    actions.push({ controller: f.split('/').at(-1), key: `${m[1].toUpperCase()} ${normRoute(route)}`, route: `${m[1].toUpperCase()} ${route}` });
  }
}
const documentedEndpoints = new Map();
for (const doc of docs) {
  for (const { line, number } of proseLines(read(doc))) {
    for (const m of line.matchAll(/`(GET|POST|PUT|PATCH|DELETE) (\/api\/[^`\s]*)/g)) {
      if (known.has(`${m[1]} ${m[2]}`)) continue;
      const key = `${m[1]} ${normRoute(m[2])}`;
      if (!documentedEndpoints.has(key)) documentedEndpoints.set(key, `${doc}:${number}`);
    }
  }
}
const actionKeys = new Set(actions.map(a => a.key));
for (const [key, where] of documentedEndpoints) if (!actionKeys.has(key)) error(where, `documented endpoint not in the code: ${key}`);
for (const controller of [...new Set(actions.map(a => a.controller))]) {
  const own = actions.filter(a => a.controller === controller);
  const missing = own.filter(a => !documentedEndpoints.has(a.key));
  const documented = own.length - missing.length;
  if (!missing.length || !documented) continue;
  const text = `${documented} of ${own.length} endpoints documented; missing ${missing.map(a => a.route).join(', ')}`;
  if (documented >= 2) error(controller, text); else warn(controller, text);
}

// 8. Format and structure.
const claude = read('CLAUDE.md');
const claudeLines = claude.split('\n').length - (claude.endsWith('\n') ? 1 : 0);
if (claudeLines >= 200) error('CLAUDE.md', `${claudeLines} lines; keep it under 200 and move detail into a rule file`);
else if (claudeLines >= 190) warn('CLAUDE.md', `${claudeLines} lines, close to the 200 limit`);
for (const doc of docs) {
  for (const { line, number } of proseLines(read(doc).replace(/^---\n[\s\S]*?\n---\n/, m => '\n'.repeat(m.split('\n').length - 1)))) {
    if (line.length > 300 && !line.startsWith('|')) error(`${doc}:${number}`, `prose line of ${line.length} characters; make it a list`);
  }
}
const byPaths = new Map();
for (const r of ruleList) {
  if (r.frontmatter !== null) {
    if (!/^paths:\n(\s+- ".+"\n?)+$/.test(r.frontmatter)) error(`${RULES}/${r.file}`, 'frontmatter must be only `paths:` with a list of quoted patterns');
    if (!r.paths.length) error(`${RULES}/${r.file}`, 'frontmatter without paths; remove it to make the rule always loaded');
  }
  if (!r.body.trimStart().startsWith('# ')) error(`${RULES}/${r.file}`, 'must start with a "# " heading');
  for (const p of r.paths) if (!files.some(f => matches(f, [p]))) warn(`${RULES}/${r.file}`, `pattern matches no file: ${p}`);
  if (r.paths.length) {
    const key = [...r.paths].sort().join('|');
    (byPaths.get(key) ?? byPaths.set(key, []).get(key)).push(r.file);
  }
  const slash = r.file.lastIndexOf('/');
  const folder = slash < 0 ? null : r.file.slice(0, slash + 1);
  const name = r.file.slice(slash + 1);
  const indexed = claude.split('\n').some(l => l.startsWith(folder ? '- `' + folder + '`' : '- `' + name + '`') && l.includes('`' + name + '`'));
  if (!indexed) error('CLAUDE.md', `rule file not in the index under "Current rules": ${r.file}`);
}
for (const group of byPaths.values()) if (group.length > 1) error(RULES, `same paths, so they always load together; merge them or narrow one: ${group.join(', ')}`);
for (const doc of docs.filter(d => d.startsWith('.claude/agents/'))) {
  const front = read(doc).match(/^---\n([\s\S]*?)\n---/);
  if (!front || !/^name: /m.test(front[1]) || !/^description: /m.test(front[1])) error(doc, 'agent frontmatter needs name and description');
}

console.log(`Checked ${docs.length} files: ${checkedNames} names and paths, ${tables} entity tables, ${enums.length} enums, ${ruleList.length} rule files.`);
console.log(`Always loaded: CLAUDE.md (${claudeLines} lines) + ${ruleList.filter(r => !r.paths.length).map(r => r.file).join(', ')}`);
if (warnings.length) { console.log(`\nWarnings (${warnings.length}):`); warnings.forEach(w => console.log('  ' + w)); }
if (errors.length) { console.log(`\nErrors (${errors.length}):`); errors.forEach(e => console.log('  ' + e)); }
console.log(errors.length ? '\nFAILED' : '\nOK');
if (verbose) console.log(`(repo: ${repo})`);
process.exitCode = errors.length ? 1 : 0;
