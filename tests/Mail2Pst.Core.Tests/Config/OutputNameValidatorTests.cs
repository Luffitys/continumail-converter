// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using Mail2Pst.Core.Config;
using Xunit;

namespace Mail2Pst.Core.Tests.Config;

public class OutputNameValidatorTests
{
    [Theory]
    [InlineData("Personal")]
    [InlineData("Gmail-Takeout")]
    [InlineData("My Archive 2024")]
    [InlineData("ContinuMail_Corpus")]
    [InlineData("Archive v1.2")]   // interior dot + space are fine
    [InlineData("My.Backup")]      // interior dot is fine
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
    [InlineData("report ")]        // trailing space (Windows strips it)
    [InlineData(" report")]        // leading space
    [InlineData("report.")]        // trailing period (Windows strips it)
    [InlineData(".report")]        // leading period
    [InlineData("CON.backup")]     // reserved device name + extension
    [InlineData("con.txt")]        // reserved (case-insensitive) + extension
    [InlineData("LPT9.log")]       // reserved + extension
    public void Validate_InvalidName_Throws(string name)
    {
        Assert.ThrowsAny<System.Exception>(() => OutputNameValidator.Validate(name));
    }

    [Theory]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData(':')]
    [InlineData('"')]
    [InlineData('/')]
    [InlineData('\\')]
    [InlineData('|')]
    [InlineData('?')]
    [InlineData('*')]
    [InlineData('\t')]   // a control char (0x09)
    [InlineData('\0')]   // null — also a control char (0x00)
    public void Validate_WindowsInvalidCharacter_Throws(char invalid)
    {
        Assert.ThrowsAny<System.Exception>(() => OutputNameValidator.Validate($"a{invalid}b"));
    }
}
