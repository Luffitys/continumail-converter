// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using PSTFileFormat;
using Xunit;

namespace Mbox2Pst.Core.Tests;

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
}
