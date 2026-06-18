// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using Mail2Pst.Core.Writing;
using Xunit;

namespace Mail2Pst.Core.Tests.Writing;

public class TransientFileRetryTests
{
    // Synthetic Win32 HResults: low word 32 = ERROR_SHARING_VIOLATION, 33 = ERROR_LOCK_VIOLATION.
    private const int SharingViolationHResult = unchecked((int)0x80070020);
    private const int LockViolationHResult = unchecked((int)0x80070021);
    // Low word 2 = ERROR_FILE_NOT_FOUND — a non-transient IOException.
    private const int FileNotFoundHResult = unchecked((int)0x80070002);

    [Fact]
    public void Run_RetriesTransientSharingViolation_ThenSucceeds()
    {
        int calls = 0;
        TransientFileRetry.Run(() =>
        {
            calls++;
            if (calls < 3) throw new IOException("locked", SharingViolationHResult);
        });
        Assert.Equal(3, calls); // failed twice, succeeded on the 3rd
    }

    [Fact]
    public void Run_AlwaysTransient_ExhaustsFiveAttemptsThenThrows()
    {
        int calls = 0;
        IOException ex = Assert.Throws<IOException>(() => TransientFileRetry.Run(() =>
        {
            calls++;
            throw new IOException("locked", LockViolationHResult);
        }));
        Assert.Equal(5, calls);
        Assert.Equal(LockViolationHResult, ex.HResult); // the original transient exception, unwrapped
    }

    [Fact]
    public void Run_NonTransientIOException_ThrowsImmediately()
    {
        int calls = 0;
        Assert.Throws<IOException>(() => TransientFileRetry.Run(() =>
        {
            calls++;
            throw new IOException("missing", FileNotFoundHResult);
        }));
        Assert.Equal(1, calls); // no retry — surfaced on the first attempt
    }
}
