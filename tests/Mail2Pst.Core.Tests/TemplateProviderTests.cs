// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using PSTFileFormat;
using Xunit;

namespace Mail2Pst.Core.Tests;

// Regression guard for the sidecar packaging bug: the single-file CLI is run by
// the Tauri shell from a relocated directory (e.g. target/debug) with no loose
// assets/ folder beside it, so the blank PST template must travel *inside* the
// assembly. TemplateProvider extracts that embedded copy to a temp file.
public class TemplateProviderTests
{
    [Fact]
    public void ExtractToTempFile_WritesNonEmptyFileThatOpensAsPst()
    {
        string path = TemplateProvider.ExtractToTempFile();
        try
        {
            Assert.True(File.Exists(path), "extracted template should exist on disk");
            Assert.True(new FileInfo(path).Length > 0, "extracted template should not be empty");

            // The embedded template must be a valid Unicode PST the vendored reader can open.
            var pst = new PSTFile(path, FileAccess.Read);
            Assert.NotNull(pst.TopOfPersonalFolders);
            pst.CloseFile();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExtractToTempFile_IsByteIdenticalToCanonicalTemplate()
    {
        string canonical = Path.Combine(AppContext.BaseDirectory, "assets", "template.pst");
        string path = TemplateProvider.ExtractToTempFile();
        try
        {
            Assert.Equal(File.ReadAllBytes(canonical), File.ReadAllBytes(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SweepStaleTemplates_RemovesOnlyOldMatchingFiles()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mail2pst-sweep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string oldMatching = Path.Combine(dir, "continumail-template-aaa.pst");
            string newMatching = Path.Combine(dir, "continumail-template-bbb.pst");
            string boundaryMatching = Path.Combine(dir, "continumail-template-boundary.pst");
            string oldNonMatching = Path.Combine(dir, "something-else.pst");
            File.WriteAllText(oldMatching, "x");
            File.WriteAllText(newMatching, "x");
            File.WriteAllText(boundaryMatching, "x");
            File.WriteAllText(oldNonMatching, "x");

            DateTime now = DateTime.UtcNow;
            DateTime cutoff = now.AddHours(-24);
            File.SetLastWriteTimeUtc(oldMatching, now.AddHours(-25));
            File.SetLastWriteTimeUtc(newMatching, now);
            File.SetLastWriteTimeUtc(boundaryMatching, cutoff); // exactly at cutoff
            File.SetLastWriteTimeUtc(oldNonMatching, now.AddHours(-25));

            TemplateProvider.SweepStaleTemplates(dir, cutoff);

            Assert.False(File.Exists(oldMatching));     // old + matching name -> swept
            Assert.True(File.Exists(newMatching));       // matching name but too new -> kept
            Assert.True(File.Exists(boundaryMatching));  // exactly at cutoff -> kept (strict <)
            Assert.True(File.Exists(oldNonMatching));    // old but wrong name -> kept
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SweepStaleTemplates_MissingDirectory_DoesNotThrow()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mail2pst-sweep-missing-" + Guid.NewGuid().ToString("N"));
        // Directory is never created — enumeration must fail silently (best-effort).
        TemplateProvider.SweepStaleTemplates(dir, DateTime.UtcNow);
    }
}
