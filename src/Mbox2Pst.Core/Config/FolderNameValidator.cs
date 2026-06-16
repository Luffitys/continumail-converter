// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

namespace Mbox2Pst.Core.Config;

/// <summary>
/// Validates a per-source <see cref="SourceConfig.TargetFolder"/> as a safe PST
/// folder name. This is the engine-side enforcement of the SAME policy the GUI
/// applies in <c>desktop/src/lib/options.ts</c> <c>validateFolderName</c> — keep the
/// two rule-sets in sync (a parity test in both projects guards drift). The GUI must
/// not be the only defense: a hand-written config or direct CLI use bypasses it.
/// Throws <see cref="ConfigValidationException"/>. The caller only validates non-null
/// values, but a null/empty/whitespace name is rejected here too.
/// </summary>
public static class FolderNameValidator
{
    // Windows reserved device names, optionally followed by an extension
    // (e.g. "CON", "con.txt"). Mirrors the GUI regex
    // /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$/i.
    private static readonly Regex ReservedName =
        new(@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void Validate(string? name)
    {
        string value = name ?? string.Empty;
        string trimmed = value.Trim();

        if (trimmed.Length == 0)
            throw new ConfigValidationException("Folder name can't be empty.");

        if (value.IndexOfAny(new[] { '/', '\\' }) >= 0)
            throw new ConfigValidationException("Folder name can't contain \\ or /.");

        if (value.Any(ch => ch < 0x20))
            throw new ConfigValidationException("Folder name can't contain control characters.");

        if (value != trimmed)
            throw new ConfigValidationException("Folder name can't start or end with a space.");

        if (trimmed.StartsWith('.') || trimmed.EndsWith('.'))
            throw new ConfigValidationException("Folder name can't start or end with a dot.");

        if (ReservedName.IsMatch(trimmed))
            throw new ConfigValidationException("Folder name is reserved on Windows.");
    }
}
