// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Parsing;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing;

public class MboxParserDateTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Parse_MessageWithoutDateHeader_LeavesDateNull()
    {
        IMailSourceParser parser = ParserRegistry.Get("mbox");
        ParseResult result = parser.Parse(Fixture("mbox-no-dates.mbox")).Single();

        Assert.True(result.Success);
        Assert.Null(result.Message!.Date);
    }

    [Fact]
    public void Parse_MessageWithDateHeader_PopulatesDate()
    {
        IMailSourceParser parser = ParserRegistry.Get("mbox");
        ParseResult result = parser.Parse(Fixture("sample.mbox")).First();

        Assert.True(result.Success);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), result.Message!.Date);
    }

    // A present-but-unparseable Date header makes MimeKit return MinValue
    // (0001-01-01). That must be treated as "no date" (null), exactly like a
    // missing header, or it poisons scan date ranges (e.g. dateFrom shows
    // 0001-01-01 for a whole mbox because of one bad message).
    [Fact]
    public void Parse_MessageWithUnparseableDateHeader_LeavesDateNull()
    {
        IMailSourceParser parser = ParserRegistry.Get("mbox");
        ParseResult result = parser.Parse(Fixture("mbox-bad-date.mbox")).Single();

        Assert.True(result.Success);
        Assert.Null(result.Message!.Date);
    }
}
