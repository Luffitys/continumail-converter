// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Collections.Generic;

namespace Mail2Pst.Core;

/// <summary>Human-readable folder-path rendering. Display ONLY — never use as a dictionary key.</summary>
public static class FolderPathDisplay
{
    public static string Join(IReadOnlyList<string> path) => string.Join(" / ", path);
    public static string FromKey(string key) => string.Join(" / ", key.Split('\0'));
}
