// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using Mail2Pst.Core.Cli;

namespace Mail2Pst.Cli;

internal static class CliArgs
{
    internal static string? Flag(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    internal static List<string> Flags(string[] args, string name)
    {
        var values = new List<string>();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) values.Add(args[i + 1]);
        return values;
    }

    internal static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

    internal static void WriteJsonLine(object payload) =>
        Console.WriteLine(CliEventSerializer.Serialize(payload, indented: false));
}
