// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;

namespace Mbox2Pst.Core.Parsing;

public static class ParserRegistry
{
    private static readonly Dictionary<string, IMailSourceParser> Parsers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mbox"] = new MboxParser(),
        };

    public static IMailSourceParser Get(string sourceType)
    {
        if (Parsers.TryGetValue(sourceType, out IMailSourceParser? parser))
        {
            return parser;
        }

        throw new NotSupportedException($"Unsupported source type: '{sourceType}'");
    }
}
