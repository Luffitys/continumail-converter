// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using Mbox2Pst.Core.Parsing;
using Xunit;

namespace Mbox2Pst.Core.Tests.Parsing;

public class ParserRegistryTests
{
    [Theory]
    [InlineData("mbox")]
    [InlineData("MBOX")]
    public void Get_KnownType_ReturnsParser(string type)
    {
        IMailSourceParser parser = ParserRegistry.Get(type);

        Assert.NotNull(parser);
        Assert.IsType<MboxParser>(parser);
    }

    [Fact]
    public void Get_UnknownType_Throws()
    {
        Assert.Throws<NotSupportedException>(() => ParserRegistry.Get("maildir"));
    }
}
