// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mail2Pst.Core.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ConversionConfig Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConversionConfig>(json, Options)
            ?? throw new InvalidDataException($"Config file '{path}' deserialized to null.");
    }
}
