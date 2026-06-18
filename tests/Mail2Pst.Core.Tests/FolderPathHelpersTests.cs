// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using Mail2Pst.Core;
using Xunit;

namespace Mail2Pst.Core.Tests;

public class FolderPathHelpersTests
{
    [Fact]
    public void Display_JoinsWithSpaceSlashSpace()
        => Assert.Equal("A / B / Sent", FolderPathDisplay.Join(new[] { "A", "B", "Sent" }));

    [Fact]
    public void Display_SingleSegment_IsBareName()
        => Assert.Equal("Inbox", FolderPathDisplay.Join(new[] { "Inbox" }));

    [Fact]
    public void Key_JoinsWithNul()
        => Assert.Equal("A\0B", FolderPathKey.Join(new[] { "A", "B" }));

    [Fact]
    public void Display_FromKey_RoundTripsToDisplay()
        => Assert.Equal("A / B", FolderPathDisplay.FromKey(FolderPathKey.Join(new[] { "A", "B" })));
}
