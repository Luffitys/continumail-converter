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

public class NestedFolderWriteTests
{
    // One tiny mbox with `count` synthetic messages and unique Message-IDs.
    private static string WriteMbox(string dir, string name, int count)
    {
        string path = Path.Combine(dir, name);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        for (int i = 0; i < count; i++)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Sender", "sender@example.com"));
            msg.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
            msg.Subject = $"{name} msg {i}";
            msg.Date = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i);
            msg.MessageId = $"{name}-{i}@example.com";
            msg.Body = new TextPart("plain") { Text = $"Body {name} {i}." };
            byte[] from = System.Text.Encoding.ASCII.GetBytes($"From sender@example.com Mon Jan  1 00:{i:D2}:00 2024\r\n");
            fs.Write(from, 0, from.Length);
            msg.WriteTo(fs);
            fs.WriteByte((byte)'\r'); fs.WriteByte((byte)'\n');
        }
        return path;
    }

    private static SourceConfig Src(string path, params string[] target) =>
        new() { Type = "mbox", Path = path, TargetFolderPath = new List<string>(target) };

    private static (IReadOnlyList<ReadFolder> readback, IReadOnlyList<string> outputs) Run(
        string dir, bool includeEmpty, params SourceConfig[] sources)
    {
        var config = new ConversionConfig
        {
            Outputs = new List<OutputGroupConfig>
            {
                new()
                {
                    Name = "Out", MaxSizeMB = 100, FolderMapping = FolderMappingMode.Mirror,
                    IncludeEmptyFolders = includeEmpty,
                    Sources = new List<SourceConfig>(sources),
                },
            },
        };
        var (outputs, _) = RoundTripHarness.Convert(config, dir);
        return (PstReader.Read(outputs), outputs);
    }

    private static bool Has(IReadOnlyList<ReadFolder> rb, params string[] path) =>
        rb.Any(f => f.Path.SequenceEqual(path));

    private static ReadFolder Get(IReadOnlyList<ReadFolder> rb, params string[] path) =>
        rb.Single(f => f.Path.SequenceEqual(path));

    private static string NewDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "mail2pst-nested-" + Guid.NewGuid());
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void CreatesNestedPath_AndParentWithMessagesAndChild()
    {
        string dir = NewDir();
        try
        {
            // "A" has its own messages AND a child "A/B" with messages (Thunderbird-style).
            var (rb, _) = Run(dir, includeEmpty: true,
                Src(WriteMbox(dir, "a.mbox", 2), "A"),
                Src(WriteMbox(dir, "ab.mbox", 3), "A", "B"));

            Assert.Equal(2, Get(rb, "A").Messages.Count);
            Assert.Equal(3, Get(rb, "A", "B").Messages.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SameFullPath_Merges()
    {
        string dir = NewDir();
        try
        {
            var (rb, _) = Run(dir, includeEmpty: true,
                Src(WriteMbox(dir, "x.mbox", 2), "Inbox", "Clients"),
                Src(WriteMbox(dir, "y.mbox", 3), "Inbox", "Clients"));

            Assert.Equal(5, Get(rb, "Inbox", "Clients").Messages.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SameLeafUnderDifferentParents_StaysDistinct()
    {
        string dir = NewDir();
        try
        {
            var (rb, _) = Run(dir, includeEmpty: true,
                Src(WriteMbox(dir, "w.mbox", 1), "Work", "Inbox"),
                Src(WriteMbox(dir, "p.mbox", 4), "Personal", "Inbox"));

            Assert.Single(Get(rb, "Work", "Inbox").Messages);
            Assert.Equal(4, Get(rb, "Personal", "Inbox").Messages.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmptyLeaf_IncludeEmptyTrue_FolderAndAncestorsExist()
    {
        string dir = NewDir();
        try
        {
            var (rb, _) = Run(dir, includeEmpty: true,
                Src(WriteMbox(dir, "empty.mbox", 0), "A", "B", "C"));

            Assert.True(Has(rb, "A", "B", "C"));
            Assert.Empty(Get(rb, "A", "B", "C").Messages);
            Assert.True(Has(rb, "A"));        // ancestors precreated
            Assert.True(Has(rb, "A", "B"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmptyLeaf_IncludeEmptyFalse_CreatesNeitherLeafNorOrphanParents()
    {
        string dir = NewDir();
        try
        {
            // Only an EMPTY source maps under "A" -> nothing should be created at all.
            var (rb, _) = Run(dir, includeEmpty: false,
                Src(WriteMbox(dir, "empty.mbox", 0), "A", "B", "C"));

            Assert.False(Has(rb, "A"));
            Assert.False(Has(rb, "A", "B"));
            Assert.False(Has(rb, "A", "B", "C"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void IncludeEmptyFalse_AncestorsCreatedOnDemandForNonEmptyDescendant()
    {
        string dir = NewDir();
        try
        {
            // "A/C" has messages (so A is created on demand); empty "A/B" must NOT appear.
            var (rb, _) = Run(dir, includeEmpty: false,
                Src(WriteMbox(dir, "ac.mbox", 2), "A", "C"),
                Src(WriteMbox(dir, "ab.mbox", 0), "A", "B"));

            Assert.Equal(2, Get(rb, "A", "C").Messages.Count);
            Assert.True(Has(rb, "A"));          // structural ancestor created for A/C
            Assert.False(Has(rb, "A", "B"));     // empty leaf skipped
        }
        finally { Directory.Delete(dir, true); }
    }
}
