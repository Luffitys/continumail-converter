// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using Mbox2Pst.Core.Models;

namespace Mbox2Pst.Core.Writing;

public class PlannedMessage
{
    public MailMessage Message { get; set; } = new();
    public string TargetFolderName { get; set; } = string.Empty;
}
