// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Mail2Pst.Core.Cli;
using Xunit;

namespace Mail2Pst.Core.Tests.Cli;

public class CliEventSerializerTests
{
    [Fact]
    public void Serialize_InjectsSchemaVersion_AndPreservesPayload()
    {
        string json = CliEventSerializer.Serialize(new { type = "version", version = "9.9" });
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.Equal(CliEventSerializer.SchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("version", root.GetProperty("type").GetString());
        Assert.Equal("9.9", root.GetProperty("version").GetString());
    }

    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        string json = CliEventSerializer.Serialize(new { type = "scan", totalMessages = 3 });
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetProperty("totalMessages").GetInt32());
    }

    [Fact]
    public void Serialize_Default_IsSingleLine_Indented_IsMultiLine()
    {
        string compact = CliEventSerializer.Serialize(new { type = "scan", totalMessages = 1 });
        string indented = CliEventSerializer.Serialize(new { type = "scan", totalMessages = 1 }, indented: true);

        Assert.DoesNotContain("\n", compact);
        Assert.Contains("\n", indented);
    }
}
