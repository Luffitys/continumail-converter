// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using PSTFileFormat;
using Xunit;

namespace Mail2Pst.Core.Tests.PSTFileFormat;

public class AllocationMapPageTests
{
    [Fact]
    public void GetMaxAlignedContiguousSpace_EmptyPage_ReturnsMappedLength()
    {
        var page = new AllocationMapPage(new byte[AllocationMapPage.Length]);

        Assert.Equal(AllocationMapPage.MapppedLength, page.GetMaxAlignedContiguousSpace());
        Assert.Equal(AllocationMapPage.MapppedLength, page.GetMaxContiguousSpace());
    }

    [Fact]
    public void GetMaxAlignedContiguousSpace_FullPage_ReturnsZero()
    {
        byte[] buffer = new byte[AllocationMapPage.Length];
        for (int i = 0; i < 496; i++)
        {
            buffer[i] = 0xFF;
        }
        var page = new AllocationMapPage(buffer);

        Assert.Equal(0, page.GetMaxAlignedContiguousSpace());
        Assert.Equal(0, page.GetMaxContiguousSpace());
    }

    [Fact]
    public void GetMaxAlignedContiguousSpace_FreeRunNotStartingAtByteBoundary_ExcludesUnalignedPortion()
    {
        // Byte 0 = 0xF0: the high 4 bits (bitNumber 0-3) are allocated, the low 4 bits
        // (bitNumber 4-7) are free. That free run starts at bitNumber 4 (not a byte
        // boundary) and merges with the all-free remainder of the page.
        byte[] buffer = new byte[AllocationMapPage.Length];
        buffer[0] = 0xF0;
        var page = new AllocationMapPage(buffer);

        // General: the 256-byte run at the end of byte 0 merges with the 495 fully-free
        // bytes that follow (495 * 512 = 253440) for a total of 253696.
        Assert.Equal(253696, page.GetMaxContiguousSpace());

        // Aligned: the run starting mid-byte-0 doesn't count. The best aligned start is
        // byte 1 (a true byte/512-byte boundary), giving 495 * 512 = 253440.
        Assert.Equal(253440, page.GetMaxAlignedContiguousSpace());
    }

    [Fact]
    public void GetMaxAlignedContiguousSpace_FreeRunStartingAtByteBoundary_Counts()
    {
        // Byte 0 = 0x00 (fully free, a 512-byte run starting at a byte boundary),
        // all other bytes = 0xFF (allocated).
        byte[] buffer = new byte[AllocationMapPage.Length];
        for (int i = 1; i < 496; i++)
        {
            buffer[i] = 0xFF;
        }
        var page = new AllocationMapPage(buffer);

        Assert.Equal(512, page.GetMaxAlignedContiguousSpace());
        Assert.Equal(512, page.GetMaxContiguousSpace());
    }
}
