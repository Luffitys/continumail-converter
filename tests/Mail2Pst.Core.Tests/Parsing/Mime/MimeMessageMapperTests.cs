// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Parsing.Mime;
using MimeKit;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing.Mime;

public class MimeMessageMapperTests
{
    private static readonly SourceReference Src = new() { SourcePath = "x.eml", Identifier = "#1" };

    private static MailMessage Map(MimeMessage mime, List<string>? warnings = null) =>
        new MimeMessageMapper().Map(mime, Src, warnings ?? new List<string>());

    [Fact]
    public void Map_CoreFieldsAndBodies()
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Alice", "alice@example.com"));
        mime.To.Add(new MailboxAddress("Bob", "bob@example.com"));
        mime.Cc.Add(new MailboxAddress("Carol", "carol@example.com"));
        mime.Bcc.Add(new MailboxAddress("Dave", "dave@example.com"));
        mime.Subject = "Hello";
        mime.Date = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        mime.Body = new BodyBuilder { TextBody = "plain", HtmlBody = "<p>html</p>" }.ToMessageBody();

        MailMessage m = Map(mime);

        Assert.Equal("Hello", m.Subject);
        Assert.Equal("Alice", m.From!.Name);
        Assert.Equal("alice@example.com", m.From!.Email);
        Assert.Equal("bob@example.com", Assert.Single(m.To).Email);
        Assert.Equal("carol@example.com", Assert.Single(m.Cc).Email);
        Assert.Equal("dave@example.com", Assert.Single(m.Bcc).Email);
        Assert.Equal(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero), m.Date);
        Assert.Equal("plain", m.TextBody);
        Assert.Equal("<p>html</p>", m.HtmlBody);
    }

    [Fact]
    public void Map_ThreadingHeaders_NormalizedWithAngleBrackets()
    {
        var mime = new MimeMessage { Subject = "t" };
        mime.From.Add(new MailboxAddress("A", "a@x.com"));
        mime.MessageId = "msg-1@x.com";
        mime.InReplyTo = "parent@x.com";
        mime.References.Add("r1@x.com");
        mime.References.Add("r2@x.com");
        mime.Body = new TextPart("plain") { Text = "b" };

        MailMessage m = Map(mime);

        Assert.Equal("<msg-1@x.com>", m.MessageId);
        Assert.Equal("<parent@x.com>", m.InReplyTo);
        Assert.Equal("<r1@x.com> <r2@x.com>", m.References);
    }

    [Fact]
    public void Map_RegularAndInlineCidAttachments()
    {
        var builder = new BodyBuilder { HtmlBody = "<img src=\"cid:img1\">" };
        builder.Attachments.Add("doc.txt", Encoding.ASCII.GetBytes("file-bytes"), ContentType.Parse("text/plain"));
        var img = builder.LinkedResources.Add("logo.png", new byte[] { 1, 2, 3 }, ContentType.Parse("image/png"));
        img.ContentId = "img1";
        var mime = new MimeMessage { Subject = "a" };
        mime.From.Add(new MailboxAddress("A", "a@x.com"));
        mime.Body = builder.ToMessageBody();

        MailMessage m = Map(mime);

        MailAttachment regular = Assert.Single(m.Attachments, a => a.FileName == "doc.txt");
        Assert.Equal("text/plain", regular.MimeType);
        Assert.False(regular.IsInline);
        Assert.Equal("file-bytes", Encoding.ASCII.GetString(regular.Content.ReadAllBytes()));

        MailAttachment inline = Assert.Single(m.Attachments, a => a.FileName == "logo.png");
        Assert.True(inline.IsInline);
        Assert.Equal("img1", inline.ContentId);
    }

    [Fact]
    public void Map_ReadState_MozillaStatusGmailLabelsAndDefault()
    {
        var read = new MimeMessage { Subject = "r" };
        read.From.Add(new MailboxAddress("A", "a@x.com"));
        read.Headers.Add("X-Mozilla-Status", "0001");
        read.Body = new TextPart("plain") { Text = "b" };
        Assert.True(Map(read).IsRead);

        var unread = new MimeMessage { Subject = "u" };
        unread.From.Add(new MailboxAddress("A", "a@x.com"));
        unread.Headers.Add("X-Gmail-Labels", "Inbox,Unread");
        unread.Body = new TextPart("plain") { Text = "b" };
        Assert.False(Map(unread).IsRead);

        var bare = new MimeMessage { Subject = "n" };
        bare.From.Add(new MailboxAddress("A", "a@x.com"));
        bare.Body = new TextPart("plain") { Text = "b" };
        Assert.True(Map(bare).IsRead);
    }

    [Fact]
    public void Map_Importance_FromXPriority()
    {
        var mime = new MimeMessage { Subject = "p" };
        mime.From.Add(new MailboxAddress("A", "a@x.com"));
        mime.Headers.Add("X-Priority", "1");
        mime.Body = new TextPart("plain") { Text = "b" };

        Assert.Equal(MailImportance.High, Map(mime).Importance);
    }

    [Fact]
    public void Map_MissingDate_IsNull()
    {
        var mime = new MimeMessage { Subject = "d" };
        mime.From.Add(new MailboxAddress("A", "a@x.com"));
        mime.Body = new TextPart("plain") { Text = "b" };
        while (mime.Headers.Contains(HeaderId.Date)) mime.Headers.Remove(HeaderId.Date);

        Assert.Null(Map(mime).Date);
    }

    [Fact]
    public void Map_LargeAttachment_SpillsToTempFileAndCleansUp()
    {
        var builder = new BodyBuilder { TextBody = "b" };
        byte[] big = Enumerable.Range(0, 2048).Select(i => (byte)(i % 251)).ToArray();
        builder.Attachments.Add("big.bin", big, ContentType.Parse("application/octet-stream"));
        var mime = new MimeMessage { Subject = "s" };
        mime.From.Add(new MailboxAddress("A", "a@x.com"));
        mime.Body = builder.ToMessageBody();

        var mapper = new MimeMessageMapper(tempFileThresholdBytes: 1024); // < 2048
        MailMessage m = mapper.Map(mime, Src, new List<string>());

        MailAttachment att = Assert.Single(m.Attachments);
        Assert.True(att.Content.IsTempFileBacked);
        string tempPath = att.Content.TempPath!;
        Assert.True(File.Exists(tempPath), "temp file must exist before dispose");
        Assert.Equal(big, att.Content.ReadAllBytes());
        att.Content.Dispose();
        Assert.False(File.Exists(tempPath), "temp file must be deleted on dispose");
    }
}
