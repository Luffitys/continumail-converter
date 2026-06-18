// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
namespace Mail2Pst.Core.Models;

public class MailAddress
{
    public string? Name { get; set; }
    public string Email { get; set; } = string.Empty;
}
