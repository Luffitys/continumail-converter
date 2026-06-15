// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
namespace Mbox2Pst.Core.Scanning;

/// <summary>Advisory byte-progress for a scan: cumulative bytes read across all
/// sources so far (clamped to total), and the total bytes to scan (sum of all
/// source file sizes). A pure side-channel — <see cref="ScanReport"/> is
/// unaffected.</summary>
public sealed record ScanProgress(long Bytes, long TotalBytes);
