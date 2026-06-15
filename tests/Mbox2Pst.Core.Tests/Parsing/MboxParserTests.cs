// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mbox2Pst.Core.Models;
using Mbox2Pst.Core.Parsing;
using MimeKit;
using Xunit;

namespace Mbox2Pst.Core.Tests.Parsing;

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
        Assert.Equal("second message", results[1].Message!.Subject);
        Assert.Equal("message #2", results[1].Message!.Source.Identifier);
    }

    [Fact]
    public void ExtractAttachments_PartWithNoContent_RecordsWarningAndSkipsAttachment()
    {
        var mime = new MimeMessage();
        var multipart = new Multipart("mixed");
        multipart.Add(new TextPart("plain") { Text = "body" });

        var brokenPart = new MimePart("application", "octet-stream")
        {
            FileName = "broken.bin",
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
        };
        multipart.Add(brokenPart);
        mime.Body = multipart;

        var warnings = new List<string>();

        List<MailAttachment> attachments = new MboxParser().ExtractAttachments(mime, warnings);

        Assert.Empty(attachments);
        Assert.Single(warnings);
        Assert.Contains("broken.bin", warnings[0]);
        Assert.Contains("application/octet-stream", warnings[0]);
    }

    [Fact]
    public void ExtractAttachments_MultipartAlternativeBodyPartsWithContentId_ProducesNoAttachments()
    {
        // Regression: LinkedIn-style emails use multipart/alternative with
        // Content-ID headers on the text/plain and text/html body parts as
        // labels (e.g. "Content-ID: text-body"). These must NOT be classified
        // as attachments — they are body content, not inline resources.
        var mime = new MimeMessage();
        var alternative = new MultipartAlternative();

        var plainPart = new TextPart("plain") { Text = "plain body" };
        plainPart.ContentId = "text-body";

        var htmlPart = new TextPart("html") { Text = "<p>html body</p>" };
        htmlPart.ContentId = "html-body";

        alternative.Add(plainPart);
        alternative.Add(htmlPart);
        mime.Body = alternative;

        var warnings = new List<string>();

        List<MailAttachment> attachments = new MboxParser().ExtractAttachments(mime, warnings);

        Assert.Empty(attachments);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ExtractAttachments_InlineDispositionWithoutContentId_IsInlineWithNullContentId()
    {
        var mime = new MimeMessage();
        var multipart = new Multipart("mixed");
        multipart.Add(new TextPart("plain") { Text = "body" });

        var inlinePart = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("inline data"))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
        };
        multipart.Add(inlinePart);
        mime.Body = multipart;

        var warnings = new List<string>();

        List<MailAttachment> attachments = new MboxParser().ExtractAttachments(mime, warnings);

        Assert.Empty(warnings);
        Assert.Single(attachments);
        MailAttachment attachment = attachments[0];
        Assert.True(attachment.IsInline);
        Assert.Null(attachment.ContentId);
    }

    [Fact]
    public void ExtractAttachments_PartWithContentLocation_PopulatesContentLocation()
    {
        var mime = new MimeMessage();
        var multipart = new Multipart("mixed");
        multipart.Add(new TextPart("plain") { Text = "body" });

        const string location = "http://example.com/images/logo.png";
        var part = new MimePart("image", "png")
        {
            FileName = "logo.png",
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("PNGDATA"))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentLocation = new Uri(location),
        };
        multipart.Add(part);
        mime.Body = multipart;

        var warnings = new List<string>();

        List<MailAttachment> attachments = new MboxParser().ExtractAttachments(mime, warnings);

        Assert.Empty(warnings);
        Assert.Single(attachments);
        Assert.Equal(location, attachments[0].ContentLocation);
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

    [Fact]
    public void ExtractAttachments_EmbeddedMessageInMultipartReport_ProducesNoAttachments()
    {
        // NDR/bounce messages wrap the original message in multipart/report + message/rfc822.
        // Gmail hides these as system messages; we must not surface them as phantom attachments.
        var original = new MimeMessage();
        original.Subject = "Original";
        original.Body = new TextPart("plain") { Text = "original body" };

        var report = new Multipart("report");
        report.Add(new TextPart("plain") { Text = "Delivery failed." });

        var deliveryStatus = new MimePart("message", "delivery-status")
        {
            Content = new MimeContent(new System.IO.MemoryStream(System.Text.Encoding.ASCII.GetBytes(
                "Final-Recipient: rfc822; user@example.com\r\nAction: failed\r\n"))),
        };
        report.Add(deliveryStatus);

        var embedded = new MessagePart { Message = original };
        report.Add(embedded);

        var mime = new MimeMessage();
        mime.Body = report;

        var warnings = new List<string>();
        List<MailAttachment> attachments = new MboxParser().ExtractAttachments(mime, warnings);

        Assert.Empty(attachments);
        Assert.Empty(warnings);
    }
}
