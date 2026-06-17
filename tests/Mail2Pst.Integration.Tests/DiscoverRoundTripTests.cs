// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Config;
using Mail2Pst.Core.Discovery;
using MimeKit;
using Xunit;

namespace Mail2Pst.Integration.Tests;

public class DiscoverRoundTripTests
{
    private static void WriteMbox(string path, int count, string idPrefix)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        for (int i = 0; i < count; i++)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("S", "sender@example.com"));
            msg.To.Add(new MailboxAddress("R", "recipient@example.com"));
            msg.Subject = $"{idPrefix} {i}";
            msg.Date = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i);
            msg.MessageId = $"{idPrefix}-{i}@example.com";
            msg.Body = new TextPart("plain") { Text = $"Body {idPrefix} {i}." };
            // NOTE: 00:{i:D2}:00 assumes count < 60; current fixtures use <=3.
            byte[] from = System.Text.Encoding.ASCII.GetBytes($"From sender@example.com Mon Jan  1 00:{i:D2}:00 2024\r\n");
            fs.Write(from, 0, from.Length);
            msg.WriteTo(fs);
            fs.WriteByte((byte)'\r'); fs.WriteByte((byte)'\n');
        }
    }

    [Fact]
    public void Discover_ThenConvert_RoundTrips()
    {
        string tree = Path.Combine(Path.GetTempPath(), "m2p-disc-rt-" + Guid.NewGuid());
        string outDir = Path.Combine(Path.GetTempPath(), "m2p-disc-out-" + Guid.NewGuid());
        Directory.CreateDirectory(tree);
        Directory.CreateDirectory(outDir);
        try
        {
            // A small Thunderbird-shaped tree where every node is a real (non-empty) folder.
            WriteMbox(Path.Combine(tree, "Inbox"), 2, "inbox");
            WriteMbox(Path.Combine(tree, "Inbox.sbd", "Clients"), 3, "clients");
            WriteMbox(Path.Combine(tree, "Inbox.sbd", "Clients.sbd", "Acme"), 2, "acme");
            WriteMbox(Path.Combine(tree, "Sent"), 1, "sent");

            DiscoveryResult disc = MailTreeDiscovery.Discover(tree);
            Assert.Equal("thunderbird", disc.Layout);
            Assert.DoesNotContain(disc.Warnings, w => w.Code == "invalid-folder-name"); // names are clean

            var config = new ConversionConfig
            {
                Outputs = new List<OutputGroupConfig>
                {
                    new()
                    {
                        Name = "Out", MaxSizeMB = 100, FolderMapping = FolderMappingMode.Mirror,
                        Sources = disc.Sources.Select(s => new SourceConfig
                        {
                            Path = s.Path, Type = s.Type, TargetFolderPath = s.TargetFolderPath.ToList(),
                        }).ToList(),
                    },
                },
            };

            var (outputs, report) = RoundTripHarness.Convert(config, outDir);
            Assert.Empty(report.Skipped);

            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            RoundTripComparer.AssertRoundTrip(truth, readback);

            // Anchors: the reconstructed tree shape made it into the PST.
            Assert.Contains(readback, f => f.Path.SequenceEqual(new[] { "Inbox", "Clients", "Acme" }));
            Assert.Contains(readback, f => f.Path.SequenceEqual(new[] { "Sent" }));
        }
        finally
        {
            Directory.Delete(tree, true);
            Directory.Delete(outDir, true);
        }
    }
}
