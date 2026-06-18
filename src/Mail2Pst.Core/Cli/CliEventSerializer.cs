// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mail2Pst.Core.Cli;

/// <summary>
/// Serializes a CLI JSON-Lines event payload to a single JSON string, injecting
/// the contract's <c>schemaVersion</c> so every emitted event is self-describing
/// (see the CLI event contract in CLAUDE.md). Centralizing this here means no
/// event can omit the version, and the behaviour is unit-testable (the CLI's
/// own WriteJsonLine is a local function in a top-level-statements file).
/// </summary>
public static class CliEventSerializer
{
    /// <summary>Current JSON-Lines contract version. Bump on a breaking change.</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions CamelCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Serialize <paramref name="payload"/> with camelCase property names and a
    /// top-level <c>schemaVersion</c> added. <paramref name="indented"/> controls
    /// pretty-printing (the streaming contract needs single-line; the default
    /// pretty scan object needs indented).
    /// </summary>
    public static string Serialize(object payload, bool indented = false)
    {
        if (JsonSerializer.SerializeToNode(payload, CamelCase) is not JsonObject node)
            throw new InvalidOperationException("CLI event payload did not serialize to a JSON object.");

        node["schemaVersion"] = SchemaVersion;
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }
}
