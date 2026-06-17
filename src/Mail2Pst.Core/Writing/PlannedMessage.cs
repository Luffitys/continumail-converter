// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using Mail2Pst.Core.Models;

namespace Mail2Pst.Core.Writing;

public class PlannedMessage
{
    public MailMessage Message { get; set; } = new();
    public IReadOnlyList<string> TargetFolderPath { get; set; } = System.Array.Empty<string>();
}
