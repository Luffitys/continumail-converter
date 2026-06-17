// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mail2Pst.Core.Parsing;

/// <summary>
/// Parses a mail source (a file, for mbox/eml/msg) into a sequence of
/// <see cref="ParseResult"/>s. Implementations must catch per-message errors
/// and yield a failed <see cref="ParseResult"/> rather than throwing, so the
/// caller can apply skip-and-log handling.
/// </summary>
public interface IMailSourceParser
{
    // onBytesRead is an optional, best-effort byte-progress callback invoked
    // with the source stream's current read position as each message is yielded.
    // Used by the scan path for a progress bar; convert passes null (unchanged).
    IEnumerable<ParseResult> Parse(string path, Action<long>? onBytesRead = null);

    // Default: slow but correct for any parser. MboxParser overrides with a fast scan.
    int CountMessages(string path) => Parse(path).Count();
}
