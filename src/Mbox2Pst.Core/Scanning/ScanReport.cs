// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using Mbox2Pst.Core.Reporting;

namespace Mbox2Pst.Core.Scanning;

public sealed record ScanReport(
    ScanTotals Totals,
    IReadOnlyList<SourceScanResult> Sources,
    IReadOnlyList<SkippedMessage> Skipped,
    IReadOnlyList<SkippedMessage> Warnings);
