# Contributing to ContinuMail Converter

Thanks for your interest in improving ContinuMail Converter. This document explains
how contributions are licensed. Please read it before opening a pull request.

## License of contributions

ContinuMail Converter is released under **GPL-3.0-or-later**. Your contributions
are accepted on those terms — but because ContinuMail follows an **open-core**
model (this converter is free and open-source forever, while separate paid
products fund its development), we also ask contributors to sign a lightweight
**Contributor License Agreement (CLA)** before their first contribution is merged.

### Why a CLA?

The CLA lets the project:

- keep distributing the converter under the GPL to everyone, **and**
- reuse the project's own code in ContinuMail's separate proprietary products.

Without it, a single externally-authored line under the GPL could not be reused in
those products, which would break the open-core model. The CLA does **not** take
away your rights: you keep full ownership and copyright of your contribution and
can use it however you like elsewhere.

### What you agree to

By submitting a contribution (a pull request, patch, or other change) you confirm:

1. **Original work** — the contribution is your own work, or you have the right to
   submit it, and to your knowledge it does not infringe anyone's rights.
2. **License grant** — you license your contribution to the project under
   **GPL-3.0-or-later** for public distribution.
3. **Relicensing grant** — you additionally grant Aksel Visby (ContinuMail) a
   perpetual, worldwide, royalty-free, irrevocable right to use, modify, and
   **relicense** your contribution, including under proprietary licenses, as part
   of ContinuMail products. You retain copyright and all other rights.

The full agreement is in [`CLA.md`](CLA.md) (an Individual CLA, governed by Danish law).

### How acceptance works

You accept the CLA **electronically** when you open your first pull request: an automated
assistant requests your acceptance and records it against your GitHub account. Your pull
request is merged only after acceptance. You keep full ownership of your contribution —
the CLA grants the Project the rights described above, it does not transfer your copyright.
Contributors who prefer may instead complete and return the signed signature block in
[`CLA.md`](CLA.md).

## How to contribute (practical)

- Discuss non-trivial changes in an issue first.
- Keep pull requests focused; one logical change per PR.
- Follow the existing code style and add tests where the project has them
  (`dotnet test mbox2pst.sln` for the engine, `cd desktop && npm test` for the GUI).
- Do not add dependencies under licenses incompatible with GPL-3.0.

## Trademarks

Contributing code does not grant any rights to the ContinuMail name, logos, or
brand assets — see [`TRADEMARKS.md`](TRADEMARKS.md).
