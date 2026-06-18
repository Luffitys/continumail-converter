// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Progress;
using Mail2Pst.Core.Reporting;
using Mail2Pst.Core.Writing;
using Xunit;

namespace Mail2Pst.Core.Tests.Writing;

public class ProgressThrottlerTests
{
    private static readonly SourceReference Src = new() { SourcePath = "a.mbox", Identifier = "#1" };

    [Fact]
    public void Emit_NullCallback_DoesNotThrow()
    {
        var t = new ProgressThrottler(null, totalMessages: 5);
        t.Emit(new ConversionReport(), "a.mbox", "Inbox", 0); // must be a no-op, no throw
    }

    [Fact]
    public void Emit_FirstCall_AlwaysEmits()
    {
        var events = new List<ProgressEvent>();
        var t = new ProgressThrottler(e => events.Add((ProgressEvent)e), 5);
        t.Emit(new ConversionReport(), "a.mbox", "Inbox", 0);
        Assert.Single(events);
    }

    [Fact]
    public void Emit_UnchangedSnapshot_Suppresses()
    {
        var events = new List<ProgressEvent>();
        var report = new ConversionReport();
        report.RecordConverted();
        var t = new ProgressThrottler(e => events.Add((ProgressEvent)e), 5);
        t.Emit(report, "a.mbox", "Inbox", 100);
        t.Emit(report, "a.mbox", "Inbox", 100); // identical -> suppressed
        Assert.Single(events);
    }

    [Fact]
    public void Emit_EachDedupeField_TriggersEmit()
    {
        var events = new List<ProgressEvent>();
        var report = new ConversionReport();
        var t = new ProgressThrottler(e => events.Add((ProgressEvent)e), 5);
        t.Emit(report, "a", "f", 0);            // 1: first
        report.RecordConverted();
        t.Emit(report, "a", "f", 0);            // 2: converted changed
        report.RecordSkipped(Src, "x");
        t.Emit(report, "a", "f", 0);            // 3: skipped changed
        report.RecordWarning(Src, "w");
        t.Emit(report, "a", "f", 0);            // 4: warnings changed
        t.Emit(report, "a", "f", 50);           // 5: bytes changed
        Assert.Equal(5, events.Count);
    }

    [Fact]
    public void Emit_SourceOrFolderChangeAlone_DoesNotEmit()
    {
        // Matches the original EmitProgress dedupe: source/folder are NOT dedupe keys.
        var events = new List<ProgressEvent>();
        var report = new ConversionReport();
        var t = new ProgressThrottler(e => events.Add((ProgressEvent)e), 5);
        t.Emit(report, "a", "f1", 0);           // 1: first
        t.Emit(report, "b", "f2", 0);           // counts+bytes unchanged -> suppressed
        Assert.Single(events);
    }

    [Fact]
    public void Emit_PassesThroughSourceFolderBytesTotal()
    {
        var events = new List<ProgressEvent>();
        var t = new ProgressThrottler(e => events.Add((ProgressEvent)e), totalMessages: 7);
        t.Emit(new ConversionReport(), "src.mbox", "Archive", 1234);
        var ev = Assert.Single(events);
        Assert.Equal("src.mbox", ev.CurrentSource);
        Assert.Equal("Archive", ev.CurrentFolder);
        Assert.Equal(1234, ev.EstimatedOutputBytes);
        Assert.Equal(7, ev.TotalMessages);
    }
}
