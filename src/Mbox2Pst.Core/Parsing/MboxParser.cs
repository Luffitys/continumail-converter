// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mbox2Pst.Core.Models;
using MimeKit;

namespace Mbox2Pst.Core.Parsing;

public class MboxParser : IMailSourceParser
{
    private static readonly byte[] FromMarker = Encoding.ASCII.GetBytes("From ");
    private const int BufferSize = 81920;

    private readonly long _tempFileThresholdBytes;

    public MboxParser(long tempFileThresholdBytes = 4L * 1024 * 1024)
    {
        if (tempFileThresholdBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(tempFileThresholdBytes), tempFileThresholdBytes, "Temp-file threshold must be non-negative.");
        _tempFileThresholdBytes = tempFileThresholdBytes;
    }

    public IEnumerable<ParseResult> Parse(string path, Action<long>? onBytesRead = null)
    {
        using FileStream stream = File.OpenRead(path);

        int index = 0;
        foreach (byte[] messageBytes in SplitMessages(stream, onBytesRead))
        {
            index++;
            var sourceRef = new SourceReference
            {
                SourcePath = path,
                Identifier = $"message #{index}",
            };

            MimeMessage? mime = null;
            string? error = null;
            try
            {
                mime = ParseMimeMessage(messageBytes);
            }
            catch (Exception ex) when (ex is FormatException or IOException)
            {
                // Expected, per-message parse failures (malformed MIME / stream
                // error): record as a skip and continue. Any other exception is
                // an unexpected defect and is allowed to propagate so it surfaces
                // loudly instead of silently dropping mail.
                error = ex.Message;
            }

            if (error is not null)
            {
                yield return ParseResult.Failed(sourceRef, error);
                continue;
            }

            var warnings = new List<string>();
            MailMessage message = ToMailMessage(mime!, sourceRef, warnings);
            yield return ParseResult.Ok(message, warnings.Count > 0 ? warnings : null);
        }
    }

    /// <summary>
    /// Parses one message's raw bytes into a <see cref="MimeMessage"/>. Per
    /// MimeKit, throws <see cref="FormatException"/> for malformed MIME and
    /// <see cref="IOException"/> for stream errors; <see cref="Parse"/> treats
    /// only those as a per-message skip and lets anything else propagate.
    /// Virtual so tests can substitute the parse step.
    /// </summary>
    protected virtual MimeMessage ParseMimeMessage(byte[] messageBytes)
    {
        using var messageStream = new MemoryStream(messageBytes);
        var entityParser = new MimeParser(messageStream, MimeFormat.Entity);
        return entityParser.ParseMessage();
    }

    public int CountMessages(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return CountBoundaries(stream);
    }

    /// <summary>
    /// Counts the number of messages in an mbox stream by scanning for "From " boundary
    /// lines, using the same rule as <see cref="SplitMessages"/> via the shared
    /// <see cref="IsMessageBoundary"/> helper: a line starting with "From " is a boundary
    /// if the previous line was blank (or it is the first line), OR the line itself matches
    /// the envelope postmark shape. Runs in O(n) time with a small, fixed read buffer and
    /// minimal per-line allocation (one pooled MemoryStream, re-used across lines).
    /// </summary>
    private static int CountBoundaries(Stream rawStream)
    {
        var buffer = new byte[BufferSize];
        using var line = new MemoryStream(256);
        bool previousLineWasBlank = true;
        int count = 0;

        int bytesRead;
        while ((bytesRead = rawStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            int offset = 0;
            while (offset < bytesRead)
            {
                int newlineIndex = Array.IndexOf(buffer, (byte)'\n', offset, bytesRead - offset);
                if (newlineIndex == -1)
                {
                    line.Write(buffer, offset, bytesRead - offset);
                    break;
                }

                int lineLength = newlineIndex - offset + 1;
                line.Write(buffer, offset, lineLength);
                offset = newlineIndex + 1;

                ReadOnlySpan<byte> lineSpan = line.GetBuffer().AsSpan(0, (int)line.Length);
                if (IsMessageBoundary(lineSpan, previousLineWasBlank))
                    count++;
                previousLineWasBlank = IsBlankLine(lineSpan);
                line.SetLength(0);
            }
        }

        // Flush the final line if the file doesn't end with '\n'.
        if (line.Length > 0)
        {
            ReadOnlySpan<byte> lineSpan = line.GetBuffer().AsSpan(0, (int)line.Length);
            if (IsMessageBoundary(lineSpan, previousLineWasBlank))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Splits an mbox file into the raw bytes of each contained message.
    /// A message boundary is a line that starts with the literal "From "
    /// marker and is either the first line of the file or immediately
    /// follows a blank line (the mboxrd convention used by Gmail Takeout,
    /// where in-body lines starting with "From " are escaped as ">From ").
    /// The marker line itself is not included in the returned message bytes.
    /// This avoids MimeKit's <see cref="MimeFormat.Mbox"/> parser, whose
    /// end-of-stream detection can be fooled by message bodies that happen
    /// to contain text resembling a MIME boundary, causing it to silently
    /// stop before the end of the file.
    /// </summary>
    private static IEnumerable<byte[]> SplitMessages(Stream rawStream, Action<long>? onBytesRead = null)
    {
        var buffer = new byte[BufferSize];
        var current = new MemoryStream();
        var line = new MemoryStream();
        bool previousLineWasBlank = true;
        bool currentHasContent = false;

        int bytesRead;
        while ((bytesRead = rawStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            int offset = 0;
            while (offset < bytesRead)
            {
                int newlineIndex = Array.IndexOf(buffer, (byte)'\n', offset, bytesRead - offset);
                if (newlineIndex == -1)
                {
                    line.Write(buffer, offset, bytesRead - offset);
                    break;
                }

                int lineLength = newlineIndex - offset + 1;
                line.Write(buffer, offset, lineLength);
                offset = newlineIndex + 1;

                byte[] lineBytes = line.ToArray();
                line.SetLength(0);

                bool isBoundary = IsMessageBoundary(lineBytes, previousLineWasBlank);
                if (isBoundary)
                {
                    if (currentHasContent)
                    {
                        onBytesRead?.Invoke(rawStream.Position);
                        yield return current.ToArray();
                        current = new MemoryStream();
                        currentHasContent = false;
                    }
                }
                else
                {
                    byte[] contentBytes = UnescapeFromLine(lineBytes);
                    current.Write(contentBytes, 0, contentBytes.Length);
                    currentHasContent = true;
                }

                previousLineWasBlank = IsBlankLine(lineBytes);
            }
        }

        if (line.Length > 0)
        {
            byte[] lineBytes = line.ToArray();
            bool isBoundary = IsMessageBoundary(lineBytes, previousLineWasBlank);
            if (!isBoundary)
            {
                byte[] contentBytes = UnescapeFromLine(lineBytes);
                current.Write(contentBytes, 0, contentBytes.Length);
                currentHasContent = true;
            }
        }

        if (currentHasContent)
        {
            onBytesRead?.Invoke(rawStream.Position);
            yield return current.ToArray();
        }
    }

    // Matches the mbox "From " postmark / envelope line in asctime form, e.g.
    // "From sender@host Mon Jan  1 00:00:00 2020" (optional timezone token before
    // the year). Specific enough that ordinary body lines beginning "From " (e.g.
    // "From now on ...") do not match, so it is a safe fallback boundary signal
    // for files whose messages are not separated by a blank line.
    private static readonly Regex EnvelopePostmark = new Regex(
        @"^From \S+ \w{3} \w{3}\s+\d{1,2} \d{2}:\d{2}:\d{2}(\s+\S+)? \d{4}\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static bool StartsWithMarkerAt(ReadOnlySpan<byte> line, int index, ReadOnlySpan<byte> marker)
    {
        if (line.Length - index < marker.Length)
        {
            return false;
        }

        for (int i = 0; i < marker.Length; i++)
        {
            if (line[index + i] != marker[i])
            {
                return false;
            }
        }

        return true;
    }

    // mboxrd un-escaping: a body line that originally matched ^>*From  was stored
    // with one extra leading '>' to distinguish it from a real envelope boundary.
    // Strip exactly one '>' from any line of the form ^>+From  to restore the
    // original text. Non-escaped lines are returned unchanged (no copy).
    private static byte[] UnescapeFromLine(byte[] line)
    {
        int gt = 0;
        while (gt < line.Length && line[gt] == (byte)'>')
            gt++;

        if (gt == 0 || !StartsWithMarkerAt(line, gt, FromMarker))
            return line;

        var result = new byte[line.Length - 1];
        Array.Copy(line, 1, result, 0, line.Length - 1);
        return result;
    }

    private static bool StartsWithFromMarker(ReadOnlySpan<byte> line) =>
        StartsWithMarkerAt(line, 0, FromMarker);

    private static bool IsEnvelopePostmark(ReadOnlySpan<byte> line)
    {
        // From lines are ASCII; decode and drop the trailing newline before matching.
        // Only reached for "From " lines NOT preceded by a blank line, so this is rare.
        string text = Encoding.ASCII.GetString(line).TrimEnd('\r', '\n');
        return EnvelopePostmark.IsMatch(text);
    }

    // A line is a message boundary when it starts with the "From " marker AND
    // either the previous line was blank (the common mbox case; previousLineWasBlank
    // is initialised true so the first line qualifies) OR the line itself matches
    // the envelope postmark shape (so messages with no blank separator still split,
    // without splitting on unescaped body lines that merely begin with "From ").
    private static bool IsMessageBoundary(ReadOnlySpan<byte> line, bool previousLineWasBlank) =>
        StartsWithFromMarker(line) && (previousLineWasBlank || IsEnvelopePostmark(line));

    private static bool IsBlankLine(ReadOnlySpan<byte> line)
    {
        foreach (byte b in line)
        {
            if (b != (byte)'\r' && b != (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }

    // A missing Date header yields null; a present-but-unparseable one makes
    // MimeKit return DateTimeOffset.MinValue (0001-01-01). Treat both as "no
    // date" so a single bad message can't poison scan date ranges.
    private static DateTimeOffset? ParseDate(MimeMessage mime)
    {
        if (!mime.Headers.Contains(HeaderId.Date)) return null;
        DateTimeOffset date = mime.Date;
        return date == DateTimeOffset.MinValue ? null : date;
    }

    private MailMessage ToMailMessage(MimeMessage mime, SourceReference sourceRef, List<string> warnings)
    {
        return new MailMessage
        {
            Subject = mime.Subject,
            From = ToMailAddress(mime.From.Mailboxes.FirstOrDefault()),
            To = mime.To.Mailboxes.Select(ToMailAddressNonNull).ToList(),
            Cc = mime.Cc.Mailboxes.Select(ToMailAddressNonNull).ToList(),
            Bcc = mime.Bcc.Mailboxes.Select(ToMailAddressNonNull).ToList(),
            Date = ParseDate(mime),
            TextBody = mime.TextBody,
            HtmlBody = mime.HtmlBody,
            Attachments = ExtractAttachments(mime, warnings),
            Source = sourceRef,
            MessageId = EnsureAngleBrackets(mime.MessageId),
            InReplyTo = EnsureAngleBrackets(mime.InReplyTo),
            References = mime.References.Count > 0
                ? string.Join(" ", mime.References.Select(id => EnsureAngleBrackets(id)).OfType<string>())
                : null,
            IsRead = ParseIsRead(mime),
            Importance = ParseImportance(mime),
        };
    }

    private static MailImportance ParseImportance(MimeMessage mime)
    {
        if (mime.Importance == MessageImportance.High) return MailImportance.High;
        if (mime.Importance == MessageImportance.Low) return MailImportance.Low;

        // Importance == Normal is treated as "not explicitly set"; check X-Priority fallback.
        // mime.XPriority (distinct from mime.Priority) reads the X-Priority header and handles
        // decorated values like "1 (Highest)" that raw int-parse would miss.
        // XMessagePriority: Highest=1, High=2 → our High; Low=4, Lowest=5 → our Low; Normal=3.
        XMessagePriority xp = mime.XPriority;
        if (xp == XMessagePriority.Highest || xp == XMessagePriority.High) return MailImportance.High;
        if (xp == XMessagePriority.Low || xp == XMessagePriority.Lowest) return MailImportance.Low;

        return MailImportance.Normal;
    }

    private static bool ParseIsRead(MimeMessage mime)
    {
        string? gmailLabels = mime.Headers["X-Gmail-Labels"];
        if (gmailLabels is not null)
        {
            // Gmail exports labels in the account's display language, so we match known locale variants.
            // English: "Unread", Danish: "Ulæste"
            return !gmailLabels.Split(',').Any(t =>
                t.Trim().Equals("Unread", StringComparison.OrdinalIgnoreCase) ||
                t.Trim().Equals("Ulæste", StringComparison.OrdinalIgnoreCase));
        }

        string? mozillaStatus = mime.Headers["X-Mozilla-Status"];
        if (mozillaStatus is not null)
        {
            if (uint.TryParse(mozillaStatus.Trim(), System.Globalization.NumberStyles.HexNumber, null, out uint flags))
                return (flags & 0x0001) != 0;
            // malformed → fall through
        }

        string? status = mime.Headers["Status"];
        if (status is not null)
            return status.Contains('R', StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private static string? EnsureAngleBrackets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.StartsWith("<") && value.EndsWith(">")) return value;
        return $"<{value}>";
    }

    /// <summary>
    /// Classifies each entity in <paramref name="mime"/>'s MIME tree as either
    /// body content (skipped) or an attachment/inline resource. Any
    /// unrecognized or non-body leaf MIME part is preserved as an attachment
    /// rather than dropped (a deliberate v1 fidelity choice). Per-attachment
    /// extraction failures are recorded in <paramref name="warnings"/> and the
    /// attachment is dropped without failing the whole message.
    /// </summary>
    internal List<MailAttachment> ExtractAttachments(MimeMessage mime, List<string> warnings)
    {
        var attachments = new List<MailAttachment>();
        int index = 0;

        foreach (MimeEntity entity in mime.BodyParts)
        {
            if (entity is MessagePart messagePart)
            {
                // NDR/bounce messages use multipart/report with a message/rfc822 part containing
                // the original undelivered mail. Gmail suppresses these as system messages;
                // skip them here so we don't surface a phantom attachment.
                if (mime.Body?.ContentType.IsMimeType("multipart", "report") == true)
                    continue;

                index++;
                const string embeddedFileName = "attached-message.eml";
                const string embeddedMimeType = "message/rfc822";
                try
                {
                    using var messageBytes = new MemoryStream();
                    messagePart.Message.WriteTo(messageBytes);

                    attachments.Add(new MailAttachment
                    {
                        FileName = embeddedFileName,
                        MimeType = embeddedMimeType,
                        Content = ToAttachmentContent(messageBytes),
                    });
                }
                catch (Exception ex)
                {
                    warnings.Add(FormatDroppedAttachmentWarning(index, embeddedFileName, embeddedMimeType, ex));
                }

                continue;
            }

            if (entity is not MimePart part)
            {
                continue;
            }

            // message/delivery-status (and similar message/* parts) inside an NDR are
            // machine-readable delivery metadata, not user-visible attachments.
            if (part.ContentType.MediaType.Equals("message", StringComparison.OrdinalIgnoreCase)
                && mime.Body?.ContentType.IsMimeType("multipart", "report") == true)
                continue;

            bool isAttachmentDisposition = part.ContentDisposition?.Disposition is { } disposition
                && disposition.Equals(ContentDisposition.Attachment, StringComparison.OrdinalIgnoreCase);
            bool isBodyMimeType = part.ContentType.IsMimeType("text", "plain") || part.ContentType.IsMimeType("text", "html");

            // text/plain and text/html parts are body content — only treat them as
            // attachments if they carry an explicit filename or attachment disposition.
            // A Content-ID alone on a body-typed part is a body-part label (e.g.
            // LinkedIn's "Content-ID: text-body"), not an inline resource reference.
            bool isAttachment = isAttachmentDisposition
                || !string.IsNullOrEmpty(part.FileName)
                || !isBodyMimeType;

            if (!isAttachment)
            {
                continue;
            }

            index++;

            // part.FileName is string? (MimeKit is nullable-annotated); IsNullOrEmpty
            // has [NotNullWhen(false)], so fileName is non-null after this block.
            string? fileName = part.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"attachment-{index}{GuessExtension(part.ContentType)}";
            }

            string mimeType = part.ContentType.MimeType;

            if (part.Content is null)
            {
                warnings.Add(FormatDroppedAttachmentWarning(index, fileName, mimeType,
                    new InvalidOperationException("attachment part has no content data")));
                continue;
            }

            try
            {
                using var buffer = new MemoryStream();
                part.Content.DecodeTo(buffer);

                string? contentId = string.IsNullOrEmpty(part.ContentId) ? null : NormalizeContentId(part.ContentId);
                bool isInlineDisposition = part.ContentDisposition?.Disposition is { } partDisposition
                    && partDisposition.Equals(ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase);

                attachments.Add(new MailAttachment
                {
                    FileName = fileName,
                    MimeType = mimeType,
                    ContentId = contentId,
                    ContentLocation = part.ContentLocation?.ToString(),
                    IsInline = isInlineDisposition || contentId is not null,
                    Content = ToAttachmentContent(buffer),
                });
            }
            catch (Exception ex)
            {
                warnings.Add(FormatDroppedAttachmentWarning(index, fileName, mimeType, ex));
            }
        }

        return attachments;
    }

    // Routes attachment bytes to memory (small) or a temp file (large) to bound the
    // SUSTAINED memory of the bounded parse/write queue — a queued temp-backed attachment
    // holds only a path, not its bytes. This does NOT reduce PEAK memory: the part is
    // already fully decoded into `buffer` (a MemoryStream) before this point, and the temp
    // file is read back in full at write time (AttachmentContent.ReadAllBytes -> PstWriter).
    // It is queue-memory hygiene, not end-to-end streaming.
    private AttachmentContent ToAttachmentContent(MemoryStream buffer)
    {
        if (buffer.Length < _tempFileThresholdBytes)
            return AttachmentContent.FromBytes(buffer.ToArray());

        string tempPath = Path.Combine(Path.GetTempPath(), $"mbox2pst-{Guid.NewGuid()}");
        buffer.Position = 0;
        try
        {
            using (var tempFile = File.Create(tempPath))
                buffer.CopyTo(tempFile);
        }
        catch (Exception)
        {
            try { File.Delete(tempPath); } catch (Exception) { }
            throw;
        }
        return AttachmentContent.FromTempFile(tempPath, buffer.Length);
    }

    /// <summary>
    /// Formats the warning recorded when an attachment is dropped due to an
    /// extraction failure.
    /// </summary>
    private static string FormatDroppedAttachmentWarning(int index, string fileName, string mimeType, Exception ex) =>
        $"Dropped attachment #{index} '{fileName}' ({mimeType}): {ex.Message}";

    /// <summary>
    /// Returns a best-effort file extension (including the leading '.') for a
    /// part with no filename, derived from its MIME subtype (e.g. "image/png"
    /// -> ".png"). Returns "" for "octet-stream" or for subtypes that aren't a
    /// single alphanumeric token (e.g. "svg+xml"), since those wouldn't make a
    /// sensible bare extension.
    /// </summary>
    private static string GuessExtension(ContentType contentType)
    {
        string subtype = contentType.MediaSubtype;
        if (string.IsNullOrEmpty(subtype) || !subtype.All(char.IsLetterOrDigit))
        {
            return string.Empty;
        }

        if (subtype.Equals("octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "." + subtype.ToLowerInvariant();
    }

    /// <summary>
    /// Strips the optional "cid:" prefix and surrounding angle brackets from a
    /// Content-ID/cid: reference, so stored ContentId values can be compared
    /// directly against each other regardless of which form they came from.
    /// Note: <c>part.ContentId</c> (from MIME headers) never has a "cid:"
    /// prefix in practice — that prefix only appears in HTML
    /// <c>src="cid:..."</c> references. The "cid:"-stripping branch here is
    /// defensive/future-proofing, for if such an HTML reference is ever
    /// normalized through this same helper.
    /// </summary>
    private static string NormalizeContentId(string contentId)
    {
        contentId = contentId.Trim();

        if (contentId.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
        {
            contentId = contentId.Substring(4);
        }

        if (contentId.StartsWith("<") && contentId.EndsWith(">"))
        {
            contentId = contentId.Substring(1, contentId.Length - 2);
        }

        return contentId;
    }

    private static MailAddress? ToMailAddress(MailboxAddress? mailbox) =>
        mailbox is null ? null : ToMailAddressNonNull(mailbox);

    private static MailAddress ToMailAddressNonNull(MailboxAddress mailbox) =>
        new() { Name = mailbox.Name, Email = mailbox.Address };
}
