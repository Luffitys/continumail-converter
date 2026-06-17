// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.Text;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Writing;
using Xunit;

namespace Mail2Pst.Core.Tests.Writing;

public class PstWriterEstimateTests
{
    private static MailMessage Msg(string? subject = null, string? text = null, string? html = null,
        List<MailAttachment>? attachments = null) => new()
    {
        Subject = subject,
        TextBody = text,
        HtmlBody = html,
        Source = new SourceReference { SourcePath = "test.mbox", Identifier = "#1" },
        Attachments = attachments ?? new List<MailAttachment>(),
    };

    [Fact]
    public void HtmlBody_CountedAsUtf8Bytes_NotUtf16Units()
    {
        // Multi-byte mix: é (2 UTF-8 bytes), 中 (3), 𐍈 (4, a surrogate PAIR so .Length == 2).
        // UTF-8 byte count is strictly greater than the .Length the OLD estimate used, so an
        // HTML-only message's estimate must be >= the UTF-8 byte count. Old code returned
        // html.Length (< UTF-8 bytes) → FAILS; new code includes UTF8.GetByteCount(html) → PASSES.
        string html = "<p>café 中 𐍈 test</p>";
        long estimate = PstWriter.EstimateMessageSize(Msg(html: html));

        Assert.True(estimate >= Encoding.UTF8.GetByteCount(html),
            $"HTML must be counted as UTF-8 bytes; estimate {estimate} < UTF-8 {Encoding.UTF8.GetByteCount(html)}");
    }

    [Fact]
    public void Subject_CountedAsUtf16_TwoBytesPerChar()
    {
        // Two messages differing ONLY by a 5-char subject. The per-message overhead cancels in
        // the difference, isolating the subject cost: UTF-16 → 2 bytes/char → delta == 10.
        // Old code counted .Length (×1) → delta == 5 → FAILS.
        long withSubject = PstWriter.EstimateMessageSize(Msg(subject: "ABCDE"));
        long withoutSubject = PstWriter.EstimateMessageSize(Msg(subject: ""));

        Assert.Equal(10, withSubject - withoutSubject);
    }

    [Fact]
    public void EmptyMessage_HasNonZeroStructuralOverhead()
    {
        // A message with no subject/body/attachments still costs PST structural overhead.
        // Old code returned 0 → FAILS; new code returns the per-message overhead constant → PASSES.
        long estimate = PstWriter.EstimateMessageSize(Msg());

        Assert.True(estimate > 0, "empty message must carry per-message structural overhead");
    }

    [Fact]
    public void Attachment_AddsContentBytesPlusOverhead()
    {
        // Adding one 100-byte attachment must add MORE than 100 bytes (content + per-attachment
        // overhead). Old code added exactly content.Length (== 100) → delta == 100 → FAILS the
        // strict-greater assertion; new code adds 100 + overhead → delta > 100 → PASSES.
        var attachment = new MailAttachment
        {
            FileName = "a.bin",
            MimeType = "application/octet-stream",
            Content = AttachmentContent.FromBytes(new byte[100]),
        };
        long withAttachment = PstWriter.EstimateMessageSize(
            Msg(subject: "X", attachments: new List<MailAttachment> { attachment }));
        long withoutAttachment = PstWriter.EstimateMessageSize(Msg(subject: "X"));

        Assert.True(withAttachment - withoutAttachment > 100,
            $"attachment must add content bytes + overhead; delta was {withAttachment - withoutAttachment}");
    }
}
