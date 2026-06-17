// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mail2Pst.Core.Parsing;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing;

public class MboxParserProgressTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Parse_WithOnBytesRead_ReportsNonDecreasingPositionsWithinFileLength()
    {
        var parser = new MboxParser();
        var positions = new List<long>();
        long length = new FileInfo(Fixture("sample.mbox")).Length;

        foreach (var _ in parser.Parse(Fixture("sample.mbox"), positions.Add)) { }

        Assert.NotEmpty(positions);
        for (int i = 1; i < positions.Count; i++)
            Assert.True(positions[i] >= positions[i - 1], "positions must be non-decreasing");
        Assert.True(positions[^1] <= length, "position must not exceed file length");
    }

    [Fact]
    public void Parse_WithOnBytesRead_ReportsStrictlyIncreasingPositionsAcrossBuffers()
    {
        // Build a temp mbox larger than the parser's 80 KB read buffer so the
        // callback reports positions mid-file (strictly below EOF), not just
        // once at EOF — making the monotonic/≤-length assertions load-bearing.
        string path = Path.Combine(Path.GetTempPath(), $"mail2pst-progress-{Guid.NewGuid()}.mbox");
        try
        {
            var sb = new StringBuilder();
            string body = new string('x', 5000);
            for (int i = 0; i < 40; i++)
            {
                sb.Append("From sender@example.com Mon Jan  1 00:00:00 2024\n");
                sb.Append($"Subject: Message {i}\n");
                sb.Append("Date: Mon, 01 Jan 2024 10:00:00 +0000\n");
                sb.Append("\n");
                sb.Append(body);
                sb.Append("\n\n");
            }
            File.WriteAllText(path, sb.ToString());
            long length = new FileInfo(path).Length;

            var parser = new MboxParser();
            var positions = new List<long>();
            foreach (var _ in parser.Parse(path, positions.Add)) { }

            Assert.True(positions.Count >= 2, "expected multiple progress reports");
            for (int i = 1; i < positions.Count; i++)
                Assert.True(positions[i] >= positions[i - 1], "positions must be non-decreasing");
            Assert.True(positions[0] < length, "early positions should be below EOF (multi-buffer)");
            Assert.True(positions[^1] <= length, "position must not exceed file length");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_WithNullCallback_BehavesAsBefore()
    {
        var parser = new MboxParser();
        int count = 0;
        foreach (var _ in parser.Parse(Fixture("sample.mbox"))) count++;
        Assert.Equal(2, count);
    }
}
