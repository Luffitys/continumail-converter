// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.Linq;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Parsing;
using Xunit;

namespace Mail2Pst.Core.Tests.Parsing;

public class MboxParserMetadataTests
{
    private static MailMessage ParseSingle(string mboxContent)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, mboxContent);
            var parser = new MboxParser();
            var result = parser.Parse(path).First();
            Assert.True(result.Success, result.Error);
            return result.Message!;
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Builds a minimal valid mbox with one message, injecting extraHeaders
    // between the standard From/To/Date headers and the blank separator line.
    private static string MboxMessage(string extraHeaders = "", string body = "body text") =>
        "From sender@example.com Mon Jan 01 00:00:00 2024\r\n" +
        "From: sender@example.com\r\n" +
        "To: recipient@example.com\r\n" +
        "Date: Mon, 01 Jan 2024 00:00:00 +0000\r\n" +
        (extraHeaders.Length > 0 ? extraHeaders + "\r\n" : "") +
        "\r\n" +
        body + "\r\n";

    // --- Threading header tests ---

    [Fact]
    public void Parse_MessageIdPresent_PopulatesMessageId()
    {
        var msg = ParseSingle(MboxMessage("Message-ID: <msg@example.com>"));
        Assert.Equal("<msg@example.com>", msg.MessageId);
    }

    [Fact]
    public void Parse_MessageIdAbsent_MessageIdIsNull()
    {
        var msg = ParseSingle(MboxMessage());
        Assert.Null(msg.MessageId);
    }

    [Fact]
    public void Parse_InReplyToPresent_PopulatesInReplyTo()
    {
        var msg = ParseSingle(MboxMessage("In-Reply-To: <orig@example.com>"));
        Assert.Equal("<orig@example.com>", msg.InReplyTo);
    }

    [Fact]
    public void Parse_InReplyToAbsent_InReplyToIsNull()
    {
        var msg = ParseSingle(MboxMessage());
        Assert.Null(msg.InReplyTo);
    }

    [Fact]
    public void Parse_ReferencesWithTwoIds_PopulatesReferences()
    {
        var msg = ParseSingle(MboxMessage("References: <a@x.com> <b@x.com>"));
        Assert.Equal("<a@x.com> <b@x.com>", msg.References);
    }

    [Fact]
    public void Parse_ReferencesAbsent_ReferencesIsNull()
    {
        var msg = ParseSingle(MboxMessage());
        Assert.Null(msg.References);
    }

    // --- Read/Unread tests ---

    [Fact]
    public void Parse_XGmailLabelsContainsUnread_IsReadFalse()
    {
        var msg = ParseSingle(MboxMessage("X-Gmail-Labels: Inbox,Unread"));
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void Parse_XGmailLabelsContainsDanishUnread_IsReadFalse()
    {
        // Gmail exports labels in the account's display language; Danish "Ulæste" = "Unread"
        var msg = ParseSingle(MboxMessage("X-Gmail-Labels: Indbakke,Kategori: Opdateringer,Ulæste"));
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void Parse_XGmailLabelsNoUnread_IsReadTrue()
    {
        var msg = ParseSingle(MboxMessage("X-Gmail-Labels: Inbox"));
        Assert.True(msg.IsRead);
    }

    [Fact]
    public void Parse_XMozillaStatusReadBitSet_IsReadTrue()
    {
        // 0001 hex = read bit set; no Gmail header
        var msg = ParseSingle(MboxMessage("X-Mozilla-Status: 0001"));
        Assert.True(msg.IsRead);
    }

    [Fact]
    public void Parse_XMozillaStatusReadBitClear_IsReadFalse()
    {
        // 0000 hex = read bit clear; no Gmail header
        var msg = ParseSingle(MboxMessage("X-Mozilla-Status: 0000"));
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void Parse_GmailUnreadWinsOverMozillaStatusRead()
    {
        // Gmail says unread; Mozilla says read (0001) — Gmail wins
        var msg = ParseSingle(MboxMessage("X-Gmail-Labels: Inbox,Unread\r\nX-Mozilla-Status: 0001"));
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void Parse_MalformedMozillaStatusFallsBackToStatusHeader()
    {
        // ZZZZ is not valid hex → falls through to Status: R → IsRead = true
        var msg = ParseSingle(MboxMessage("X-Mozilla-Status: ZZZZ\r\nStatus: R"));
        Assert.True(msg.IsRead);
    }

    [Fact]
    public void Parse_XMozillaStatusNewBitSetReadBitClear_IsReadFalse()
    {
        // 0x10000 is the Mozilla NEW flag, NOT the Read flag (Read = 0x0001). With the NEW
        // bit set but the Read bit clear, the message is UNREAD. This pins that we key off
        // bit 0, disproving the external reviewer's claim that 0x0001 is the NEW/unread bit.
        var msg = ParseSingle(MboxMessage("X-Mozilla-Status: 10000"));
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void Parse_StatusHeaderR_IsReadTrue()
    {
        var msg = ParseSingle(MboxMessage("Status: R"));
        Assert.True(msg.IsRead);
    }

    [Fact]
    public void Parse_StatusHeaderO_IsReadFalse()
    {
        // O = old/seen but not explicitly read
        var msg = ParseSingle(MboxMessage("Status: O"));
        Assert.False(msg.IsRead);
    }

    [Fact]
    public void Parse_NoReadStateHeader_IsReadDefaultsTrue()
    {
        var msg = ParseSingle(MboxMessage());
        Assert.True(msg.IsRead);
    }

    // --- Importance tests ---

    [Fact]
    public void Parse_ImportanceHigh_ImportanceIsHigh()
    {
        var msg = ParseSingle(MboxMessage("Importance: high"));
        Assert.Equal(MailImportance.High, msg.Importance);
    }

    [Fact]
    public void Parse_ImportanceLow_ImportanceIsLow()
    {
        var msg = ParseSingle(MboxMessage("Importance: low"));
        Assert.Equal(MailImportance.Low, msg.Importance);
    }

    [Fact]
    public void Parse_XPriorityFiveOnly_ImportanceIsLow()
    {
        // X-Priority: 5 = NonUrgent; no Importance header → Normal → fallback fires → Low
        var msg = ParseSingle(MboxMessage("X-Priority: 5"));
        Assert.Equal(MailImportance.Low, msg.Importance);
    }

    [Fact]
    public void Parse_ImportanceNormalWithXPriorityOne_ImportanceIsHigh()
    {
        // Importance: normal means "not explicitly set", so X-Priority: 1 (Urgent) fallback fires
        var msg = ParseSingle(MboxMessage("Importance: normal\r\nX-Priority: 1"));
        Assert.Equal(MailImportance.High, msg.Importance);
    }

    [Fact]
    public void Parse_NoImportanceHeader_ImportanceIsNormal()
    {
        var msg = ParseSingle(MboxMessage());
        Assert.Equal(MailImportance.Normal, msg.Importance);
    }

    [Fact]
    public void Parse_XPriorityWithDecoratedText_ImportanceIsHigh()
    {
        // Outlook and some MUAs send "1 (Highest)" — mime.XPriority handles this correctly
        var msg = ParseSingle(MboxMessage("X-Priority: 1 (Highest)"));
        Assert.Equal(MailImportance.High, msg.Importance);
    }
}
