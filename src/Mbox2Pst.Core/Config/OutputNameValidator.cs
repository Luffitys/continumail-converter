// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Mbox2Pst.Core.Config;

/// <summary>
/// Validates an output group's <c>Name</c> as a safe file-name stem before it
/// is combined with the output directory. Rejects path separators, traversal,
/// rooted/drive-qualified paths, invalid characters, and Windows reserved
/// device names — all of which could write outside the output directory or
/// produce an unusable file. Throws <see cref="ConfigValidationException"/>.
/// </summary>
public static class OutputNameValidator
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static void Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ConfigValidationException("Output name must not be empty.");

        if (name is "." or "..")
            throw new ConfigValidationException($"Output name '{name}' is not a valid file name.");

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ConfigValidationException(
                $"Output name '{name}' contains an invalid character (path separators, ':', '*', '?', etc. are not allowed).");

        if (ReservedNames.Contains(name))
            throw new ConfigValidationException($"Output name '{name}' is a reserved device name.");
    }
}
