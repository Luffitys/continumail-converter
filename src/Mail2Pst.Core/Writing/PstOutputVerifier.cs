// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using PSTFileFormat;

namespace Mail2Pst.Core.Writing;

/// <summary>
/// Post-conversion smoke test: opens each finished PST read-only and confirms the total
/// message count across all parts equals the count the engine believes it wrote. A data-
/// integrity guard — a corrupt or short PST must fail loudly rather than be reported as
/// success. Throws InvalidDataException on any failure; never deletes output (the files are
/// kept for diagnosis).
/// </summary>
internal static class PstOutputVerifier
{
    internal static void Verify(IReadOnlyList<string> outputFiles, int expectedMessageCount)
    {
        int total = 0;
        foreach (string path in outputFiles)
        {
            PSTFile? pst = null;
            try
            {
                try
                {
                    // Open through the transient-lock retry: reading immediately after the
                    // writer closed the file is exactly the AV-scanner lock window.
                    TransientFileRetry.Run(() => pst = new PSTFile(path, FileAccess.Read));

                    PSTFolder root = pst!.TopOfPersonalFolders
                        ?? throw new InvalidDataException($"Output PST '{path}' has no root folder.");

                    total += CountMessagesRecursive(root);
                }
                catch (Exception ex)
                {
                    // Broad catch is intentional: the vendored PST library throws assorted
                    // (often non-IOException) exceptions for a corrupt/missing file. We wrap
                    // them all to add the file path and preserve the original as InnerException.
                    // Do NOT narrow this to IOException — that would drop the filename context
                    // for corruption failures.
                    throw new InvalidDataException(
                        $"Failed to open output PST '{path}' for verification.", ex);
                }
            }
            finally
            {
                pst?.CloseFile();
            }
        }

        if (total != expectedMessageCount)
        {
            throw new InvalidDataException(
                $"PST verification failed: expected {expectedMessageCount} message(s) across " +
                $"{outputFiles.Count} file(s) but found {total}.");
        }
    }

    private static int CountMessagesRecursive(PSTFolder folder)
    {
        int total = folder.MessageCount;
        foreach (PSTFolder child in folder.GetChildFolders())
            total += CountMessagesRecursive(child);
        return total;
    }
}
