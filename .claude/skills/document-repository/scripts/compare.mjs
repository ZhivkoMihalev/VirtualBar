// After a restructure (moving, merging, splitting or reformatting documentation), proves nothing was lost:
// compares every name in backticks and every number in CLAUDE.md, .claude/rules and .claude/agents at <base> with the
// working tree. Usage: node .claude/skills/document-repository/scripts/compare.mjs [base]   (default: master)
import { docFiles, git, read } from './lib.mjs';

const base = process.argv[2] ?? 'master';
const strip = text => text.replace(/\r/g, '').replace(/^---\n[\s\S]*?\n---\n/, '');
const listed = git(`git ls-tree -r --name-only ${base} -- CLAUDE.md .claude/rules .claude/agents`).split('\n').filter(f => f.endsWith('.md'));
const before = listed.map(f => strip(git(`git show ${base}:${f}`))).join('\n');
const after = docFiles().map(f => strip(read(f))).join('\n');

const count = (text, re) => { const m = new Map(); for (const [, x] of text.matchAll(re)) m.set(x, (m.get(x) ?? 0) + 1); return m; };
let lostAny = false;
const report = (label, re) => {
  const b = count(before, re), a = count(after, re);
  const lost = [...b.keys()].filter(k => !a.has(k));
  const fewer = [...b.entries()].filter(([k, n]) => a.has(k) && a.get(k) < n).map(([k, n]) => `${k} (${n} → ${a.get(k)})`);
  const added = [...a.keys()].filter(k => !b.has(k));
  lostAny ||= lost.length > 0;
  console.log(`${label}: ${b.size} distinct at ${base}, ${a.size} now`);
  console.log(`  lost entirely (${lost.length}): ${lost.join(' | ') || '-'}`);
  console.log(`  fewer mentions (${fewer.length}): ${fewer.join(' | ') || '-'}`);
  console.log(`  new (${added.length}): ${added.join(' | ') || '-'}`);
};
report('Names in backticks', /`([^`\n]+)`/g);
report('Numbers', /(?<![\w.#-])(\d+(?:[.,]\d+)?)(?![\w])/g);
console.log(lostAny
  ? '\nSomething disappeared: check each lost item was removed on purpose (a stale fact, a renamed file) and say so.'
  : '\nNothing lost.');
