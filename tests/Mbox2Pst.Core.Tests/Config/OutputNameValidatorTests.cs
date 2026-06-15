// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using Mbox2Pst.Core.Config;
using Xunit;

namespace Mbox2Pst.Core.Tests.Config;

public class OutputNameValidatorTests
{
    [Theory]
    [InlineData("Personal")]
    [InlineData("Gmail-Takeout")]
    [InlineData("My Archive 2024")]
    [InlineData("ContinuMail_Corpus")]
    public void Validate_ValidName_DoesNotThrow(string name)
    {
        OutputNameValidator.Validate(name); // should not throw
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]            // forward separator
    [InlineData("a\\b")]           // backslash separator
    [InlineData("../evil")]        // path traversal
    [InlineData("C:\\abs")]        // rooted / drive
    [InlineData("a:b")]            // colon
    [InlineData("a*b")]            // wildcard
    [InlineData("a?b")]
    [InlineData("a|b")]
    [InlineData("CON")]            // reserved device name
    [InlineData("nul")]           // reserved (case-insensitive)
    [InlineData("LPT1")]
    public void Validate_InvalidName_Throws(string name)
    {
        Assert.ThrowsAny<System.Exception>(() => OutputNameValidator.Validate(name));
    }
}
