// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Reflection;

namespace Mbox2Pst.Core;

/// <summary>
/// Provides the blank Unicode PST template that every output PST is seeded from.
/// The template is embedded in this assembly (logical name
/// "Mbox2Pst.Core.assets.template.pst") so it travels inside the single-file,
/// self-contained CLI sidecar: the Tauri shell relocates the lone exe into a
/// directory that has no loose assets/ folder beside it, so resolving the
/// template relative to <see cref="AppContext.BaseDirectory"/> fails there.
/// </summary>
public static class TemplateProvider
{
    private const string ResourceName = "Mbox2Pst.Core.assets.template.pst";

    /// <summary>
    /// Extracts the embedded blank PST template to a fresh temp file and returns
    /// its path. The caller owns the file and should delete it when finished.
    /// </summary>
    public static string ExtractToTempFile()
    {
        Assembly asm = typeof(TemplateProvider).Assembly;
        using Stream stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded PST template resource '{ResourceName}' was not found in {asm.GetName().Name}.");

        string tempPath = Path.Combine(
            Path.GetTempPath(), $"continumail-template-{Guid.NewGuid():N}.pst");
        using (FileStream file = File.Create(tempPath))
        {
            stream.CopyTo(file);
        }
        return tempPath;
    }
}
