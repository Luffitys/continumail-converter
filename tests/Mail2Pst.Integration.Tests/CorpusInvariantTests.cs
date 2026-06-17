// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mail2Pst.Core;
using Mail2Pst.Core.Config;
using Xunit;

namespace Mail2Pst.Integration.Tests;

public class CorpusInvariantTests
{
    // Starts EMPTY. Add explicit, commented entries for known-benign warnings only.
    // Example (do not add until observed):
    //   new Regex("could not decode attachment", RegexOptions.IgnoreCase) // benign; observed on gmail corpus
    private static readonly Regex[] WarningAllowlist = Array.Empty<Regex>();

    [SkippableFact]
    public void RealCorpus_RoundTripInvariantsHold()
    {
        string? configPath = Environment.GetEnvironmentVariable("MAIL2PST_CORPUS_CONFIG");
        Skip.If(string.IsNullOrWhiteSpace(configPath),
            "Set MAIL2PST_CORPUS_CONFIG to a corpus ConversionConfig path to run the local corpus tier.");
        Skip.IfNot(File.Exists(configPath), $"MAIL2PST_CORPUS_CONFIG points to a missing file: {configPath}");

        ConversionConfig config = ConfigLoader.Load(configPath!);

        // Corpus configs list source paths relative to the repo root; the test process runs from
        // the test bin dir, so anchor relative paths to the repo root (else every source would be
        // a spurious missing-source skip). Absolute paths pass through. See RepoPaths.
        foreach (OutputGroupConfig output in config.Outputs)
            foreach (SourceConfig source in output.Sources)
                source.Path = RepoPaths.ResolveAgainstRepoRoot(source.Path);

        string outDir = Path.Combine(Path.GetTempPath(), "mail2pst-corpus-" + Guid.NewGuid());
        Directory.CreateDirectory(outDir);
        try
        {
            var (outputs, report) = RoundTripHarness.Convert(config, outDir);

            // Invariant: nothing skipped.
            Assert.True(report.Skipped.Count == 0, $"expected 0 skips, got {report.Skipped.Count}");

            // Invariant: every warning is explicitly allowlisted.
            foreach (var w in report.Warnings)
                Assert.True(WarningAllowlist.Any(rx => rx.IsMatch(w.Reason)),
                    $"un-allowlisted warning: {w.Reason}");

            var truth = RoundTripHarness.BuildTruth(config);
            IReadOnlyList<ReadFolder> readback = PstReader.Read(outputs);
            Dictionary<string, IReadOnlyList<ReadBackMessage>> byName =
                readback.ToDictionary(f => FolderPathKey.Join(f.Path), f => f.Messages, StringComparer.Ordinal);

            // Invariant: total read-back == converted count.
            int totalRead = readback.Sum(f => f.Messages.Count);
            Assert.Equal(report.ConvertedCount, totalRead);

            // Invariant: per-folder count parity (diagnostic on a missing folder, not KeyNotFoundException).
            foreach (var (folder, msgs) in truth)
            {
                Assert.True(byName.TryGetValue(folder, out IReadOnlyList<ReadBackMessage>? readFolder),
                    $"missing read-back folder '{FolderPathDisplay.FromKey(folder)}'");
                Assert.True(msgs.Count == readFolder!.Count,
                    $"folder '{folder}': parsed {msgs.Count}, read back {readFolder.Count}");
            }

            // Sampled fidelity: first 25 + last 25 per folder.
            RoundTripComparer.AssertSample(truth, readback, head: 25, tail: 25);
        }
        finally { Directory.Delete(outDir, true); }
    }
}
