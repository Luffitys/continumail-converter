# ContinuMail Converter

**Convert Gmail Takeout and other mbox mail archives into Outlook PST files —
locally, with no upload and no Outlook installation required.**

ContinuMail Converter is a free, open-source, **local-first** email conversion tool
for Windows. Your mail never leaves your computer: it reads `.mbox` archives on disk
and writes standard Outlook `.pst` files you can open in Outlook or import into
Microsoft 365 — without driving Outlook via COM automation and without any Microsoft
libraries installed.

Part of **ContinuMail** — a family of practical, honest mail tools.

> **Status: early release (0.x).** The desktop app and the conversion engine are
> covered by a comprehensive automated test suite (engine + desktop unit tests). It has been
> validated on real Gmail Takeout and Thunderbird/Exchange exports. As an early
> release, please keep backups and validate output before relying on it.

![ContinuMail Converter — the Options screen: choose folder mapping and split size, with a live preview of the resulting PST folder tree](media/options.png)

## Download & install

1. Download the latest installer from the [**Releases**](../../releases/latest) page:
   `ContinuMail Converter_<version>_x64-setup.exe`.
2. Run it. ContinuMail installs for the current user (no admin required) and adds a
   Start-menu shortcut. Windows 10/11, 64-bit.

> **"Windows protected your PC"?** This early release is **not yet code-signed**, so
> Windows SmartScreen may warn about an "unknown publisher." If you trust this source,
> click **More info → Run anyway**. (Code signing is planned — see [Where this is going](#where-this-is-going).)

Prefer the command line or automating conversions? See
[Command-line interface](#command-line-interface) below.

## Why this exists

*A note from the maintainer.*

I've spent years as an MSP technician running email migrations — hundreds of them.
Time and again I'd show up to a client and find the same kind of mess: years of mail
stranded in an old ISP mailbox, an ancient desktop client, a Thunderbird that's been
pulling POP3 since who-knows-when. And every time, the same sinking question — *how is
this ever going to make it to something modern like Microsoft 365?*

Before I started this, I assumed converting email was a solved, trivial problem. It
isn't. mbox → PST in particular is genuinely hard to get right, and most of the tools
that do it are paid, closed-source, and charge real money for what should be a basic
capability. I needed this tool, for real, on multiple jobs — so I built it, and I'm
giving it away.

We didn't reinvent the wheel — ContinuMail Converter stands on excellent existing work
(see [Credits](#credits)). What we added is a free, open, local-first tool that handles
the messy, real-world archives I kept running into, and puts it in anyone's hands.

**It's free, and it stays free.**

## What makes it different

- **No Outlook, no COM automation.** Among free and open-source options, writing PST
  almost always means remote-controlling a running copy of Outlook (COM automation): you
  need Outlook installed and licensed, and the conversion *takes over the application* —
  Outlook is locked for the entire run (often hours), so you can't use your mail while it
  works. The few free tools that avoid Outlook can only *read* PST, not write it.
  ContinuMail is **100% independent of Outlook** — it writes PST directly using a
  vendored, genuinely from-scratch PST engine: the excellent
  [PSTFileFormat](https://github.com/ROM-Knowledgeware/PSTFileFormat) by **ROM
  Knowledgeware** (see [Credits](#credits)). Nothing to install, nothing hijacked, and you
  can keep working while it runs.
- **Private / local-first.** No archive upload; makes no network connections.
  Your email stays on your machine.
- **Gmail Takeout–aware.** Built and validated against real Google Takeout exports,
  including their quirks (and a MimeKit mbox-parsing bug that truncates some
  archives, which this converter works around).
- **Faithful.** Preserves folder structure, attachments (including inline images
  and embedded `.eml` messages), HTML bodies, dates, sender, To/Cc/Bcc,
  read/unread state, importance, and threading headers.

## Using the app

The desktop app walks you through the whole conversion:

1. **Source** — pick `.mbox` files or a folder of them.
2. **Scanning** — a fast dry-run counts messages and estimates output size (with a
   live progress bar) before anything is written.
3. **Review** — see per-folder counts and choose whether to include empty folders.
4. **Options** — pick folder mapping (mirror or flatten), set a split size, and
   preview the resulting PST folder tree; rename folders if you like.
5. **Convert** — watch live progress (count, MB/s, ETA); cancel any time.
6. **Done** — open the output folder, or review any warnings.

Your originals are never modified — the tool only reads them.

## Features

- mbox input via a custom mbox splitter plus MimeKit entity parsing.
- Folder mapping: mirror (one PST folder per source file), flatten, or per-source
  custom target folder.
- One PST per output group, with size-based auto-splitting into parts.
- HTML bodies (`PidTagHtml` + `PidTagNativeBody`) with a plain-text fallback.
- Full attachment support: regular files, inline CID images (correctly hidden so
  they don't show a phantom paperclip), and embedded `.eml` messages.
- Metadata fidelity: To/Cc/Bcc recipient types, read/unread, importance/priority,
  Message-ID / In-Reply-To / References, conversation topic.
- Large attachments (≥ 4 MB) spill to a temp file so many of them don't pile up in memory at once.
- Conversion reports (human-readable + JSON) listing skipped messages and warnings.
- Empty source folders become empty PST folders (optional).

## Command-line interface

The same engine is available as a CLI (the desktop app bundles and drives it). Useful
for automation, scripting, or large batch jobs. It uses subcommands:

```bash
# Dry-run: per-source breakdown (counts, sizes, date range), writing nothing.
# --input is repeatable to scan several mbox files in one call.
dotnet run --project src/Mbox2Pst.Cli -- scan --input <path.mbox> [--input <path2.mbox> ...]

# Convert: JSON Lines progress to stdout, PST + reports to the output directory.
dotnet run --project src/Mbox2Pst.Cli -- convert --config <config.json> --output <output-dir>
```

A minimal working example lives in `fixtures/sample-config.json` + `fixtures/sample.mbox`.

### Configuration

A config describes one or more output groups (each becomes a PST file group):

```json
{
  "outputs": [
    {
      "name": "Personal",
      "maxSizeMB": 45000,
      "folderMapping": "mirror",
      "includeEmptyFolders": true,
      "sources": [
        { "path": "Takeout/Mail/Inbox.mbox", "type": "mbox" }
      ]
    }
  ]
}
```

- `folderMapping`: `mirror` (one PST folder per source file, named after it) or
  `flatten` (everything into one folder unless a source sets `targetFolder`).
- `maxSizeMB`: split the output into `Name-1.pst`, `Name-2.pst`, … when exceeded.
  A single un-split output is just `Name.pst`.
- `includeEmptyFolders`: keep empty source folders as empty PST folders.

### Scan output

`scan` prints a single (pretty-printed) JSON object with run-wide `totals` and a
per-source breakdown. `bytes` is the estimated PST content size; `sourceBytes` is the
raw mbox file size. `dateFrom`/`dateTo` are `null` when a source has no dated messages.

```json
{
  "type": "scan",
  "totals": { "messages": 17989, "bytes": 2007117000, "sourceBytes": 2400000000, "sources": 2 },
  "sources": [
    {
      "id": "inbox", "path": "Takeout/Mail/Inbox.mbox", "displayName": "Inbox",
      "messages": 12000, "bytes": 98765432, "sourceBytes": 123456789,
      "dateFrom": "2012-01-01T00:00:00Z", "dateTo": "2024-12-31T00:00:00Z",
      "warnings": 0, "skipped": 0
    }
  ],
  "skipped": [],
  "warnings": []
}
```

### JSON Lines progress (convert)

```json
{"type":"started","input":"config.json","outputDirectory":"out/"}
{"type":"scan","totalMessages":17989}
{"type":"progress","converted":1000,"total":17989,"warnings":2,"skipped":0,"currentSource":"Inbox.mbox","currentFolder":"Inbox"}
{"type":"warning","source":"Inbox.mbox","identifier":"message #52","reason":"Dropped attachment 'bad.bin': invalid encoding"}
{"type":"done","converted":17989,"skipped":0,"warnings":1,"outputs":["out/Personal.pst"],"outputDirectory":"out/","report":"out/conversion-report.json","elapsedMs":132000}
```

After conversion the output directory contains `conversion-report.txt` (human-readable)
and `conversion-report.json` (same data, machine-readable).

Send a line `cancel` on the process's **stdin** (or `SIGTERM` / `Ctrl+C`) to stop a
running `convert` cleanly: the engine deletes the in-progress PST part, leaves any
completed split parts on disk, and emits a terminal `cancelled` event instead of `done`.
Exit codes: **0** success, **1** fatal error, **2** cancelled.

## Build from source

### Engine + CLI

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). Windows is the primary
target (the PST engine uses some Windows APIs).

```bash
dotnet build mbox2pst.sln
dotnet test  mbox2pst.sln
```

### Desktop app

A [Tauri](https://tauri.app/) v2 + React (Vite) app that ships the CLI as a bundled
sidecar and drives it via the JSON Lines contract above. Targets Windows 10/11 and
produces an NSIS installer.

**Prerequisites:** [Node.js](https://nodejs.org/) (LTS) + npm, the
[Rust toolchain](https://rustup.rs/) with the MSVC target
(`rustup target add x86_64-pc-windows-msvc`), the
[.NET 8 SDK](https://dotnet.microsoft.com/download) (for the sidecar), and WebView2
(present on Windows 11; the installer ensures it on Windows 10).

```bash
cd desktop
npm install
npm run tauri dev     # run the app in dev mode
npm test              # frontend unit tests (Vitest)
npm run tauri build   # release build + NSIS installer
# Output: src-tauri/target/release/bundle/nsis/ContinuMail Converter_<ver>_x64-setup.exe
```

> The CLI sidecar build (`desktop/scripts/build-sidecar.ps1`) is Windows / win-x64 only,
> matching the v1 Windows-first scope. It runs automatically before `tauri dev`/`build`
> via the `pretauri` npm hook. Cross-platform sidecar builds are deferred to a future
> release.

## Privacy & data handling

ContinuMail Converter reads your mail archive locally and writes the PST locally.
It makes **no network connections** and **uploads nothing**. Your originals are only
ever read, never modified.

## Disclaimer

ContinuMail Converter is free software, provided **"as is", without warranty of any
kind** (see [`LICENSE`](LICENSE)). Email history is important — please:

- **Keep a backup** of your original mbox files before converting.
- **Validate** the output (open the PST, or import into a test Outlook profile)
  before deleting or relying on the originals.
- **Review the conversion report** for skipped messages and warnings.

You remain responsible for backups, testing, and validation before relying on
converted data.

**Unsigned installer.** This release is **not yet code-signed**, so when you run
the installer Windows SmartScreen may warn that the publisher is "unknown." This is
expected for an early open-source release — see [Download & install](#download--install)
for how to proceed. Code signing is on the roadmap.

## Where this is going

ContinuMail Converter is more than an mbox-to-PST tool — that's just where it starts.
The goal is a genuinely **free, open alternative to the paid, closed email-conversion
products** that dominate this space, eventually covering the formats real migrations
actually need.

mbox came first on purpose: in the field it's both the **most common** archive I run
into *and* the **hardest** to convert faithfully. Everything else builds out from here.

On the roadmap:

- **More formats** — EML and MSG input next, and more to follow (mbox-only today).
- **Code signing** of the installer (to remove the SmartScreen prompt).
- **Cross-platform** builds (Windows-only today).

The converter is free and open-source, and it stays that way. (ContinuMail is built on
an open-core model: this converter is free forever; any paid products are separate.)

## License

ContinuMail Converter's own code is licensed under the **GNU General Public License
v3.0 or later** (**GPL-3.0-or-later**) — see [`LICENSE`](LICENSE). You may redistribute
and modify it under those terms; derivative works must remain under the GPL and ship
their corresponding source.

It bundles third-party components under their own (GPL-compatible) licenses
(see [`NOTICE`](NOTICE)):

- **PSTFileFormat** (the MS-PST read/write engine) under **LGPLv3** — vendored under
  `vendor/PSTFileFormat/` with local modifications. The complete corresponding source
  is included, so it can be modified and the app rebuilt, per the LGPL.
- **MimeKit** under the MIT License.

The **ContinuMail** name, logos, icons, and brand assets are **not** covered by the GPL
and remain proprietary — see [`TRADEMARKS.md`](TRADEMARKS.md). Contributions are accepted
under a Contributor License Agreement — see [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Credits

- **[ROM-Knowledgeware/PSTFileFormat](https://github.com/ROM-Knowledgeware/PSTFileFormat)**
  by **Tal Aloni** — the rare, from-scratch MS-PST reader/writer that makes ContinuMail's
  Outlook-independence possible. Writing PST *without* Outlook is the genuinely hard part,
  and this library (LGPLv3) is what makes it work. **ContinuMail would not exist without
  it.** Huge thanks. (Vendored under `vendor/PSTFileFormat/` with local modifications, per
  the LGPL.)
- **[MimeKit](https://github.com/jstedfast/MimeKit)** by **Jeffrey Stedfast** — the MIME
  parsing that reads every message.

## How it works (for contributors)

Writing PST files is the hard part: `libpst`/`java-libpst` are read-only, and most
"mbox to PST" tools drive Outlook via COM (slow, requires Outlook). PSTFileFormat is a
real from-scratch writer, but it can only *open* an existing PST — so the tool ships a
small blank Unicode PST template (`assets/template.pst`) and writes into a copy of it.

Key things to know when working on the code:

- `MboxParser` does **not** use MimeKit's `MimeFormat.Mbox` parser — it splits the file
  into per-message chunks (mboxrd convention) and parses each as a MIME entity, working
  around a MimeKit bug that truncates some real archives.
- Use `parentFolder.CreateChildFolder(name, type)` (not `CreateNewFolder()`), and call
  `folder.SaveChanges()` before `file.EndSavingChanges()`.
- The vendored allocation scan was made O(1) (AMap cache + free-space index) to keep
  large PST writes fast.

### Layout

- `src/Mbox2Pst.Core/` — engine: config, mapping, parsing, scanning, reporting, PST writing.
- `src/Mbox2Pst.Cli/` — command-line entry point (`scan`, `convert`).
- `desktop/` — the Tauri + React desktop app (drives the CLI as a sidecar).
- `tests/Mbox2Pst.Core.Tests/` — xUnit coverage for the pipeline.
- `vendor/PSTFileFormat/`, `vendor/Utilities/` — vendored PSTFileFormat (LGPLv3).
- `assets/template.pst` — blank Unicode PST used as the write seed.
- `tools/template-gen/` — one-time, dev-only Outlook-COM tool to regenerate the template
  (not part of the shipped app).
