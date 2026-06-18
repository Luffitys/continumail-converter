// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using Mail2Pst.Core.Progress;
using Mail2Pst.Core.Reporting;

namespace Mail2Pst.Core.Writing;

/// <summary>
/// De-duplicates progress emission for one WritePlan call: only invokes the callback
/// when the converted/skipped/warning counts or the estimated byte total have changed
/// since the last emit. Behaviour is a 1:1 extraction of PstWriter.WritePlan's former
/// EmitProgress local function. CurrentSource/CurrentFolder are passed through but are
/// deliberately NOT dedupe keys (matching the original).
/// </summary>
internal sealed class ProgressThrottler
{
    private readonly Action<ConversionProgressEvent>? _onProgress;
    private readonly int _totalMessages;

    private int _lastConverted = -1;
    private int _lastSkipped = -1;
    private int _lastWarnings = -1;
    private long _lastBytes = -1;

    public ProgressThrottler(Action<ConversionProgressEvent>? onProgress, int totalMessages)
    {
        _onProgress = onProgress;
        _totalMessages = totalMessages;
    }

    public void Emit(ConversionReport report, string? currentSource, string? currentFolder, long estimatedOutputBytes)
    {
        if (_onProgress is null) return;
        if (report.ConvertedCount == _lastConverted
            && report.SkippedCount == _lastSkipped
            && report.WarningCount == _lastWarnings
            && estimatedOutputBytes == _lastBytes)
        {
            return;
        }
        _lastConverted = report.ConvertedCount;
        _lastSkipped = report.SkippedCount;
        _lastWarnings = report.WarningCount;
        _lastBytes = estimatedOutputBytes;
        _onProgress(new ProgressEvent(
            report.ConvertedCount, _totalMessages, report.WarningCount, report.SkippedCount,
            currentSource, currentFolder, estimatedOutputBytes));
    }
}
