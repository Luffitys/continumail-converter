// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
namespace Mail2Pst.Core.Reporting;

public class SkippedMessage
{
    public string SourcePath { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
