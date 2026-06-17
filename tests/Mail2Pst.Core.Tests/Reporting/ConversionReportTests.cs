// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Reporting;
using Xunit;

namespace Mail2Pst.Core.Tests.Reporting;

public class ConversionReportTests
{
    [Fact]
    public void RecordConverted_IncrementsConvertedCount()
    {
        var report = new ConversionReport();

        report.RecordConverted();
        report.RecordConverted();

        Assert.Equal(2, report.ConvertedCount);
        Assert.Empty(report.Skipped);
    }

    [Fact]
    public void RecordSkipped_AddsSkippedEntryAndAppearsInSummary()
    {
        var report = new ConversionReport();
        var source = new SourceReference { SourcePath = "Inbox.mbox", Identifier = "message #3" };

        report.RecordSkipped(source, "unsupported encoding");

        Assert.Single(report.Skipped);
        Assert.Equal("Inbox.mbox", report.Skipped[0].SourcePath);
        Assert.Equal("message #3", report.Skipped[0].Identifier);
        Assert.Equal("unsupported encoding", report.Skipped[0].Reason);

        string summary = report.ToSummary();
        Assert.Contains("Converted: 0", summary);
        Assert.Contains("Skipped: 1", summary);
        Assert.Contains("Inbox.mbox", summary);
        Assert.Contains("unsupported encoding", summary);
    }

    [Fact]
    public void RecordWarning_AddsWarningEntryAndAppearsInSummary()
    {
        var report = new ConversionReport();
        var source = new SourceReference { SourcePath = "Inbox.mbox", Identifier = "message #4" };

        report.RecordWarning(source, "Dropped attachment #2 'bad.bin' (application/octet-stream): boom");

        Assert.Single(report.Warnings);
        Assert.Equal("Inbox.mbox", report.Warnings[0].SourcePath);
        Assert.Equal("message #4", report.Warnings[0].Identifier);
        Assert.Equal("Dropped attachment #2 'bad.bin' (application/octet-stream): boom", report.Warnings[0].Reason);

        string summary = report.ToSummary();
        Assert.Contains("Warnings: 1", summary);
        Assert.Contains("Inbox.mbox", summary);
        Assert.Contains("Dropped attachment #2", summary);
    }

    [Fact]
    public void NewReport_IsNotCancelled_AndHasNoDeletedFiles()
    {
        var report = new ConversionReport();
        Assert.False(report.Cancelled);
        Assert.Empty(report.DeletedFiles);
    }

    [Fact]
    public void MarkCancelled_SetsCancelledTrue()
    {
        var report = new ConversionReport();
        report.MarkCancelled();
        Assert.True(report.Cancelled);
    }

    [Fact]
    public void RecordDeletedFile_AppearsInDeletedFilesSnapshot()
    {
        var report = new ConversionReport();
        report.RecordDeletedFile(@"C:\out\Archive-2.pst");
        Assert.Equal(new[] { @"C:\out\Archive-2.pst" }, report.DeletedFiles.ToArray());
    }
}
