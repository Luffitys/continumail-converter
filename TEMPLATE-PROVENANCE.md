# Provenance of `assets/template.pst`

ContinuMail Converter ships a small checked-in binary, `assets/template.pst`. This
document explains what it is, how it was made, why it is committed, and how to verify it.

## What it is

A **blank Unicode PST** (`wVer=23`) — an empty Outlook personal-store file with no
messages. It is the **write seed**: for every conversion, `PstWriter` copies this file and
writes the converted messages into the copy. The converter never modifies your input.

| Property | Value |
|----------|-------|
| Size | 271,360 bytes |
| SHA-256 | `e046dc3a0624e8e9f5861962a89e550243b9d6fe2d05d63446a5361a8a294a4a` |

## Why a copy of a template, instead of creating a PST from scratch?

The converter writes PST without Outlook by vendoring
[PSTFileFormat](https://github.com/ROM-Knowledgeware/PSTFileFormat) (LGPLv3). That library
is a genuine from-scratch MS-PST reader/writer, but it can only **open** an existing PST —
it cannot create a new, valid empty store. So the project ships one blank store and writes
into a copy of it.

## How it was generated

One-time, with Microsoft Outlook installed, using the dev-only tool in
[`tools/template-gen/`](tools/template-gen/) (not part of the shipped application):

1. Outlook is driven via COM to create a **new Unicode (`.pst`) data file**.
2. The file is created empty (no messages, default top-of-store folders only) and closed
   cleanly so the on-disk store is consistent.
3. The resulting blank store is saved as `assets/template.pst` and committed.

Because generation requires Outlook COM and CI has no Outlook, the template is **not**
regenerated in CI — it is a committed artifact. This document plus the guard test below are
how its integrity is maintained instead.

## How to verify it

```powershell
Get-FileHash -Algorithm SHA256 assets\template.pst
```

```bash
sha256sum assets/template.pst
```

The output must equal the SHA-256 above.

## Integrity guard (self-policing)

The test `tests/Mail2Pst.Core.Tests/Assets/TemplateProvenanceTests.cs` asserts that both
the on-disk `assets/template.pst` **and** the copy embedded in the shipped `Mail2Pst.Core`
assembly match the size and SHA-256 published here. If the template is ever regenerated,
that test fails until the constants in it and the values in this document are updated
together — so the published hash cannot silently go stale.
