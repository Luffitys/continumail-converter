// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Mbox2Pst.Core.Config;
using Mbox2Pst.Core.Mapping;
using Xunit;

namespace Mbox2Pst.Core.Tests.Mapping;

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
        Assert.Equal("Inbox", plans[0].SourceMappings[0].TargetFolderName);
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

        Assert.All(plans[0].SourceMappings, m => Assert.Equal("Imported Mail", m.TargetFolderName));
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

        Assert.Equal("Sent Items", plans[0].SourceMappings[0].TargetFolderName);
    }
}
