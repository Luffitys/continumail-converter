// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using Mbox2Pst.Core.Config;
using Xunit;

namespace Mbox2Pst.Core.Tests.Config;

public class FolderNameValidatorTests
{
    // NOTE: keep this table identical (case-for-case) to the parity table in
    // desktop/src/lib/options.test.ts so the engine and GUI validators can't drift.
    [Theory]
    [InlineData("Imported Mail")]
    [InlineData("Work")]
    [InlineData("2024 Archive")]
    [InlineData("a.b")]
    [InlineData("Folder.name.with.dots")]
    [InlineData("café")]
    public void Validate_AcceptsValidNames(string name)
    {
        FolderNameValidator.Validate(name); // must not throw
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("tab\there")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9.log")]
    public void Validate_RejectsUnsafeNames(string name)
    {
        Assert.Throws<ConfigValidationException>(() => FolderNameValidator.Validate(name));
    }

    [Fact]
    public void Validate_RejectsNull()
    {
        Assert.Throws<ConfigValidationException>(() => FolderNameValidator.Validate(null));
    }
}
