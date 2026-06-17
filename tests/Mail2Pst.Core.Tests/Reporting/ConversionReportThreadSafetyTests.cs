// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Reporting;
using Xunit;

namespace Mail2Pst.Core.Tests.Reporting;

public class ConversionReportThreadSafetyTests
{
    private static readonly SourceReference Source = new() { SourcePath = "x.mbox", Identifier = "msg" };

    // Reproduces the race: one thread records warnings/skips (mutating the
    // backing lists) while another reads the report (ToJson / enumeration).
    // With unlocked reads this throws "Collection was modified"; the report
    // must expose locked snapshots so concurrent reads never throw.
    //
    // The writer is bounded (10 000 iterations) rather than running until
    // cancellation. An unbounded loop adds entries faster than the reader can
    // finish its 3 000 iterations on fast hardware, ballooning the backing
    // lists into the millions and exhausting memory (OOM) before the assert
    // is ever reached. 10 000 records is enough to overlap the reader at the
    // start of the run and catch any unsynchronized-enumeration regression,
    // while keeping peak memory negligible.
    [Fact]
    public void ConcurrentRecordAndRead_DoesNotThrow()
    {
        var report = new ConversionReport();
        using var cts = new CancellationTokenSource();
        Exception? captured = null;

        var writer = Task.Run(() =>
        {
            // Bounded producer: enough records to overlap the reader and trigger
            // any unsynchronized-enumeration regression, without growing the
            // backing lists unboundedly (which OOMs on fast machines as ToJson
            // snapshots/serializes an ever-larger list).
            for (int n = 0; n < 10_000 && !cts.IsCancellationRequested; n++)
            {
                report.RecordWarning(Source, "w");
                report.RecordSkipped(Source, "s");
            }
        });

        try
        {
            for (int i = 0; i < 3000; i++)
            {
                try
                {
                    _ = report.ToJson();
                    _ = report.ToSummary();
                    foreach (var _ in report.Warnings) { }
                    foreach (var _ in report.Skipped) { }
                }
                catch (Exception ex)
                {
                    captured = ex;
                    break;
                }
            }
        }
        finally
        {
            cts.Cancel();
            writer.Wait();
        }

        Assert.Null(captured);
    }
}
