# Leak-check pre-commit / pre-push hook

This repository ships a mandatory leak guard (`.githooks/`) that scans **staged** content (and,
on push, the commit range leaving your machine) and blocks the operation if it finds:

- a forbidden private path (`docs/`, `reviews/`, `.claude-memory/`, `.superpowers/`, `.worktrees/`,
  `testdata/`, `CLAUDE.md`, `todo.md`);
- an mbox `From ` envelope outside `fixtures/` (real-mail leak);
- an email address not in `.githooks/leak-check-allowlist.txt`;
- a home-directory absolute path (`C:/Users/…`, `/home/…`);
- an API-token-shaped secret (`ghp_…`, `sk-…`).

## Install (per clone)

```bash
git config core.hooksPath .githooks
```

## False positives

Add the address (or its `@domain`) to `.githooks/leak-check-allowlist.txt`. Lockfiles and
license files are already exempt from the email scan; the hook's own files (`.githooks/`) are
exempt from content scanning.

## Override (eyes-open only)

```bash
CONTINUMAIL_LEAK_CHECK_OVERRIDE=I_KNOW_THIS_MAY_LEAK_PII git commit ...
```

The override is logged loudly. Use it only when you are certain a match is a false positive.
