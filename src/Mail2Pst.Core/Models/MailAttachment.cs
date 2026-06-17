// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
namespace Mail2Pst.Core.Models;

public class MailAttachment
{
    public string FileName { get; set; } = "attachment";
    public string MimeType { get; set; } = "application/octet-stream";
    public string? ContentId { get; set; }
    public string? ContentLocation { get; set; }
    public bool IsInline { get; set; }
    public AttachmentContent Content { get; set; } = AttachmentContent.FromBytes([]);
}
