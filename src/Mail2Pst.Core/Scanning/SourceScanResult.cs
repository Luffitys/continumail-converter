// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;

namespace Mail2Pst.Core.Scanning;

/// <summary>One row of a <see cref="ScanReport"/>: the per-source breakdown for a
/// single input file.</summary>
public sealed record SourceScanResult(
    string Id,
    string Path,
    string DisplayName,
    int Messages,
    long EstimatedBytes,
    long SourceBytes,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int Warnings,
    int Skipped);

/// <summary>Run-wide totals across every source in a <see cref="ScanReport"/>.</summary>
public sealed record ScanTotals(int Messages, long Bytes, long SourceBytes, int Sources);
