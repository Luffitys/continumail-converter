// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Parsing;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing;

public class MboxParserTempFileTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Parse_LargeAttachment_SpillsTempFile_IsTempFileBacked()
    {
        // threshold = 1 forces every non-empty attachment to a temp file
        var parser = new MboxParser(tempFileThresholdBytes: 1);
        var results = parser.Parse(Fixture("mbox-with-attachments.mbox")).ToList();

        var attachmentsWithContent = results
            .Where(r => r.Success)
            .SelectMany(r => r.Message!.Attachments)
            .Where(a => a.Content.Length > 0)
            .ToList();

        Assert.NotEmpty(attachmentsWithContent);
        foreach (var attachment in attachmentsWithContent)
        {
            Assert.True(attachment.Content.IsTempFileBacked,
                $"Attachment '{attachment.FileName}' should be temp-file-backed with threshold=1");
        }

        foreach (var attachment in attachmentsWithContent)
            attachment.Content.Dispose();
    }

    [Fact]
    public void Parse_LargeAttachment_SpillsTempFile_TempFileExistsBeforeDispose()
    {
        var parser = new MboxParser(tempFileThresholdBytes: 1);
        var results = parser.Parse(Fixture("mbox-with-attachments.mbox")).ToList();

        var spilledAttachments = results
            .Where(r => r.Success)
            .SelectMany(r => r.Message!.Attachments)
            .Where(a => a.Content.IsTempFileBacked)
            .ToList();

        Assert.NotEmpty(spilledAttachments);
        foreach (var attachment in spilledAttachments)
        {
            Assert.True(File.Exists(attachment.Content.TempPath),
                "Temp file must exist before Dispose is called");
        }

        foreach (var attachment in spilledAttachments)
            attachment.Content.Dispose();
    }

    [Fact]
    public void Parse_LargeAttachment_SpillsTempFile_ContentMatchesOriginalBytes()
    {
        // Parse once with MaxValue threshold (everything in-memory)
        var parserDefault = new MboxParser(tempFileThresholdBytes: long.MaxValue);
        var inMemoryAttachments = parserDefault.Parse(Fixture("mbox-with-attachments.mbox"))
            .Where(r => r.Success)
            .SelectMany(r => r.Message!.Attachments)
            .Select(a => a.Content.ReadAllBytes())
            .ToList();

        // Parse again with threshold=1 (temp file for every non-empty attachment)
        var parserLowThreshold = new MboxParser(tempFileThresholdBytes: 1);
        var tempFileAttachments = parserLowThreshold.Parse(Fixture("mbox-with-attachments.mbox"))
            .Where(r => r.Success)
            .SelectMany(r => r.Message!.Attachments)
            .Select(a =>
            {
                byte[] bytes = a.Content.ReadAllBytes();
                a.Content.Dispose();
                return bytes;
            })
            .ToList();

        Assert.Equal(inMemoryAttachments.Count, tempFileAttachments.Count);
        for (int i = 0; i < inMemoryAttachments.Count; i++)
        {
            Assert.Equal(inMemoryAttachments[i], tempFileAttachments[i]);
        }
    }

    [Fact]
    public void Parse_SmallAttachment_StaysInMemory()
    {
        // threshold = long.MaxValue means nothing ever spills to disk
        var parser = new MboxParser(tempFileThresholdBytes: long.MaxValue);
        var attachments = parser.Parse(Fixture("mbox-with-attachments.mbox"))
            .Where(r => r.Success)
            .SelectMany(r => r.Message!.Attachments)
            .ToList();

        Assert.NotEmpty(attachments);
        foreach (var attachment in attachments)
        {
            Assert.False(attachment.Content.IsTempFileBacked,
                $"Attachment '{attachment.FileName}' should be in-memory with threshold=MaxValue");
        }
    }

    [Fact]
    public void Parse_WithTempFileThreshold_SameMessageCountAsDefault()
    {
        var parserDefault = new MboxParser();
        var parserLowThreshold = new MboxParser(tempFileThresholdBytes: 1);

        int defaultCount = parserDefault.Parse(Fixture("mbox-with-attachments.mbox")).Count(r => r.Success);

        int thresholdCount = 0;
        foreach (ParseResult result in parserLowThreshold.Parse(Fixture("mbox-with-attachments.mbox")))
        {
            if (result.Success)
            {
                thresholdCount++;
                foreach (MailAttachment attachment in result.Message!.Attachments)
                    attachment.Content.Dispose();
            }
        }

        Assert.Equal(defaultCount, thresholdCount);
    }

    [Fact]
    public void Constructor_NegativeThreshold_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MboxParser(tempFileThresholdBytes: -1));
    }
}
