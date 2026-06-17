// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using Xunit;

namespace Mail2Pst.Core.Tests.Assets;

/// <summary>
/// Pins the provenance of the blank Unicode PST seed (assets/template.pst) so the
/// SHA-256 published in TEMPLATE-PROVENANCE.md is self-policing. If the template is
/// regenerated (tools/template-gen, dev-only), update BOTH the constants here AND
/// TEMPLATE-PROVENANCE.md in lockstep — this test will fail until you do.
/// </summary>
public class TemplateProvenanceTests
{
    private const string ExpectedSha256 =
        "e046dc3a0624e8e9f5861962a89e550243b9d6fe2d05d63446a5361a8a294a4a";
    private const long ExpectedSizeBytes = 271360;

    private static string OnDiskTemplate =>
        Path.Combine(AppContext.BaseDirectory, "assets", "template.pst");

    private const string EmbeddedResourceName = "Mail2Pst.Core.assets.template.pst";

    [Fact]
    public void OnDiskSeed_MatchesPublishedSizeAndHash()
    {
        Assert.True(File.Exists(OnDiskTemplate), $"template.pst not found at {OnDiskTemplate}");
        Assert.Equal(ExpectedSizeBytes, new FileInfo(OnDiskTemplate).Length);
        Assert.Equal(ExpectedSha256, Sha256Hex(File.ReadAllBytes(OnDiskTemplate)));
    }

    [Fact]
    public void EmbeddedSeed_MatchesPublishedHash()
    {
        // The shipped single-file app writes from the embedded copy; guard it too so
        // the embedded resource cannot silently drift from the on-disk seed.
        var asm = typeof(Mail2Pst.Core.TemplateProvider).Assembly;
        using Stream? s = asm.GetManifestResourceStream(EmbeddedResourceName);
        Assert.True(s is not null, $"embedded resource '{EmbeddedResourceName}' not found");
        using var ms = new MemoryStream();
        s!.CopyTo(ms);
        Assert.Equal(ExpectedSha256, Sha256Hex(ms.ToArray()));
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
