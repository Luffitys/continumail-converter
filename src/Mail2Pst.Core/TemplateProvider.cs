// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Mail2Pst.Core;

/// <summary>
/// Provides the blank Unicode PST template that every output PST is seeded from.
/// The template is embedded in this assembly (logical name
/// "Mail2Pst.Core.assets.template.pst") so it travels inside the single-file,
/// self-contained CLI sidecar: the Tauri shell relocates the lone exe into a
/// directory that has no loose assets/ folder beside it, so resolving the
/// template relative to <see cref="AppContext.BaseDirectory"/> fails there.
/// </summary>
public static class TemplateProvider
{
    private const string ResourceName = "Mail2Pst.Core.assets.template.pst";

    // Stale-temp sweep: orphaned templates from a crashed run leak as
    // continumail-template-<guid>.pst in TEMP. The 24h cutoff is generous enough that
    // no in-progress conversion could match, so a concurrent run's in-use template is
    // never deleted. Runs once per process (the GUI spawns a fresh CLI per conversion).
    private const string StaleTemplatePattern = "continumail-template-*.pst";
    private static readonly TimeSpan StaleTemplateAge = TimeSpan.FromHours(24);
    private static int _sweepDone; // 0 = not yet run, 1 = run (once-per-process guard)

    /// <summary>
    /// Extracts the embedded blank PST template to a fresh temp file and returns
    /// its path. The caller owns the file and should delete it when finished.
    /// </summary>
    public static string ExtractToTempFile()
    {
        if (Interlocked.Exchange(ref _sweepDone, 1) == 0)
            SweepStaleTemplates(Path.GetTempPath(), DateTime.UtcNow - StaleTemplateAge);

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

    /// <summary>
    /// Deletes continumail-template-*.pst files in <paramref name="directory"/> whose
    /// LastWriteTimeUtc is strictly older than <paramref name="utcCutoff"/>. Best-effort:
    /// enumeration, timestamp reads, and deletes are all guarded so this never throws —
    /// a per-file failure (locked or racing file) skips that file and continues. internal
    /// for direct testing.
    /// </summary>
    internal static void SweepStaleTemplates(string directory, DateTime utcCutoff)
    {
        // Broad catch is intentional here (not the engine's usual fail-loud stance): this is
        // best-effort orphan cleanup that must NEVER throw into / block ExtractToTempFile, so
        // every failure — enumeration or per-file — is swallowed. A per-file failure (locked or
        // racing file) skips that file and continues with the rest.
        try
        {
            foreach (string path in Directory.EnumerateFiles(directory, StaleTemplatePattern))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < utcCutoff)
                        File.Delete(path);
                }
                catch
                {
                    // best-effort: skip a locked/racing/unreadable file, continue with the rest
                }
            }
        }
        catch
        {
            // best-effort: enumeration failed (e.g. missing dir) -> sweep does nothing, never throws
        }
    }
}
