# Security

## Posture: local-first

ContinuMail Converter runs entirely on your machine. It **reads** your `.mbox` archives and
**writes** `.pst` files locally. It makes **no network connections** and **uploads nothing**;
your originals are only ever read, never modified. The desktop app's WebView loads only
local, bundled assets.

## Content Security Policy

The desktop app (Tauri v2 + WebView2) ships a restrictive CSP
(`desktop/src-tauri/tauri.conf.json`):

```
default-src 'self'; img-src 'self' asset: http://asset.localhost data:; style-src 'self' 'unsafe-inline'; font-src 'self' data:; script-src 'self'; connect-src 'self' ipc: http://ipc.localhost; object-src 'none'; base-uri 'self'; frame-ancestors 'none'
```

**Why `style-src` allows `'unsafe-inline'`:** the build pipeline (Vite + Tailwind) injects
styles as inline `<style>` elements at runtime, which a strict `style-src 'self'` would
block. The relaxation is therefore confined to **styling only**.

**Script execution stays locked down.** Note what is *not* relaxed:

- `script-src 'self'` — no `'unsafe-inline'` and no `'unsafe-eval'` for scripts.
- `object-src 'none'`, `base-uri 'self'`, `frame-ancestors 'none'`.
- `default-src 'self'` — everything not otherwise listed defaults to same-origin.
- `connect-src` is limited to `'self'` and the Tauri IPC origin (no outbound network).

(`tauri.conf.json` is strict JSON and cannot carry an inline comment, which is why this
rationale lives here.)

## Reporting a vulnerability

Please report security issues **privately** — do not open a public issue.

Use GitHub's **private vulnerability reporting**: on the repository, go to the
**Security** tab → **Report a vulnerability** (GitHub Security Advisories). This opens a
private channel with the maintainer. We aim to acknowledge reports promptly and will
coordinate disclosure once a fix is available.
