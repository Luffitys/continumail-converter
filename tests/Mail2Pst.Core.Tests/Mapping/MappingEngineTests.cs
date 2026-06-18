// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Mail2Pst.Core.Config;
using Mail2Pst.Core.Mapping;
using Xunit;

namespace Mail2Pst.Core.Tests.Mapping;

public class MappingEngineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildPlan_CarriesIncludeEmptyFoldersFlag(bool includeEmpty)
    {
        var config = new ConversionConfig
        {
            Outputs = new List<OutputGroupConfig>
            {
                new()
                {
                    Name = "Personal",
                    MaxSizeMB = 100,
                    IncludeEmptyFolders = includeEmpty,
                    Sources = new List<SourceConfig> { new() { Path = "Inbox.mbox", Type = "mbox" } },
                },
            },
        };

        List<PstOutputPlan> plans = MappingEngine.BuildPlan(config);

        Assert.Equal(includeEmpty, plans[0].IncludeEmptyFolders);
    }

    [Fact]
    public void BuildPlan_MirrorUsesSourceFileNameAsFolder()
    {
        var config = new ConversionConfig
        {
            Outputs = new List<OutputGroupConfig>
            {
                new()
                {
                    Name = "Personal",
                    MaxSizeMB = 100,
                    FolderMapping = FolderMappingMode.Mirror,
                    Sources = new List<SourceConfig>
                    {
                        new() { Path = "extracted/Inbox.mbox", Type = "mbox" },
                    },
                },
            },
        };

        List<PstOutputPlan> plans = MappingEngine.BuildPlan(config);

        Assert.Single(plans);
        Assert.Equal("Personal", plans[0].Name);
        Assert.Equal(100L * 1024 * 1024, plans[0].MaxSizeBytes);
        Assert.Equal(new[] { "Inbox" }, plans[0].SourceMappings[0].TargetFolderPath);
    }

    [Fact]
    public void BuildPlan_FlattenUsesDefaultFolderNameForAllSources()
    {
        var config = new ConversionConfig
        {
            Outputs = new List<OutputGroupConfig>
            {
                new()
                {
                    Name = "Archive",
                    FolderMapping = FolderMappingMode.Flatten,
                    Sources = new List<SourceConfig>
                    {
                        new() { Path = "extracted/old1.mbox", Type = "mbox" },
                        new() { Path = "extracted/old2.mbox", Type = "mbox" },
                    },
                },
            },
        };

        List<PstOutputPlan> plans = MappingEngine.BuildPlan(config);

        Assert.All(plans[0].SourceMappings, m => Assert.Equal(new[] { "Imported Mail" }, m.TargetFolderPath));
    }

    [Fact]
    public void BuildPlan_TargetFolderOverrideTakesPrecedenceOverMappingMode()
    {
        var config = new ConversionConfig
        {
            Outputs = new List<OutputGroupConfig>
            {
                new()
                {
                    Name = "Personal",
                    FolderMapping = FolderMappingMode.Mirror,
                    Sources = new List<SourceConfig>
                    {
                        new() { Path = "extracted/Sent.mbox", Type = "mbox", TargetFolder = "Sent Items" },
                    },
                },
            },
        };

        List<PstOutputPlan> plans = MappingEngine.BuildPlan(config);

        Assert.Equal(new[] { "Sent Items" }, plans[0].SourceMappings[0].TargetFolderPath);
    }

    [Fact]
    public void Mirror_NoExplicitTarget_UsesFilenameStem()
    {
        var config = OneOutput(FolderMappingMode.Mirror, new SourceConfig { Type = "mbox", Path = "C:/x/Inbox.mbox" });
        var plans = MappingEngine.BuildPlan(config);
        Assert.Equal(new[] { "Inbox" }, plans[0].SourceMappings[0].TargetFolderPath);
    }

    [Fact]
    public void Flatten_NoExplicitTarget_UsesImportedMail()
    {
        var config = OneOutput(FolderMappingMode.Flatten, new SourceConfig { Type = "mbox", Path = "C:/x/Inbox.mbox" });
        var plans = MappingEngine.BuildPlan(config);
        Assert.Equal(new[] { "Imported Mail" }, plans[0].SourceMappings[0].TargetFolderPath);
    }

    [Fact]
    public void TargetFolder_NormalizesToSingleSegment()
    {
        var config = OneOutput(FolderMappingMode.Mirror,
            new SourceConfig { Type = "mbox", Path = "a.mbox", TargetFolder = "Sent" });
        var plans = MappingEngine.BuildPlan(config);
        Assert.Equal(new[] { "Sent" }, plans[0].SourceMappings[0].TargetFolderPath);
    }

    [Theory]
    [InlineData(FolderMappingMode.Mirror)]
    [InlineData(FolderMappingMode.Flatten)]
    public void ExplicitPath_WinsOverMode(FolderMappingMode mode)
    {
        var config = OneOutput(mode,
            new SourceConfig { Type = "mbox", Path = "a.mbox", TargetFolderPath = new() { "A", "B" } });
        var plans = MappingEngine.BuildPlan(config);
        Assert.Equal(new[] { "A", "B" }, plans[0].SourceMappings[0].TargetFolderPath);
    }

    [Fact]
    public void Flatten_MixedExplicitAndDefault_EachResolvesIndependently()
    {
        var config = new ConversionConfig
        {
            Outputs = new List<OutputGroupConfig>
            {
                new()
                {
                    Name = "Out", MaxSizeMB = 100, FolderMapping = FolderMappingMode.Flatten,
                    Sources = new List<SourceConfig>
                    {
                        new() { Type = "mbox", Path = "s1.mbox", TargetFolderPath = new() { "A", "Inbox" } },
                        new() { Type = "mbox", Path = "s2.mbox" },
                    },
                },
            },
        };
        var plans = MappingEngine.BuildPlan(config);
        Assert.Equal(new[] { "A", "Inbox" }, plans[0].SourceMappings[0].TargetFolderPath);
        Assert.Equal(new[] { "Imported Mail" }, plans[0].SourceMappings[1].TargetFolderPath);
    }

    private static ConversionConfig OneOutput(FolderMappingMode mode, params SourceConfig[] sources) => new()
    {
        Outputs = new List<OutputGroupConfig>
        {
            new() { Name = "Out", MaxSizeMB = 100, FolderMapping = mode, Sources = new List<SourceConfig>(sources) },
        },
    };
}
