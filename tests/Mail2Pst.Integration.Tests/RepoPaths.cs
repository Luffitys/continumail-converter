// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;

namespace Mail2Pst.Integration.Tests;

/// <summary>
/// Resolves corpus-config source paths for the local Tier-B corpus tier.
///
/// A corpus ConversionConfig (e.g. testdata/corpus-thunderbird-config.json) lists source paths
/// RELATIVE TO THE REPO ROOT (e.g. "testdata/mbox-corpus-thunderbird/Inbox.mbox"). But the test
/// process runs with its working directory set to the test bin output dir, so those relative paths
/// would otherwise resolve to a non-existent location and every source would be recorded as a
/// missing-source skip. Anchoring relative paths to the repo root makes the tier work from any cwd
/// on any machine without per-machine config edits. Absolute paths pass through unchanged.
/// </summary>
public static class RepoPaths
{
    /// <summary>The repo root: nearest ancestor of the test output dir that contains Mail2Pst.sln.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ResolveAgainstRepoRoot(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(RepoRoot, path));

    private static string FindRepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Mail2Pst.sln")))
                return dir.FullName;
        }
        throw new InvalidOperationException(
            $"Could not locate the repo root (no Mail2Pst.sln above '{AppContext.BaseDirectory}').");
    }
}
