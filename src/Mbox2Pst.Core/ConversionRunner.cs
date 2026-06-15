// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Mbox2Pst.Core.Config;
using Mbox2Pst.Core.Mapping;
using Mbox2Pst.Core.Models;
using Mbox2Pst.Core.Parsing;
using Mbox2Pst.Core.Progress;
using Mbox2Pst.Core.Reporting;
using Mbox2Pst.Core.Writing;

namespace Mbox2Pst.Core;

/// <summary>
/// Ties together config -> mapping plan -> parsing -> PST writing -> report
/// for a full conversion run.
/// </summary>
public class ConversionRunner
{
    private readonly PstWriter _writer;

    public ConversionRunner(string templatePath, int checkIntervalMessages = 500, int progressIntervalMessages = 25)
    {
        _writer = new PstWriter(templatePath, checkIntervalMessages, progressIntervalMessages);
    }

    public ConversionReport Run(ConversionConfig config, string outputDirectory, Action<ConversionProgressEvent>? onProgress = null, CancellationToken cancellationToken = default)
    {
        // Validate the config up front (output names, duplicates, sizes, sources)
        // so problems fail loudly before any output file is created.
        ConfigValidator.Validate(config);

        var report = new ConversionReport();
        List<PstOutputPlan> plans = MappingEngine.BuildPlan(config);

        // Validate source types eagerly so an unsupported type fails loudly
        // before any output file is created, rather than mid-write.
        foreach (PstOutputPlan plan in plans)
        {
            foreach (SourceMapping mapping in plan.SourceMappings)
            {
                ParserRegistry.Get(mapping.Source.Type);
            }
        }

        int total = 0;
        if (onProgress is not null)
        {
            foreach (PstOutputPlan plan in plans)
            {
                foreach (SourceMapping mapping in plan.SourceMappings)
                {
                    try
                    {
                        total += ParserRegistry.Get(mapping.Source.Type).CountMessages(mapping.Source.Path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // best-effort: missing/unreadable source contributes 0 to the total
                    }
                }
            }
            onProgress(new ScanEvent(total));
        }

        try
        {
            foreach (PstOutputPlan plan in plans)
            {
                IEnumerable<PlannedMessage> plannedMessages = EnumeratePlannedMessages(plan, report, onProgress);
                List<string> outputFiles = _writer.WritePlan(plan, plannedMessages, outputDirectory, report, total, onProgress, cancellationToken);
                report.AddOutputFiles(outputFiles);
            }
        }
        catch (OperationCanceledException)
        {
            // The writer already deleted the in-progress part and recorded both the
            // deletion and any surviving completed parts on the report.
            report.MarkCancelled();
        }

        return report;
    }

    private static IEnumerable<PlannedMessage> EnumeratePlannedMessages(
        PstOutputPlan plan, ConversionReport report, Action<ConversionProgressEvent>? onProgress = null)
    {
        foreach (SourceMapping mapping in plan.SourceMappings)
        {
            IMailSourceParser parser = ParserRegistry.Get(mapping.Source.Type);
            IEnumerator<ParseResult> results = parser.Parse(mapping.Source.Path).GetEnumerator();

            while (true)
            {
                ParseResult result;
                try
                {
                    if (!results.MoveNext())
                    {
                        break;
                    }

                    result = results.Current;
                }
                catch (IOException ex)
                {
                    report.RecordSkipped(
                        new SourceReference { SourcePath = mapping.Source.Path, Identifier = "(source)" },
                        ex.Message);
                    break;
                }

                if (!result.Success)
                {
                    report.RecordSkipped(result.Source, result.Error!);
                    continue;
                }

                foreach (string warning in result.Warnings)
                {
                    report.RecordWarning(result.Source, warning);
                    onProgress?.Invoke(new WarningEvent(result.Source.SourcePath, result.Source.Identifier, warning));
                }

                yield return new PlannedMessage
                {
                    Message = result.Message!,
                    TargetFolderName = mapping.TargetFolderName,
                };
            }
        }
    }
}
