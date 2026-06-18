// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Collections.Generic;

namespace Mail2Pst.Core;

/// <summary>
/// Stable identity key for a folder path: segments joined by NUL ('\0'), which folder
/// segments may never contain (control-char rule in FolderNameValidator). Identity ONLY —
/// never render to a user. Shared by the writer cache and the round-trip reader.
/// </summary>
internal static class FolderPathKey
{
    public static string Join(IReadOnlyList<string> path) => string.Join('\0', path);
}
