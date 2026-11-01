// Shared helpers for the document-repository scripts: the repo, its tracked files, the rule files and their paths.
import { execSync } from 'node:child_process';
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';

export const repo = execSync('git rev-parse --show-toplevel', { encoding: 'utf8' }).trim();
export const git = command => execSync(command, { cwd: repo, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
export const read = path => readFileSync(join(repo, path), 'utf8').replace(/\r\n/g, '\n');
export const exists = path => existsSync(join(repo, path));

export const tracked = () => git('git ls-files').split('\n').filter(Boolean);
export const untracked = () => git('git ls-files --others --exclude-standard').split('\n').filter(Boolean);

const FRONTMATTER = /^---\n([\s\S]*?)\n---\n\n?/;

/** The `paths` of a rule file's frontmatter (an empty list means the rule is always loaded), and its body. */
export const parseRule = text => {
  const front = text.match(FRONTMATTER);
  if (!front) return { paths: [], body: text, frontmatter: null };
  const paths = front[1].split('\n').map(l => l.match(/^\s*-\s*"(.*)"\s*$/)?.[1]).filter(Boolean);
  return { paths, body: text.slice(front[0].length), frontmatter: front[1] };
};

export const RULES = '.claude/rules';
/** Every rule file, as a path relative to .claude/rules, with its paths and body. */
export const rules = () => readdirSync(join(repo, RULES), { recursive: true })
  .filter(f => f.endsWith('.md'))
  .map(f => f.replaceAll('\\', '/'))
  .sort()
  .map(file => ({ file, ...parseRule(read(`${RULES}/${file}`)) }));

/** A glob as Claude Code's rule paths use it: `**\/`, a trailing `/**`, `*`, literal text. */
export const globToRegex = (pattern, flags = '') => {
  let re = '';
  for (let i = 0; i < pattern.length; i++) {
    if (pattern.startsWith('**/', i)) { re += '(?:.*/)?'; i += 2; }
    else if (pattern.startsWith('/**', i) && i + 3 === pattern.length) { re += '/.*'; i += 2; }
    else if (pattern === '**') { re += '.*'; i += 1; }
    else if (pattern[i] === '*') re += '[^/]*';
    else re += pattern[i].replace(/[.+?^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp('^' + re + '$', flags);
};

// Claude Code also tries a rule's patterns against every folder on the path (#80), and the docs do not say whether
// it ignores case, so matching here ignores case: it can only report a rule too many, never one too few.
const folders = file => file.split('/').map((_, i, parts) => parts.slice(0, i + 1).join('/'));
export const matches = (file, patterns) => {
  const regexes = patterns.map(p => globToRegex(p, 'i'));
  return folders(file).some(path => regexes.some(re => re.test(path)));
};

/** The rule files Claude Code loads when it opens `file`. */
export const rulesFor = (file, all = rules()) => all.filter(r => r.paths.length && matches(file, r.paths)).map(r => r.file);

/** Lines outside fenced code blocks, with their 1-based numbers. */
export const proseLines = text => {
  const out = [];
  let fenced = false;
  text.split('\n').forEach((line, i) => {
    if (line.trimStart().startsWith('```')) { fenced = !fenced; return; }
    if (!fenced) out.push({ line, number: i + 1 });
  });
  return out;
};

/** Every documentation file the skill looks after. */
export const docFiles = () => ['CLAUDE.md', ...rules().map(r => `${RULES}/${r.file}`),
  ...readdirSync(join(repo, '.claude/agents')).filter(f => f.endsWith('.md')).map(f => `.claude/agents/${f}`)];
