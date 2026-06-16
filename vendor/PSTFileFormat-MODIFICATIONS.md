# Local modifications to PSTFileFormat

This project vendors [`ROM-Knowledgeware/PSTFileFormat`](https://github.com/ROM-Knowledgeware/PSTFileFormat)
(upstream unmaintained since 2019), licensed under the GNU Lesser General Public License v3
(see `vendor/LICENSE-PSTFileFormat.txt`). The component remains LGPLv3; the complete corresponding
source — including the modifications listed below — is in `vendor/PSTFileFormat/`. Users may modify
that component and rebuild the application from source.

## Modifications (ContinuMail Converter, 2026)

- Added a missing property ID (`PidTagNativeBody`) used to tag HTML native bodies.
- Added allocation-map free-space helpers and caching to improve PST write throughput.
- Write correct To/Cc/Bcc recipient types and grouped display columns on written messages.
- Gmail-Takeout fidelity tweaks: NDR (non-delivery-report) suppression, unread-count handling,
  and locale handling.

See the project git history (`git log -- vendor/PSTFileFormat`) for the exact commits.
