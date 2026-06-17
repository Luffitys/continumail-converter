// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;

namespace Mail2Pst.Core.Config;

/// <summary>
/// Thrown when a <see cref="ConversionConfig"/> (or a value derived from it,
/// such as an output name) fails validation. Carries a user-facing message so
/// the CLI/GUI can surface a clear error before any output is written.
/// </summary>
public class ConfigValidationException : Exception
{
    public ConfigValidationException(string message) : base(message) { }
}
