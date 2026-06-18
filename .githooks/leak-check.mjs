// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

// Pure scanner: given staged/changed files, return human-readable leak violations.
// The git/IO plumbing (main) lives below it (added in Task 2) so this part stays unit-testable.

const FORBIDDEN_PREFIXES = [
  'docs/', 'reviews/', '.claude-memory/', '.superpowers/', '.worktrees/', 'testdata/',
];
const FORBIDDEN_EXACT = ['CLAUDE.md', 'todo.md'];

// Files whose CONTENT is fully exempt from scanning: the scanner's own sources/tests, and the
// public hook doc, legitimately contain the patterns it hunts (regexes, sample paths/addresses).
const CONTENT_EXEMPT_PREFIXES = ['.githooks/', 'LEAK_CHECK.md'];
// Files exempt from the EMAIL scan only — legit third-party / generated addresses, plus
// package manifests where `name@version` (e.g. lodash@4.17.21) looks like an email to the regex.
const EMAIL_EXEMPT = [
  /(^|\/)package-lock\.json$/, /(^|\/)package\.json$/,
  /(^|\/)Cargo\.lock$/, /(^|\/)Cargo\.toml$/,
  /(^|\/)LICENSE/i, /(^|\/)NOTICE/i,
];

const EMAIL_RE = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g;
// `name@version` / asset filenames (e.g. 128x128@2x.png, lodash@4.17.21) match EMAIL_RE but are
// not addresses: skip a match whose "TLD" is a known file/asset/code extension, not a real TLD.
const ASSET_TLD_RE = /\.(?:png|jpe?g|gif|webp|ico|svg|bmp|css|js|mjs|cjs|ts|tsx|jsx|json|md|txt|map|html?|xml|ya?ml|lock)$/i;
// mbox postmark: "From <sender> <weekday> <month> ..." — specific enough to skip prose "From the…".
const MBOX_FROM_RE = /^From \S+ (?:Mon|Tue|Wed|Thu|Fri|Sat|Sun) /m;
const HOME_PATH_RE = /(?:[A-Za-z]:[\\/]Users[\\/]|\/home\/|\/Users\/)/;
const TOKEN_RE = /(?:ghp_[A-Za-z0-9]{36}|gh[oprsu]_[A-Za-z0-9]{36}|sk-[A-Za-z0-9]{20,})/;

export function isForbiddenPath(p) {
  return FORBIDDEN_EXACT.includes(p) || FORBIDDEN_PREFIXES.some((pre) => p === pre.slice(0, -1) || p.startsWith(pre));
}
function isContentExempt(p) { return CONTENT_EXEMPT_PREFIXES.some((pre) => p.startsWith(pre)); }
function isEmailExempt(p) { return EMAIL_EXEMPT.some((re) => re.test(p)); }
function isFixture(p) { return p.startsWith('fixtures/'); }

export function emailAllowed(addr, allowlist) {
  const a = addr.toLowerCase();
  if (allowlist.has(a)) return true;
  const at = a.lastIndexOf('@');
  return at >= 0 && allowlist.has('@' + a.slice(at + 1));
}

export function scanFiles(files, allowlist) {
  const violations = [];
  for (const { path: p, content } of files) {
    if (isForbiddenPath(p)) { violations.push(`forbidden path staged: ${p}`); continue; }
    if (content == null || isContentExempt(p)) continue;
    if (!isFixture(p) && MBOX_FROM_RE.test(content))
      violations.push(`mbox "From " envelope outside fixtures/: ${p}`);
    if (HOME_PATH_RE.test(content))
      violations.push(`home-directory absolute path: ${p}`);
    if (TOKEN_RE.test(content))
      violations.push(`token-like secret: ${p}`);
    if (!isEmailExempt(p)) {
      const seen = new Set();
      for (const m of content.matchAll(EMAIL_RE)) {
        const addr = m[0];
        if (ASSET_TLD_RE.test(addr)) continue;   // asset/version filename, not an email
        if (!seen.has(addr) && !emailAllowed(addr, allowlist)) {
          seen.add(addr);
          violations.push(`unallowlisted email address (${addr}): ${p}`);
        }
      }
    }
  }
  return violations;
}

// Binary detection: a NUL byte means git handed us a binary blob; skip content scanning.
export function isBinaryText(text) {
  return text.includes('\u0000');
}

// ── git/IO plumbing (not unit-tested; smoke-tested via the wrappers) ──
import { execFileSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const ALLOWLIST_FILE = '.githooks/leak-check-allowlist.txt';
const OVERRIDE_TOKEN = 'I_KNOW_THIS_MAY_LEAK_PII';
// Skip reading known-binary blobs entirely (no point decoding a 60 MB asset into a JS string;
// also dodges the maxBuffer throw). Content checks don't apply to them anyway.
const BINARY_EXT = /\.(pst|png|ico|jpg|jpeg|gif|webp|exe|dll|psd|zip|gz|pdf|woff2?)$/i;

function loadAllowlist() {
  const set = new Set();
  if (existsSync(ALLOWLIST_FILE)) {
    for (const raw of readFileSync(ALLOWLIST_FILE, 'utf8').split('\n')) {
      const line = raw.trim();
      if (line && !line.startsWith('#')) set.add(line.toLowerCase());
    }
  }
  return set;
}

function gitLines(args) {
  return execFileSync('git', args, { encoding: 'utf8' }).split('\n').filter(Boolean);
}

// Read a git blob as UTF-8 (so Danish/non-ASCII content like Æ Ø Å decodes correctly — those
// letters aren't valid email/path chars, so the ASCII PII patterns are unaffected); return null
// for missing/binary (NUL byte) content.
function readBlob(ref) {
  try {
    const out = execFileSync('git', ['show', ref], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    return isBinaryText(out) ? null : out;
  } catch {
    return null;
  }
}

function main(argv) {
  if (process.env.CONTINUMAIL_LEAK_CHECK_OVERRIDE === OVERRIDE_TOKEN) {
    console.error(`leak-check: OVERRIDE ACTIVE (${OVERRIDE_TOKEN}) — scan skipped. This may leak PII.`);
    return 0;
  }
  const i = argv.indexOf('--range');
  let paths, contentRef;
  if (i >= 0) {
    const range = argv[i + 1];
    paths = gitLines(['diff', '--name-only', '--diff-filter=ACM', range]);
    const tip = range.includes('..') ? range.split('..').pop() : 'HEAD';
    contentRef = (p) => `${tip}:${p}`;
  } else {
    paths = gitLines(['diff', '--cached', '--name-only', '--diff-filter=ACM']);
    contentRef = (p) => `:${p}`;
  }
  const allowlist = loadAllowlist();
  // Don't read known-binary blobs (forbidden-path check still applies via scanFiles on path alone).
  const files = paths.map((p) => ({ path: p, content: BINARY_EXT.test(p) ? null : readBlob(contentRef(p)) }));
  const violations = scanFiles(files, allowlist);
  if (violations.length) {
    console.error('leak-check FAILED — refusing to proceed:');
    for (const v of violations) console.error('  - ' + v);
    console.error(`\nFalse positive? Add the address to ${ALLOWLIST_FILE}, or override (eyes-open):`);
    console.error(`  CONTINUMAIL_LEAK_CHECK_OVERRIDE=${OVERRIDE_TOKEN} git commit ...`);
    return 1;
  }
  return 0;
}

// Run main() only when invoked directly (not when imported by the test).
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  process.exit(main(process.argv.slice(2)));
}
