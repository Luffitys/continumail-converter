// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using Xunit;

namespace Mail2Pst.Integration.Tests;

public class RepoPathsTests
{
    [Fact]
    public void RepoRoot_ContainsSolution()
        => Assert.True(File.Exists(Path.Combine(RepoPaths.RepoRoot, "Mail2Pst.sln")),
            $"RepoRoot '{RepoPaths.RepoRoot}' should contain Mail2Pst.sln");

    [Fact]
    public void Resolve_RelativePath_IsAbsoluteUnderRepoRoot()
    {
        string resolved = RepoPaths.ResolveAgainstRepoRoot("testdata/x.mbox");
        Assert.True(Path.IsPathRooted(resolved));
        Assert.StartsWith(RepoPaths.RepoRoot, resolved);
        Assert.EndsWith(Path.Combine("testdata", "x.mbox"), resolved);
    }

    [Fact]
    public void Resolve_AbsolutePath_PassesThroughUnchanged()
    {
        string abs = Path.Combine(RepoPaths.RepoRoot, "testdata", "y.mbox");
        Assert.Equal(abs, RepoPaths.ResolveAgainstRepoRoot(abs));
    }
}
