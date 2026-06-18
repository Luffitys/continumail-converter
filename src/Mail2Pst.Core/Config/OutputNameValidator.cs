// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Text.RegularExpressions;

namespace Mail2Pst.Core.Config;

/// <summary>
/// Validates an output group's <c>Name</c> as a safe file-name stem before it
/// is combined with the output directory. Rejects path separators, traversal,
/// rooted/drive-qualified paths, invalid characters, control characters,
/// leading/trailing whitespace or periods (Windows silently strips these), and
/// Windows reserved device names (including with an extension, e.g. CON.backup,
/// since the stem becomes CON.backup.pst). Throws
/// <see cref="ConfigValidationException"/>.
///
/// This is a stricter, separate policy from <see cref="FolderNameValidator"/>:
/// an output name is a *file-name stem* (so it bans &lt; &gt; : " / \ | ? *),
/// whereas a PST folder name only bans / and \. The trailing-space/period and
/// reserved-name rules are kept aligned between the two.
/// </summary>
public static class OutputNameValidator
{
    // Explicit Windows invalid file-name characters. Hardcoded (not
    // Path.GetInvalidFileNameChars(), which is OS-specific — Linux returns only
    // '\0' and '/') because output PSTs are Windows/Outlook artifacts, so the
    // policy must be the Windows one regardless of the build/CI OS.
    private static readonly char[] InvalidNameChars =
        { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    // Windows reserved device names, optionally followed by an extension
    // (e.g. "CON", "CON.backup"). Mirrors FolderNameValidator's regex; the
    // IgnoreCase flag preserves the previous OrdinalIgnoreCase behaviour.
    private static readonly Regex ReservedName =
        new(@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ConfigValidationException("Output name must not be empty.");

        // Literal "." / ".." kept first for their specific, clearer message.
        if (name is "." or "..")
            throw new ConfigValidationException($"Output name '{name}' is not a valid file name.");

        if (name.IndexOfAny(InvalidNameChars) >= 0)
            throw new ConfigValidationException(
                $"Output name '{name}' contains an invalid character (path separators, ':', '*', '?', etc. are not allowed).");

        foreach (char ch in name)
        {
            if (ch < 0x20)
                throw new ConfigValidationException(
                    $"Output name '{name}' contains a control character.");
        }

        if (name != name.Trim())
            throw new ConfigValidationException(
                $"Output name '{name}' can't start or end with a space.");

        // Trailing/leading period check before the reserved-name regex so that
        // "Archive." reports the period problem, not "reserved device name"
        // (the (\..*)? clause of ReservedName would also match it).
        if (name.StartsWith('.') || name.EndsWith('.'))
            throw new ConfigValidationException(
                $"Output name '{name}' can't start or end with a period.");

        if (ReservedName.IsMatch(name))
            throw new ConfigValidationException($"Output name '{name}' is a reserved device name.");
    }
}
