// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using Mbox2Pst.Core.Config;

namespace Mbox2Pst.Core.Mapping;

public class SourceMapping
{
    public SourceConfig Source { get; set; } = new();
    public string TargetFolderName { get; set; } = string.Empty;
}
