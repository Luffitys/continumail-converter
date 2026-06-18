// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Mail2Pst.Core;
using Mail2Pst.Core.Models;
using Xunit;

namespace Mail2Pst.Integration.Tests;

/// <summary>
/// Format-agnostic round-trip assertions: PST read-back must match the parser's MailMessage truth.
/// Touches only MailMessage / ReadBackMessage, so it applies to any future source format.
/// </summary>
public static class RoundTripComparer
{
    public static void AssertRoundTrip(
        IReadOnlyDictionary<string, List<MailMessage>> truth, IReadOnlyList<ReadFolder> readback)
    {
        Dictionary<string, IReadOnlyList<ReadBackMessage>> actualByName =
            readback.ToDictionary(f => FolderPathKey.Join(f.Path), f => f.Messages, StringComparer.Ordinal);

        Assert.Equal(
            truth.Keys.OrderBy(k => k, StringComparer.Ordinal),
            actualByName.Keys.OrderBy(k => k, StringComparer.Ordinal));

        foreach ((string folder, List<MailMessage> truthMsgs) in truth)
        {
            string display = FolderPathDisplay.FromKey(folder);
            IReadOnlyList<ReadBackMessage> actual = actualByName[folder];
            Assert.True(truthMsgs.Count == actual.Count,
                $"Folder '{display}': expected {truthMsgs.Count} messages, read back {actual.Count}");

            foreach ((MailMessage t, ReadBackMessage a) in Match(truthMsgs, actual))
                AssertMessage(display, t, a);
        }
    }

    /// <summary>Folder-set parity + per-folder count parity + per-message asserts on a sample (first head + last tail per folder).</summary>
    public static void AssertSample(
        IReadOnlyDictionary<string, List<MailMessage>> truth, IReadOnlyList<ReadFolder> readback, int head, int tail)
    {
        Dictionary<string, IReadOnlyList<ReadBackMessage>> actualByName =
            readback.ToDictionary(f => FolderPathKey.Join(f.Path), f => f.Messages, StringComparer.Ordinal);

        // Folder-set parity — also catches an *extra* read-back folder, so AssertSample is a safe
        // standalone invariant, not only "every truth folder exists".
        Assert.Equal(
            truth.Keys.OrderBy(k => k, StringComparer.Ordinal),
            actualByName.Keys.OrderBy(k => k, StringComparer.Ordinal));

        foreach ((string folder, List<MailMessage> truthMsgs) in truth)
        {
            string display = FolderPathDisplay.FromKey(folder);
            Assert.True(actualByName.TryGetValue(folder, out IReadOnlyList<ReadBackMessage>? actual),
                $"Missing read-back folder '{display}'");
            // Count parity is required before positional matching, else Match silently drops extras.
            Assert.True(truthMsgs.Count == actual!.Count,
                $"Folder '{display}': expected {truthMsgs.Count} messages, read back {actual.Count}");

            var sample = new HashSet<MailMessage>(truthMsgs.Take(head).Concat(truthMsgs.TakeLast(tail)));
            foreach ((MailMessage t, ReadBackMessage a) in Match(truthMsgs, actual))
                if (sample.Contains(t))
                    AssertMessage(display, t, a);
        }
    }

    private static IEnumerable<(MailMessage, ReadBackMessage)> Match(
        List<MailMessage> truth, IReadOnlyList<ReadBackMessage> actual)
    {
        // Bracket-insensitive: the parser stores Message-IDs angle-bracketed (EnsureAngleBrackets),
        // so both sides are bracketed today, but strip brackets defensively for future formats.
        static string? Norm(string? id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim().Trim('<', '>');

        Dictionary<string, MailMessage> truthById = truth
            .Where(m => Norm(m.MessageId) != null)
            .GroupBy(m => Norm(m.MessageId)!, StringComparer.Ordinal)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        Dictionary<string, ReadBackMessage> actualById = actual
            .Where(m => Norm(m.MessageId) != null)
            .GroupBy(m => Norm(m.MessageId)!, StringComparer.Ordinal)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);

        var usedTruth = new HashSet<MailMessage>();
        var usedActual = new HashSet<ReadBackMessage>();
        var pairs = new List<(MailMessage, ReadBackMessage)>();

        foreach (KeyValuePair<string, MailMessage> kv in truthById)
            if (actualById.TryGetValue(kv.Key, out ReadBackMessage? a))
            {
                pairs.Add((kv.Value, a));
                usedTruth.Add(kv.Value);
                usedActual.Add(a);
            }

        List<MailMessage> remTruth = truth.Where(m => !usedTruth.Contains(m)).ToList();
        List<ReadBackMessage> remActual = actual.Where(m => !usedActual.Contains(m)).ToList();
        for (int i = 0; i < remTruth.Count && i < remActual.Count; i++)
            pairs.Add((remTruth[i], remActual[i]));

        return pairs;
    }

    private static void AssertMessage(string folder, MailMessage t, ReadBackMessage a)
    {
        string id = t.MessageId ?? t.Subject ?? "(unknown)";

        Assert.True((t.Subject ?? "") == (a.Subject ?? ""),
            $"[{folder}/{id}] subject: '{t.Subject}' != '{a.Subject}'");

        string? from = t.From?.Email;
        if (!string.IsNullOrEmpty(from))
            Assert.True(string.Equals(from, a.FromAddress, StringComparison.OrdinalIgnoreCase),
                $"[{folder}/{id}] from: '{from}' != '{a.FromAddress}'");

        List<(string, RecipientKind)> tRecip = t.To.Select(x => (x.Email.ToLowerInvariant(), RecipientKind.To))
            .Concat(t.Cc.Select(x => (x.Email.ToLowerInvariant(), RecipientKind.Cc)))
            .Concat(t.Bcc.Select(x => (x.Email.ToLowerInvariant(), RecipientKind.Bcc)))
            .OrderBy(x => x).ToList();
        List<(string, RecipientKind)> aRecip = a.Recipients
            .Select(x => (x.Address.ToLowerInvariant(), x.Kind)).OrderBy(x => x).ToList();
        Assert.True(tRecip.SequenceEqual(aRecip),
            $"[{folder}/{id}] recipients: [{string.Join(",", tRecip)}] != [{string.Join(",", aRecip)}]");

        if (t.Date.HasValue)
        {
            long expected = new DateTimeOffset(t.Date.Value.UtcDateTime).ToUnixTimeSeconds();
            Assert.True(a.Date.HasValue, $"[{folder}/{id}] date missing on read-back");
            Assert.True(expected == a.Date!.Value.ToUnixTimeSeconds(), $"[{folder}/{id}] date mismatch");
        }

        List<string> tAtt = t.Attachments.Where(x => !x.IsInline).Select(x => x.FileName).OrderBy(x => x).ToList();
        List<string> aAtt = a.AttachmentNames.OrderBy(x => x).ToList();
        Assert.True(tAtt.SequenceEqual(aAtt),
            $"[{folder}/{id}] attachments: [{string.Join(",", tAtt)}] != [{string.Join(",", aAtt)}]");

        bool tHasBody = !string.IsNullOrWhiteSpace(t.TextBody) || !string.IsNullOrWhiteSpace(t.HtmlBody);
        if (tHasBody)
            Assert.True(a.HasNonEmptyBody, $"[{folder}/{id}] body empty on read-back");
    }
}
