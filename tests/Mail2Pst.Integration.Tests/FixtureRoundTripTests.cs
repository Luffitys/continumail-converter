// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Config;
using Xunit;

namespace Mail2Pst.Integration.Tests;

public class FixtureRoundTripTests
{
    private static ConversionConfig MirrorConfig(string fixtureFileName) => new()
    {
        Outputs = new List<OutputGroupConfig>
        {
            new()
            {
                Name = "Personal", MaxSizeMB = 100, FolderMapping = FolderMappingMode.Mirror,
                Sources = new List<SourceConfig>
                {
                    new() { Type = "mbox", Path = Path.Combine(RoundTripHarness.FixturesDir, fixtureFileName) },
                },
            },
        },
    };

    private static string NewOutDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "mail2pst-rt-" + Guid.NewGuid());
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Sample_RoundTrips_WithAnchors()
    {
        ConversionConfig config = MirrorConfig("sample.mbox");
        string outDir = NewOutDir();
        try
        {
            var (outputs, _) = RoundTripHarness.Convert(config, outDir);
            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            RoundTripComparer.AssertRoundTrip(truth, readback);

            // Parser-truth anchors (catch a regression the comparer cannot, since truth = parser output).
            ReadFolder sample = readback.Single(f => f.Path.SequenceEqual(new[] { "sample" }));
            Assert.Equal(2, sample.Messages.Count);
            // Bracketed Message-ID; if an anchor fails, fix to the actual fixture value, don't weaken it.
            ReadBackMessage m1 = sample.Messages.Single(m => m.MessageId == "<msg1@example.com>");
            Assert.Equal("Hello from Alice", m1.Subject);
            Assert.Equal("alice@example.com", m1.FromAddress);
            Assert.Contains(m1.Recipients, r => r.Address == "bob@example.com" && r.Kind == RecipientKind.To);
        }
        finally { Directory.Delete(outDir, true); }
    }

    [Fact]
    public void Attachments_RoundTrip_WithAnchors()
    {
        ConversionConfig config = MirrorConfig("mbox-with-attachments.mbox");
        string outDir = NewOutDir();
        try
        {
            var (outputs, _) = RoundTripHarness.Convert(config, outDir);
            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            RoundTripComparer.AssertRoundTrip(truth, readback);

            var allNames = readback.SelectMany(f => f.Messages).SelectMany(m => m.AttachmentNames).ToList();
            Assert.Contains("hello.txt", allNames);              // a real attachment
            Assert.Contains("attached-message.eml", allNames);  // embedded message surfaces
            Assert.DoesNotContain("pic.png", allNames);         // inline CID image is hidden
        }
        finally { Directory.Delete(outDir, true); }
    }

    [Fact]
    public void BrokenAttachment_RoundTrips_SurvivingMessage()
    {
        ConversionConfig config = MirrorConfig("mbox-with-broken-attachment.mbox");
        string outDir = NewOutDir();
        try
        {
            var (outputs, report) = RoundTripHarness.Convert(config, outDir);
            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            RoundTripComparer.AssertRoundTrip(truth, readback);

            Assert.Equal(1, report.ConvertedCount);           // anchor: the one message survives
            Assert.NotEmpty(report.Warnings);                  // the broken attachment is surfaced as a warning
        }
        finally { Directory.Delete(outDir, true); }
    }
}
