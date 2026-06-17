// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using Mail2Pst.Core.Config;
using MimeKit;
using Xunit;

namespace Mail2Pst.Integration.Tests;

public class SplitRoundTripTests
{
    // Build a small mbox of `count` messages, each carrying a ~`attachKB` KB attachment,
    // so a small maxSizeMB cap forces the output PST to split into multiple parts.
    private static string WriteLargeMbox(string dir, int count, int attachKB)
    {
        string path = Path.Combine(dir, "large.mbox");
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        var blob = new byte[attachKB * 1024];
        new Random(1234).NextBytes(blob);

        for (int i = 0; i < count; i++)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Sender", "sender@example.com"));
            msg.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
            msg.Subject = $"Large message {i}";
            msg.Date = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i);
            msg.MessageId = $"large{i}@example.com";

            var body = new TextPart("plain") { Text = $"Body of message {i}." };
            var attachment = new MimePart("application", "octet-stream")
            {
                Content = new MimeContent(new MemoryStream(blob)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = $"blob{i}.bin" },
                ContentTransferEncoding = ContentEncoding.Base64,
            };
            msg.Body = new Multipart("mixed") { body, attachment };

            byte[] from = System.Text.Encoding.ASCII.GetBytes($"From sender@example.com Mon Jan  1 00:{i:D2}:00 2024\r\n");
            fs.Write(from, 0, from.Length);
            msg.WriteTo(fs);
            fs.WriteByte((byte)'\r'); fs.WriteByte((byte)'\n');
        }
        return path;
    }

    [Fact]
    public void Split_AcrossParts_AggregatesAndRoundTrips()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "mail2pst-split-" + Guid.NewGuid());
        Directory.CreateDirectory(outDir);
        try
        {
            // Keep minimal: smallest count/size that deterministically yields >=2 parts (runtime-budgeted).
            string mbox = WriteLargeMbox(outDir, count: 6, attachKB: 700); // ~4.2 MB of attachments
            var config = new ConversionConfig
            {
                Outputs = new List<OutputGroupConfig>
                {
                    new()
                    {
                        Name = "Archive", MaxSizeMB = 1, FolderMapping = FolderMappingMode.Mirror,
                        Sources = new List<SourceConfig> { new() { Type = "mbox", Path = mbox } },
                    },
                },
            };

            var (outputs, report) = RoundTripHarness.Convert(config, outDir);
            Assert.True(outputs.Count >= 2, $"expected a split (>=2 parts), got {outputs.Count}");

            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            // Same-path folder ("large") across parts must aggregate into one logical folder.
            ReadFolder folder = Assert.Single(readback, f => f.Path.SequenceEqual(new[] { "large" }));
            Assert.Equal(6, folder.Messages.Count);

            RoundTripComparer.AssertRoundTrip(truth, readback);
        }
        finally { Directory.Delete(outDir, true); }
    }
}
