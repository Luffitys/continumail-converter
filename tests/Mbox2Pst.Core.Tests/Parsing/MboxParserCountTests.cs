// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using Mbox2Pst.Core.Parsing;
using Xunit;

namespace Mbox2Pst.Core.Tests.Parsing;

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
    public void CountMessages_FileWithNoTrailingNewline_MatchesParseCount()
    {
        // An mbox where the last header line has no trailing \n — CountMessages
        // must agree with Parse().Count() on the total.
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
            int countResult = parser.CountMessages(path);
            int parseResult = parser.Parse(path).Count();
            Assert.Equal(parseResult, countResult);
        }
        finally { File.Delete(path); }
    }
}
