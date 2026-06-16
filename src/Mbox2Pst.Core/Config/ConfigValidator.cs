// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;

namespace Mbox2Pst.Core.Config;

/// <summary>
/// Validates a deserialized <see cref="ConversionConfig"/> before a run, so
/// problems surface as clear <see cref="ConfigValidationException"/> messages
/// rather than obscure failures mid-conversion. Checks config shape and source
/// readability; parser-type support is validated separately by the runner.
/// </summary>
public static class ConfigValidator
{
    public static void Validate(ConversionConfig config)
    {
        if (config.Outputs is null || config.Outputs.Count == 0)
            throw new ConfigValidationException("Config has no outputs; at least one output group is required.");

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (OutputGroupConfig output in config.Outputs)
        {
            // Name must be a safe file stem (separators, '..', invalid chars, reserved names).
            OutputNameValidator.Validate(output.Name);

            // Duplicate names map to the same output file(s) — reject (case-insensitive,
            // matching Windows file-name semantics).
            if (!seenNames.Add(output.Name))
                throw new ConfigValidationException($"Duplicate output name '{output.Name}'. Output names must be unique.");

            if (output.MaxSizeMB <= 0)
                throw new ConfigValidationException(
                    $"Output '{output.Name}' has maxSizeMB={output.MaxSizeMB}; it must be greater than 0.");

            if (output.Sources is null || output.Sources.Count == 0)
                throw new ConfigValidationException($"Output '{output.Name}' has no sources.");

            foreach (SourceConfig source in output.Sources)
            {
                // Empty path is a config mistake that would otherwise crash with an
                // uncaught ArgumentException. A *missing* file is intentionally left to
                // the runner, which records it as a per-source skip (see
                // ConversionRunnerTests.Run_MissingSourceFile_RecordsSkipInsteadOfThrowing).
                if (string.IsNullOrWhiteSpace(source.Path))
                    throw new ConfigValidationException($"Output '{output.Name}' has a source with an empty path.");

                // An explicit per-source folder override must be a safe PST folder name.
                // The engine — not only the GUI — enforces this; CLI/hand-written configs
                // bypass the GUI. A null TargetFolder is legal: the folder name is derived
                // internally (mirror = filename stem; flatten = "Imported Mail").
                if (source.TargetFolder is not null)
                    FolderNameValidator.Validate(source.TargetFolder);
            }
        }
    }
}
