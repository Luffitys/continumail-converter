// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Config;

namespace Mail2Pst.Core.Mapping;

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
                IReadOnlyList<string> targetPath;
                if (source.TargetFolderPath is not null)
                    // An explicit path (even if empty/invalid) is treated as explicit — never
                    // silently falls back to a mode default. Invalid explicit targets are rejected
                    // up front by ConfigValidator in the real pipeline, and by GetOrCreateFolder's
                    // guard at write time; we do NOT mask them here by reverting to mode behaviour.
                    targetPath = source.TargetFolderPath.ToArray();        // copy: immutable snapshot
                else if (source.TargetFolder is not null)
                    // Same rule for the flat shorthand: an explicit (even empty) TargetFolder stays
                    // explicit. Using `is not null` (not IsNullOrEmpty) so an invalid "" is not
                    // silently converted into a mirror/flatten default.
                    targetPath = new[] { source.TargetFolder };
                else if (output.FolderMapping == FolderMappingMode.Mirror)
                    targetPath = new[] { Path.GetFileNameWithoutExtension(source.Path) };
                else
                    targetPath = new[] { DefaultFlattenFolderName };

                plan.SourceMappings.Add(new SourceMapping { Source = source, TargetFolderPath = targetPath });
            }

            plans.Add(plan);
        }

        return plans;
    }
}
