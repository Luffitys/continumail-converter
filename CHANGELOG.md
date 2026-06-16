# Changelog

All notable changes to ContinuMail Converter are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] — 2026-06-16

A post-launch correctness and robustness release. The headline is a **data-loss fix**:
some mbox files could silently merge messages. Upgrading is recommended.

### Fixed
- **Silent message loss on mbox files without blank separators.** A message boundary was
  previously only detected after a blank line, so two messages joined without one were merged
  into a single message — losing the second. Boundary detection now also recognizes the
  envelope ("From ") postmark line by its asctime shape. Validated against a real corpus of
  45,000+ messages with zero over-splitting.
- **Body corruption on mboxrd archives (e.g. Gmail Takeout).** Body lines beginning with
  `From ` are stored escaped as `>From `; these are now un-escaped so message text round-trips
  exactly.
- **Desktop app could hang at the end of a conversion.** The terminal `done` event could be
  dropped when the engine exited quickly; the convert output is now buffered with an exit
  fallback so the UI always finishes.

### Changed
- **Write failures are no longer silently skipped.** A PST write error — an engine bug, a
  vendored-library failure, or invalid internal state — is now **fatal**: the converter stops
  and reports it instead of producing a quietly-incomplete PST. A failed run deletes its
  in-progress PST part rather than leaving a half-written file that looks usable. (Malformed
  *input* is still skipped/warned, as before.)
- **Folder names are now validated by the engine,** not only the desktop UI — a hand-written
  config or direct CLI use can no longer place an unsafe folder name into the PST.
- **PST size-splitting no longer overshoots the cap.** When an output PST is split by size, the
  writer now splits *predictively* — before a message would cross the configured maximum — rather
  than only checking at a periodic interval (which could overshoot a small custom split size by up
  to ~500 messages).
- **Review screen:** sort the folder list by clicking column headers (▲/▼ for direction); a note
  on the Options screen clarifies that renaming sets a folder's name, not Outlook's display order.
- Documentation now describes large-attachment spill-to-disk accurately as bounding *queued*
  memory rather than implying full end-to-end streaming.

### Security
- **Upgraded MimeKit 4.9.0 → 4.17.0,** clearing the moderate `GHSA-g7hc-96xr-gvvx` (`NU1902`)
  advisory.
- **Added a restrictive Content-Security-Policy to the desktop app** (previously unset): it is
  limited to its own bundled assets and local IPC — no external network origins.

### Unchanged
- The CLI JSON Lines event contract (`scan` / `convert` / `version`) is unchanged.

## [0.1.0] — 2026-06-16

Initial public release. Convert mbox mail archives into Outlook PST files **without Outlook or
any Microsoft library installed** — a CLI engine plus a desktop GUI (Windows).

- mbox input (eml/msg deferred to a later release).
- Mirror (one folder per source file) or flatten folder mapping; size-based PST splitting.
- Metadata fidelity: To/Cc/Bcc, read/unread, importance, Message-ID/threading, HTML + plain-text
  bodies, attachments (inline CID handled).
- Free and open source (GPL-3.0-or-later); bundles the LGPLv3 PSTFileFormat reader/writer.
