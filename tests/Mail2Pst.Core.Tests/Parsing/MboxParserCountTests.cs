// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Parsing;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing;

public class MboxParserCountTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void CountMessages_SampleMbox_Returns2()
    {
        var parser = new MboxParser();
        Assert.Equal(2, parser.CountMessages(Fixture("sample.mbox")));
    }

    [Fact]
    public void CountMessages_EofBugMbox_Returns2()
    {
        var parser = new MboxParser();
        Assert.Equal(2, parser.CountMessages(Fixture("mbox-eof-bug.mbox")));
    }

    [Fact]
    public void CountMessages_WithAttachmentsMbox_Returns6()
    {
        var parser = new MboxParser();
        Assert.Equal(6, parser.CountMessages(Fixture("mbox-with-attachments.mbox")));
    }

    [Fact]
    public void CountMessages_MissingFile_ThrowsFileNotFoundException()
    {
        var parser = new MboxParser();
        Assert.Throws<FileNotFoundException>(() => parser.CountMessages("does-not-exist.mbox"));
    }

    [Fact]
    public void CountMessages_EmptyFile_Returns0()
    {
        string path = Path.GetTempFileName();
        try
        {
            // empty temp file
            var parser = new MboxParser();
            Assert.Equal(0, parser.CountMessages(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CountMessages_FileWithNoTrailingNewline_Returns2()
    {
        // An mbox whose last header line has no trailing \n. There are exactly TWO
        // messages — assert that literal, not CountMessages()==Parse().Count() (two
        // values from the same boundary rule, which a shared bug like #0 would pass).
        string content =
            "From alice@example.com Mon Jan  1 00:00:00 2024\r\n" +
            "Subject: Test\r\n" +
            "\r\n" +
            "Body.\r\n" +
            "\r\n" +
            "From bob@example.com Mon Jan  1 00:00:01 2024\r\n" +
            "Subject: Test2\r\n" +
            "\r\n" +
            "Body2."; // no trailing \n

        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content, System.Text.Encoding.ASCII);
            var parser = new MboxParser();
            Assert.Equal(2, parser.CountMessages(path));
            Assert.Equal(2, parser.Parse(path).Count());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CountMessages_TrailingBarePostmark_CountsAsSplit_NotPlusOne()
    {
        // A file ending in a bare "From " postmark with no following content. SplitMessages
        // yields 1 (no empty trailing message); the OLD CountBoundaries counted the trailing
        // boundary LINE too and returned 2 — the drift this refactor removes. Both become 1.
        string content =
            "From real@example.com Mon Jan  1 00:00:00 2024\r\n" +
            "Subject: Real\r\n" +
            "\r\n" +
            "Body.\r\n" +
            "\r\n" +
            "From trailing@example.com Mon Jan  1 00:00:01 2024\r\n"; // bare postmark, no body
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content, System.Text.Encoding.ASCII);
            var parser = new MboxParser();
            Assert.Equal(1, parser.CountMessages(path));        // literal anchor (was 2 before)
            Assert.Single(parser.Parse(path));                  // split yields exactly 1 -> parity with the count
        }
        finally { File.Delete(path); }
    }
}
