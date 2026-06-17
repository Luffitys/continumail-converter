// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using Mail2Pst.Core.Models;

namespace Mail2Pst.Core.Parsing;

/// <summary>
/// The result of attempting to parse a single message from a source.
/// Either <see cref="Message"/> is set (success) or <see cref="Error"/> is set (skip-and-log).
/// </summary>
public class ParseResult
{
    public MailMessage? Message { get; private init; }
    public string? Error { get; private init; }
    public SourceReference Source { get; private init; } = new();

    /// <summary>
    /// Non-fatal issues encountered while parsing a successful message (e.g. an
    /// attachment that could not be extracted). Empty unless populated by the parser.
    /// </summary>
    public List<string> Warnings { get; private init; } = new();

    public bool Success => Error is null;

    public static ParseResult Ok(MailMessage message, List<string>? warnings = null) =>
        new() { Message = message, Source = message.Source, Warnings = warnings ?? new List<string>() };

    public static ParseResult Failed(SourceReference source, string error) =>
        new() { Source = source, Error = error };
}
