// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Parsing;
using MimeKit;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing;

public class MboxParserTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.mbox");

    [Fact]
    public void Parse_ReturnsBothMessagesWithExpectedFields()
    {
        var parser = new MboxParser();

        var results = parser.Parse(FixturePath).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));

        var first = results[0].Message!;
        Assert.Equal("Hello from Alice", first.Subject);
        Assert.Equal("alice@example.com", first.From!.Email);
        Assert.Single(first.To);
        Assert.Equal("bob@example.com", first.To[0].Email);
        Assert.Contains("This is the first test message.", first.TextBody);
        Assert.Equal("sample.mbox", Path.GetFileName(first.Source.SourcePath));
        Assert.Equal("message #1", first.Source.Identifier);

        var second = results[1].Message!;
        Assert.Equal("Second message", second.Subject);
        Assert.Equal("carol@example.com", second.From!.Email);
        Assert.Equal(2, second.To.Count);
        Assert.Equal("bob@example.com", second.To[0].Email);
        Assert.Equal("dave@example.com", second.To[1].Email);
        Assert.Single(second.Cc);
        Assert.Equal("eve@example.com", second.Cc[0].Email);
        Assert.Equal("message #2", second.Source.Identifier);
    }

    [Fact]
    public void Parse_NonExistentFile_Throws()
    {
        var parser = new MboxParser();

        Assert.Throws<FileNotFoundException>(() => parser.Parse("does-not-exist.mbox").ToList());
    }

    [Fact]
    public void Parse_FileWithMimeKitMboxEofBug_ReturnsBothMessages()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-eof-bug.mbox");

        var results = parser.Parse(path).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success, r.Error));
        Assert.Equal("pakke", results[0].Message!.Subject);
        Assert.Equal("message #1", results[0].Message!.Source.Identifier);

        // The trap lines must stay in message 1's body, not be treated as structure.
        string body0 = results[0].Message!.TextBody ?? string.Empty;
        Assert.Contains("From here on", body0);            // From-shaped body line not split
        Assert.Contains("--not-a-real-boundary", body0);   // pseudo-boundary stayed in body
        Assert.Contains("From escaped: originally", body0); // mboxrd >From un-escaped (#1)

        // Message 2 must survive intact (not swallowed into message 1).
        Assert.Equal("second message", results[1].Message!.Subject);
        Assert.Equal("message #2", results[1].Message!.Source.Identifier);
    }

    [Fact]
    public void MimeFormatMbox_MisparsesFixture_ProvingTheHazardWeAvoid()
    {
        // MimeKit's own mbox parser (MimeFormat.Mbox) is fooled by the trap content in
        // this fixture (observed on MimeKit 4.17.0). The engine avoids it by splitting
        // messages itself (SplitMessages) and parsing each with MimeFormat.Entity.
        // NOTE: this proof is pinned to MimeKit 4.17.0's behavior. If a future MimeKit
        // upgrade FIXES its mbox parser, this test will go red — that's a welcome signal,
        // not a defect: update the assertion to the new behavior (or retire this proof).
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-eof-bug.mbox");
        using var stream = File.OpenRead(path);
        var parser = new MimeKit.MimeParser(stream, MimeKit.MimeFormat.Mbox);
        // PIN to the EXACT observed wrong behavior: MimeFormat.Mbox parses message 1 OK
        // (count=1, subject="pakke") but then throws FormatException on the second
        // ParseMessage() call ("Failed to parse message headers") — the "From here on..."
        // trap line in message 1's body caused mis-splitting that corrupts message 2's parse.
        var subjects = new List<string?>();
        Assert.Throws<FormatException>(() =>
        {
            while (!parser.IsEndOfStream) { subjects.Add(parser.ParseMessage().Subject); }
        });
        string subject0 = Assert.Single(subjects)!;
        Assert.Equal("pakke", subject0);
    }

    [Fact]
    public void Parse_MultipartMixedWithAttachment_ExtractsAttachment()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-with-attachments.mbox");

        var results = parser.Parse(path).ToList();
        Assert.All(results, r => Assert.True(r.Success, r.Error));

        MailMessage message = results[0].Message!;
        Assert.Contains("Body text for message 1.", message.TextBody);
        Assert.Single(message.Attachments);

        MailAttachment attachment = message.Attachments[0];
        Assert.Equal("hello.txt", attachment.FileName);
        Assert.Equal("text/plain", attachment.MimeType);
        Assert.Equal("Attachment content here.\n", Encoding.UTF8.GetString(attachment.Content.ReadAllBytes()));
    }

    [Fact]
    public void Parse_MultipartAlternative_HasNoAttachments()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-with-attachments.mbox");

        var results = parser.Parse(path).ToList();
        Assert.All(results, r => Assert.True(r.Success, r.Error));

        MailMessage message = results[1].Message!;
        Assert.Empty(message.Attachments);
        Assert.Contains("Plain version.", message.TextBody);
        Assert.Contains("HTML version.", message.HtmlBody);
    }

    [Fact]
    public void Parse_TextPlainAttachment_IsPreservedAsAttachment()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-with-attachments.mbox");

        var results = parser.Parse(path).ToList();
        Assert.All(results, r => Assert.True(r.Success, r.Error));

        MailMessage message = results[2].Message!;
        Assert.Contains("See attached notes.", message.TextBody);
        Assert.Single(message.Attachments);

        MailAttachment attachment = message.Attachments[0];
        Assert.Equal("notes.txt", attachment.FileName);
        Assert.Equal("These are my notes.\n", Encoding.UTF8.GetString(attachment.Content.ReadAllBytes()));
    }

    [Fact]
    public void Parse_InlineCidImageWithFilename_IsMarkedInlineWithNormalizedContentId()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-with-attachments.mbox");

        var results = parser.Parse(path).ToList();
        Assert.All(results, r => Assert.True(r.Success, r.Error));

        MailMessage message = results[3].Message!;
        Assert.Contains("cid:test-image", message.HtmlBody);
        Assert.Single(message.Attachments);

        MailAttachment attachment = message.Attachments[0];
        Assert.Equal("pic.png", attachment.FileName);
        Assert.Equal("image/png", attachment.MimeType);
        Assert.True(attachment.IsInline);
        Assert.Equal("test-image", attachment.ContentId);
    }

    [Fact]
    public void Parse_AttachedEmail_IsExtractedAsEml()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-with-attachments.mbox");

        var results = parser.Parse(path).ToList();
        Assert.All(results, r => Assert.True(r.Success, r.Error));

        MailMessage message = results[4].Message!;
        Assert.Contains("See attached email.", message.TextBody);
        Assert.Single(message.Attachments);

        MailAttachment attachment = message.Attachments[0];
        Assert.Equal("attached-message.eml", attachment.FileName);
        Assert.Equal("message/rfc822", attachment.MimeType);
        Assert.Contains("Subject: Inner message", Encoding.UTF8.GetString(attachment.Content.ReadAllBytes()));
    }

    [Fact]
    public void Parse_InlineCidImageWithoutFilename_GeneratesFileName()
    {
        var parser = new MboxParser();
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "mbox-with-attachments.mbox");

        var results = parser.Parse(path).ToList();
        Assert.All(results, r => Assert.True(r.Success, r.Error));

        MailMessage message = results[5].Message!;
        Assert.Contains("cid:logo123", message.HtmlBody);
        Assert.Single(message.Attachments);

        MailAttachment attachment = message.Attachments[0];
        Assert.False(string.IsNullOrEmpty(attachment.FileName));
        Assert.EndsWith(".png", attachment.FileName);
        Assert.Equal("image/png", attachment.MimeType);
        Assert.True(attachment.IsInline);
        Assert.Equal("logo123", attachment.ContentId);
        Assert.Equal("LOGOBYTES45", Encoding.UTF8.GetString(attachment.Content.ReadAllBytes()));
    }
}
