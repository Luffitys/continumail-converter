// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using Mbox2Pst.Core.Models;
using Xunit;

namespace Mbox2Pst.Core.Tests.Models;

public class MailMessageTests
{
    [Fact]
    public void NewMailMessage_HasEmptyRecipientListsAndNullOptionalFields()
    {
        var message = new MailMessage();

        Assert.Null(message.Subject);
        Assert.Null(message.From);
        Assert.Empty(message.To);
        Assert.Empty(message.Cc);
        Assert.Empty(message.Bcc);
        Assert.Null(message.Date);
        Assert.Null(message.TextBody);
        Assert.Null(message.HtmlBody);
        Assert.NotNull(message.Source);
        Assert.Empty(message.Attachments);
    }

    [Fact]
    public void MailAddress_StoresNameAndEmail()
    {
        var address = new MailAddress { Name = "Alice Example", Email = "alice@example.com" };

        Assert.Equal("Alice Example", address.Name);
        Assert.Equal("alice@example.com", address.Email);
    }
}
