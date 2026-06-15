// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Mbox2Pst.Core.Models;
using Mbox2Pst.Core.Parsing;
using Xunit;

namespace Mbox2Pst.Core.Tests.Parsing;

public class ParseResultTests
{
    [Fact]
    public void Ok_WithoutWarnings_HasEmptyWarningsList()
    {
        var message = new MailMessage { Source = new SourceReference { SourcePath = "a.mbox", Identifier = "message #1" } };

        ParseResult result = ParseResult.Ok(message);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Ok_WithWarnings_ExposesThem()
    {
        var message = new MailMessage { Source = new SourceReference { SourcePath = "a.mbox", Identifier = "message #1" } };
        var warnings = new List<string> { "Dropped attachment #1 'bad.bin' (application/octet-stream): boom" };

        ParseResult result = ParseResult.Ok(message, warnings);

        Assert.True(result.Success);
        Assert.Equal(warnings, result.Warnings);
    }
}
