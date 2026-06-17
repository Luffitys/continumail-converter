// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Mail2Pst.Core.Parsing.Mbox;

/// <summary>
/// Shared mbox "From " envelope-postmark detection. Extracted verbatim from MboxParser so the
/// parser and discovery's content-sniff use ONE implementation — "discovery says mbox" can never
/// disagree with "parser says mbox". Matches the asctime form, e.g.
/// "From sender@host Mon Jan  1 00:00:00 2020" (optional timezone token before the year), with
/// day-of-week and month validated against the English abbreviations (exact case).
/// </summary>
internal static class MboxPostmark
{
    private static readonly Regex EnvelopePostmark = new Regex(
        @"^From \S+ (Mon|Tue|Wed|Thu|Fri|Sat|Sun) (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2} \d{2}:\d{2}:\d{2}(\s+\S+)? \d{4}\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static bool IsEnvelopePostmark(ReadOnlySpan<byte> line)
    {
        // From lines are ASCII; decode and drop the trailing newline before matching.
        string text = Encoding.ASCII.GetString(line).TrimEnd('\r', '\n');
        return EnvelopePostmark.IsMatch(text);
    }
}
