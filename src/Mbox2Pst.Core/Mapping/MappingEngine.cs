// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using System.IO;
using Mbox2Pst.Core.Config;

namespace Mbox2Pst.Core.Mapping;

public static class MappingEngine
{
    /// <summary>
    /// Folder name used for sources in a "flatten" group that don't have a
    /// per-source <see cref="SourceConfig.TargetFolder"/> override.
    /// </summary>
    public const string DefaultFlattenFolderName = "Imported Mail";

    public static List<PstOutputPlan> BuildPlan(ConversionConfig config)
    {
        var plans = new List<PstOutputPlan>();

        foreach (OutputGroupConfig output in config.Outputs)
        {
            var plan = new PstOutputPlan
            {
                Name = output.Name,
                MaxSizeBytes = output.MaxSizeMB * 1024L * 1024L,
                IncludeEmptyFolders = output.IncludeEmptyFolders,
            };

            foreach (SourceConfig source in output.Sources)
            {
                string targetFolder;
                if (!string.IsNullOrEmpty(source.TargetFolder))
                {
                    targetFolder = source.TargetFolder;
                }
                else if (output.FolderMapping == FolderMappingMode.Mirror)
                {
                    targetFolder = Path.GetFileNameWithoutExtension(source.Path);
                }
                else
                {
                    targetFolder = DefaultFlattenFolderName;
                }

                plan.SourceMappings.Add(new SourceMapping { Source = source, TargetFolderName = targetFolder });
            }

            plans.Add(plan);
        }

        return plans;
    }
}
