// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Config;
using MimeKit;
using Xunit;

namespace Mail2Pst.Integration.Tests;

public class NestedRoundTripTests
{
    private static string WriteMbox(string dir, string name, int count, int attachKB = 0)
    {
        string path = Path.Combine(dir, name);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        byte[]? blob = attachKB > 0 ? new byte[attachKB * 1024] : null;
        if (blob != null) new Random(99).NextBytes(blob);
        for (int i = 0; i < count; i++)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Sender", "sender@example.com"));
            msg.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
            msg.Subject = $"{name} msg {i}";
            msg.Date = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i);
            msg.MessageId = $"{name}-{i}@example.com";
            var body = new TextPart("plain") { Text = $"Body {name} {i}." };
            if (blob != null)
            {
                var att = new MimePart("application", "octet-stream")
                {
                    Content = new MimeContent(new MemoryStream(blob)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = $"b{i}.bin" },
                    ContentTransferEncoding = ContentEncoding.Base64,
                };
                msg.Body = new Multipart("mixed") { body, att };
            }
            else msg.Body = body;
            byte[] from = System.Text.Encoding.ASCII.GetBytes($"From sender@example.com Mon Jan  1 00:{i:D2}:00 2024\r\n");
            fs.Write(from, 0, from.Length);
            msg.WriteTo(fs);
            fs.WriteByte((byte)'\r'); fs.WriteByte((byte)'\n');
        }
        return path;
    }

    private static SourceConfig Src(string path, params string[] target) =>
        new() { Type = "mbox", Path = path, TargetFolderPath = new List<string>(target) };

    private static string NewDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "mail2pst-nrt-" + Guid.NewGuid());
        Directory.CreateDirectory(d);
        return d;
    }

    // Every node is a mapped source (mirrors real Thunderbird, where a parent folder is itself an mbox),
    // so there are no purely-structural parents and folder-set parity holds.
    private static ConversionConfig NestedTree(string dir, int maxSizeMB) => new()
    {
        Outputs = new List<OutputGroupConfig>
        {
            new()
            {
                Name = "Out", MaxSizeMB = maxSizeMB, FolderMapping = FolderMappingMode.Mirror,
                Sources = new List<SourceConfig>
                {
                    Src(WriteMbox(dir, "inbox.mbox", 2), "Inbox"),
                    Src(WriteMbox(dir, "clients.mbox", 3), "Inbox", "Clients"),
                    Src(WriteMbox(dir, "acme.mbox", 2), "Inbox", "Clients", "Acme"),
                    Src(WriteMbox(dir, "sent.mbox", 1), "Sent"),
                },
            },
        },
    };

    [Fact]
    public void NestedTree_RoundTrips()
    {
        string dir = NewDir();
        try
        {
            ConversionConfig config = NestedTree(dir, maxSizeMB: 100);
            var (outputs, _) = RoundTripHarness.Convert(config, dir);
            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            RoundTripComparer.AssertRoundTrip(truth, readback);

            // Anchors: the tree shape exists.
            Assert.Contains(readback, f => f.Path.SequenceEqual(new[] { "Inbox", "Clients", "Acme" }));
            Assert.Contains(readback, f => f.Path.SequenceEqual(new[] { "Sent" }));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void WrongNesting_FailsComparer()
    {
        string dir = NewDir();
        try
        {
            ConversionConfig config = NestedTree(dir, maxSizeMB: 100);
            var (outputs, _) = RoundTripHarness.Convert(config, dir);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            // Corrupt the truth's nesting: claim "acme.mbox" lands at ["Inbox","Acme"] not ["Inbox","Clients","Acme"].
            var wrong = new ConversionConfig
            {
                Outputs = new List<OutputGroupConfig>
                {
                    new()
                    {
                        Name = "Out", MaxSizeMB = 100, FolderMapping = FolderMappingMode.Mirror,
                        Sources = new List<SourceConfig>
                        {
                            Src(Path.Combine(dir, "inbox.mbox"), "Inbox"),
                            Src(Path.Combine(dir, "clients.mbox"), "Inbox", "Clients"),
                            Src(Path.Combine(dir, "acme.mbox"), "Inbox", "Acme"),   // wrong parent
                            Src(Path.Combine(dir, "sent.mbox"), "Sent"),
                        },
                    },
                },
            };
            var wrongTruth = RoundTripHarness.BuildTruth(wrong);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => RoundTripComparer.AssertRoundTrip(wrongTruth, readback));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NestedTree_AcrossSplit_MergesByFullPath()
    {
        string dir = NewDir();
        try
        {
            // Force >=2 parts: ~4.2 MB of attachments into one deep nested folder, cap 1 MB.
            var config = new ConversionConfig
            {
                Outputs = new List<OutputGroupConfig>
                {
                    new()
                    {
                        Name = "Out", MaxSizeMB = 1, FolderMapping = FolderMappingMode.Mirror,
                        Sources = new List<SourceConfig>
                        {
                            Src(WriteMbox(dir, "deep.mbox", count: 6, attachKB: 700), "Deep", "Folder", "Tree"),
                        },
                    },
                },
            };
            var (outputs, _) = RoundTripHarness.Convert(config, dir);
            Assert.True(outputs.Count >= 2, $"expected a split (>=2 parts), got {outputs.Count}");

            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);

            // The nested folder appears ONCE, merged across parts.
            ReadFolder folder = Assert.Single(readback, f => f.Path.SequenceEqual(new[] { "Deep", "Folder", "Tree" }));
            Assert.Equal(6, folder.Messages.Count);

            // Only the leaf is a mapped source; the structural parents "Deep" and "Deep/Folder"
            // are created by the writer and are reproduced by prefix-aware BuildTruth, so the
            // folder-set parity in AssertRoundTrip holds.
            RoundTripComparer.AssertRoundTrip(truth, readback);
        }
        finally { Directory.Delete(dir, true); }
    }
}
