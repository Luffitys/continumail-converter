// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mbox2Pst.Core;
using Mbox2Pst.Core.Config;
using Mbox2Pst.Core.Progress;
using Mbox2Pst.Core.Scanning;

// ── helpers ──────────────────────────────────────────────────────────────────

static string? Flag(string[] args, string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static List<string> Flags(string[] args, string name)
{
    var values = new List<string>();
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) values.Add(args[i + 1]);
    return values;
}

static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

static void WriteJsonLine(object payload)
{
    Console.WriteLine(JsonSerializer.Serialize(payload,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
}

// ── dispatch ─────────────────────────────────────────────────────────────────

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  continumail-convert convert --config <config.json> --output <dir>");
    Console.Error.WriteLine("  continumail-convert scan    --input <path> [--input <path> ...] [--type mbox]");
    return 1;
}

return args[0] switch
{
    "convert"            => RunConvert(args[1..]),
    "scan"               => RunScan(args[1..]),
    "version" or "--version" or "-v" => PrintVersion(),
    _                    => PrintUnknownCommand(args[0]),
};

// ── convert ───────────────────────────────────────────────────────────────────

static int RunConvert(string[] args)
{
    string? configPath = Flag(args, "--config");
    string? outputDir  = Flag(args, "--output");

    if (configPath is null || outputDir is null)
    {
        Console.Error.WriteLine("Usage: continumail-convert convert --config <config.json> --output <dir>");
        return 1;
    }

    if (!File.Exists(configPath))
    {
        Console.Error.WriteLine($"Config not found: {configPath}");
        return 1;
    }

    Directory.CreateDirectory(outputDir);

    WriteJsonLine(new { type = "started", input = configPath, outputDirectory = outputDir });

    ConversionConfig config;
    try
    {
        config = ConfigLoader.Load(configPath);
    }
    catch (Exception ex)
    {
        WriteJsonLine(new { type = "error", stage = "convert", message = $"Failed to load config: {ex.Message}", fatal = true });
        Console.Error.WriteLine($"Failed to load config: {ex.Message}");
        return 1;
    }

    // The blank PST seed lives embedded in the engine assembly; extract it to a
    // temp file (cleaned up in the finally below). The single-file sidecar is run
    // from a relocated dir with no loose assets/ folder beside it.
    string templatePath = TemplateProvider.ExtractToTempFile();
    var runner = new ConversionRunner(templatePath);

    using var cts = new CancellationTokenSource();

    // Trigger 1 (the Tauri-on-Windows path): a "cancel" line on stdin. Background,
    // non-blocking, and quiet when stdin is closed/redirected/non-interactive.
    var stdinReader = new Thread(() =>
    {
        try
        {
            string? line;
            while ((line = Console.In.ReadLine()) is not null)
            {
                if (string.Equals(line.Trim(), "cancel", StringComparison.OrdinalIgnoreCase))
                {
                    cts.Cancel();
                    return;
                }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }) { IsBackground = true };
    stdinReader.Start();

    // Trigger 2: Ctrl+C / SIGINT. e.Cancel = true prevents the runtime's hard exit
    // so cleanup can run.
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    // Trigger 3: SIGTERM (Unix / manual CLI). ctx.Cancel = true requests graceful stop.
    using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
    {
        ctx.Cancel = true;
        cts.Cancel();
    });

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        var report = runner.Run(config, outputDir, evt =>
        {
            switch (evt)
            {
                case ScanEvent s:
                    WriteJsonLine(new { type = "scan", totalMessages = s.TotalMessages });
                    break;
                case ProgressEvent p:
                    WriteJsonLine(new { type = "progress", converted = p.Converted, total = p.TotalMessages,
                        warnings = p.Warnings, skipped = p.Skipped, bytes = p.EstimatedOutputBytes,
                        currentSource = p.CurrentSource, currentFolder = p.CurrentFolder });
                    break;
                case WarningEvent w:
                    WriteJsonLine(new { type = "warning", source = w.Source, identifier = w.Identifier, reason = w.Reason });
                    break;
            }
        }, cts.Token);

        stopwatch.Stop();

        if (report.Cancelled)
        {
            // Terminal and mutually exclusive with `done`: do not write the success
            // reports. Surface what was deleted and what completed parts remain.
            WriteJsonLine(new
            {
                type = "cancelled",
                deleted = report.DeletedFiles,
                outputs = report.OutputFiles,
                converted = report.ConvertedCount,
                skipped = report.SkippedCount,
                warnings = report.WarningCount,
                elapsedMs = stopwatch.ElapsedMilliseconds,
            });
            return 2;
        }

        string reportJsonPath = Path.Combine(outputDir, "conversion-report.json");
        File.WriteAllText(reportJsonPath, report.ToJson());

        string reportTxtPath = Path.Combine(outputDir, "conversion-report.txt");
        File.WriteAllText(reportTxtPath, report.ToSummary());

        WriteJsonLine(new
        {
            type = "done",
            converted = report.ConvertedCount,
            skipped = report.SkippedCount,
            warnings = report.WarningCount,
            outputs = report.OutputFiles,
            outputDirectory = outputDir,
            report = reportJsonPath,
            elapsedMs = stopwatch.ElapsedMilliseconds,
        });

        return 0;
    }
    catch (ConfigValidationException ex)
    {
        WriteJsonLine(new { type = "error", stage = "convert", message = ex.Message, fatal = true });
        Console.Error.WriteLine($"Config error: {ex.Message}");
        return 1;
    }
    catch (Exception ex)
    {
        WriteJsonLine(new { type = "error", stage = "convert", message = ex.Message, fatal = true });
        Console.Error.WriteLine($"Fatal error: {ex.Message}");
        return 1;
    }
    finally
    {
        try { File.Delete(templatePath); } catch { /* best-effort temp cleanup */ }
    }
}

// ── scan ──────────────────────────────────────────────────────────────────────

static int RunScan(string[] args)
{
    List<string> inputPaths = Flags(args, "--input");
    string sourceType = Flag(args, "--type") ?? "mbox";
    bool streaming = HasFlag(args, "--progress");

    if (inputPaths.Count == 0)
    {
        Console.Error.WriteLine("Usage: continumail-convert scan --input <path> [--input <path> ...] [--type mbox]");
        return 1;
    }

    foreach (string inputPath in inputPaths)
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Input file not found: {inputPath}");
            return 1;
        }
    }

    try
    {
        var scanRunner = new ScanRunner();

        Action<ScanProgress>? onProgress = streaming
            ? p => WriteJsonLine(new { type = "scanProgress", bytes = p.Bytes, totalBytes = p.TotalBytes })
            : null;

        ScanReport report = scanRunner.Scan(inputPaths, sourceType, onProgress);

        static string? Iso(DateTimeOffset? d) =>
            d?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        var output = new
        {
            type = "scan",
            totals = new
            {
                messages = report.Totals.Messages,
                bytes = report.Totals.Bytes,
                sourceBytes = report.Totals.SourceBytes,
                sources = report.Totals.Sources,
            },
            sources = report.Sources.Select(s => new
            {
                id = s.Id,
                path = s.Path,
                displayName = s.DisplayName,
                messages = s.Messages,
                bytes = s.EstimatedBytes,
                sourceBytes = s.SourceBytes,
                dateFrom = Iso(s.DateFrom),
                dateTo = Iso(s.DateTo),
                warnings = s.Warnings,
                skipped = s.Skipped,
            }),
            skipped = report.Skipped.Select(s => new { source = s.SourcePath, identifier = s.Identifier, reason = s.Reason }),
            warnings = report.Warnings.Select(w => new { source = w.SourcePath, identifier = w.Identifier, reason = w.Reason }),
        };

        Console.WriteLine(JsonSerializer.Serialize(output,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = !streaming }));

        return 0;
    }
    catch (NotSupportedException ex)
    {
        Console.Error.WriteLine($"Unsupported source type '{sourceType}': {ex.Message}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Fatal error: {ex.Message}");
        return 1;
    }
}

// ── version ───────────────────────────────────────────────────────────────────

static int PrintVersion()
{
    var asm = System.Reflection.Assembly.GetExecutingAssembly();
    string version =
        asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? asm.GetName().Version?.ToString() ?? "0.0.0";
    int plus = version.IndexOf('+'); // strip build metadata like "0.1.0+abc123"
    if (plus >= 0) version = version[..plus];
    WriteJsonLine(new { type = "version", version, engine = "Mbox2Pst.Cli" });
    return 0;
}

// ── unknown ───────────────────────────────────────────────────────────────────

static int PrintUnknownCommand(string cmd)
{
    Console.Error.WriteLine($"Unknown command '{cmd}'. Use 'convert' or 'scan'.");
    return 1;
}
