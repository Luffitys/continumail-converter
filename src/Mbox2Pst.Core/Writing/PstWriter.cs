// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mbox2Pst.Core.Config;
using Mbox2Pst.Core.Mapping;
using Mbox2Pst.Core.Models;
using Mbox2Pst.Core.Progress;
using Mbox2Pst.Core.Reporting;
using PSTFileFormat;

namespace Mbox2Pst.Core.Writing;

/// <summary>
/// Writes planned messages into one or more PST files, seeded from a copy of
/// the blank PST template. A single <see cref="PstOutputPlan"/> may produce
/// multiple physical files if it exceeds <see cref="PstOutputPlan.MaxSizeBytes"/>
/// (see Task 9).
/// </summary>
public class PstWriter
{
    private readonly string _templatePath;

    // Number of successfully-written messages between on-disk size checks.
    private readonly int _checkIntervalMessages;

    // Number of successfully-written messages between lightweight progress events
    // (decoupled from the flush/size-check interval so the GUI bar moves fluidly).
    private readonly int _progressIntervalMessages;

    // Parsed-but-not-yet-written messages buffered between the parser thread
    // and the PST-writer thread. Large enough to keep the writer busy during
    // brief parse stalls; small enough that we don't hold many messages in
    // memory simultaneously.
    private const int ParseQueueCapacity = 32;

    public PstWriter(string templatePath, int checkIntervalMessages = 500, int progressIntervalMessages = 25)
    {
        if (checkIntervalMessages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(checkIntervalMessages));
        }

        if (progressIntervalMessages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(progressIntervalMessages));
        }

        _templatePath = templatePath;
        _checkIntervalMessages = checkIntervalMessages;
        _progressIntervalMessages = progressIntervalMessages;
    }

    public List<string> WritePlan(PstOutputPlan plan, IEnumerable<PlannedMessage> messages, string outputDirectory, ConversionReport report, int totalMessages = -1, Action<ConversionProgressEvent>? onProgress = null, CancellationToken cancellationToken = default)
    {
        // Pre-flight: if already cancelled, create nothing so DeletedFiles stays empty.
        cancellationToken.ThrowIfCancellationRequested();

        var outputFiles = new List<string>();
        // Start as a single un-suffixed file ("Name.pst"). Only if a split actually
        // happens do we rename it to "Name-1.pst" and continue with "Name-2.pst", ...
        int partNumber = 1;
        string currentPath = StartNewFile(plan.Name, null, outputDirectory);
        outputFiles.Add(currentPath);

        // Producer thread: parse MIME messages (CPU-bound MimeKit work).
        // Consumer thread (current): write to PST (I/O-bound vendored library).
        // The bounded channel provides back-pressure so the parser doesn't race
        // too far ahead of the writer and hold parsed messages in memory.
        using var cts = new CancellationTokenSource();
        using var queue = new BlockingCollection<PlannedMessage>(boundedCapacity: ParseQueueCapacity);
        Exception? producerException = null;

        var producer = Task.Run(() =>
        {
            try
            {
                foreach (PlannedMessage msg in messages)
                    queue.Add(msg, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { producerException = ex; }
            finally { queue.CompleteAdding(); }
        });

        var file = new PSTFile(currentPath, FileAccess.ReadWrite);
        bool cancelled = false;
        ExceptionDispatchInfo? faultCapture = null;
        try
        {
            file.BeginSavingChanges();
            var folders = new Dictionary<string, PSTFolder>();

            // Pre-create a folder for every mapped source so that a source with
            // zero messages (e.g. an empty mbox) still appears as an empty folder
            // in the output PST, mirroring the source structure faithfully. When
            // IncludeEmptyFolders is false, folders are created lazily on first
            // write instead, so empty sources are dropped.
            if (plan.IncludeEmptyFolders)
            {
                foreach (SourceMapping mapping in plan.SourceMappings)
                    GetOrCreateFolder(file, folders, mapping.TargetFolderName);
            }

            int messagesSinceCheck = 0;
            int messagesInCurrentPart = 0;   // genuinely-written messages in the current part (reset on split)
            long templateSize = new FileInfo(_templatePath).Length;

            // The underlying PST file does not necessarily grow by a measurable
            // amount for every message written (the template ships with a block
            // of pre-allocated free space, and growth happens in large jumps
            // only once that free space is exhausted). To detect when a part is
            // approaching PstOutputPlan.MaxSizeBytes well before that point, we
            // track an estimate of the content written so far (on top of the
            // template's own size) alongside the file's actual on-disk size, and
            // split as soon as either of those reaches the limit.
            long estimatedContentBytes = 0;

            string? currentSource = null;
            string? currentFolder = null;

            long estimatedOutputBytes = 0;   // run-wide for this WritePlan call; NEVER reset on split (unlike estimatedContentBytes)
            int messagesSinceProgress = 0;
            int lastEmittedConverted = -1;
            int lastEmittedSkipped = -1;
            int lastEmittedWarnings = -1;
            long lastEmittedBytes = -1;

            void EmitProgress()
            {
                if (onProgress is null) return;
                if (report.ConvertedCount == lastEmittedConverted
                    && report.SkippedCount == lastEmittedSkipped
                    && report.WarningCount == lastEmittedWarnings
                    && estimatedOutputBytes == lastEmittedBytes)
                {
                    return;
                }
                lastEmittedConverted = report.ConvertedCount;
                lastEmittedSkipped = report.SkippedCount;
                lastEmittedWarnings = report.WarningCount;
                lastEmittedBytes = estimatedOutputBytes;
                onProgress(new ProgressEvent(
                    report.ConvertedCount, totalMessages, report.WarningCount, report.SkippedCount,
                    currentSource, currentFolder, estimatedOutputBytes));
            }

            // Closes the current (already-flushed) part and opens the next one.
            // PRECONDITION: the caller has just called file.EndSavingChanges(), so the
            // current file is flushed and can be closed cleanly. Renames the initial
            // "Name.pst" to "Name-1.pst" on the first split, opens the next part with
            // BeginSavingChanges active, and resets the per-part counters/estimate.
            // Exception-safety: the next part is fully created/opened into LOCALS before the
            // shared currentPath/outputFiles/file/partNumber are reassigned, so a
            // StartNewFile/PSTFile failure leaves those pointing at the last good state
            // rather than a half-initialized new part.
            void StartNextPartAfterFlush()
            {
                file.CloseFile();
                if (partNumber == 1)
                {
                    // First split: the initial "Name.pst" becomes part 1.
                    string part1 = ResolveOutputPath(plan.Name, 1, outputDirectory);
                    File.Move(currentPath, part1, overwrite: true);
                    outputFiles[0] = part1;
                }

                int nextPartNumber = partNumber + 1;
                string nextPath = StartNewFile(plan.Name, nextPartNumber, outputDirectory);
                var nextFile = new PSTFile(nextPath, FileAccess.ReadWrite);
                nextFile.BeginSavingChanges();

                // New part is open and ready — only now mutate shared state.
                partNumber = nextPartNumber;
                currentPath = nextPath;
                outputFiles.Add(nextPath);
                file = nextFile;
                folders.Clear();
                estimatedContentBytes = 0;
                messagesSinceCheck = 0;
                messagesInCurrentPart = 0;
            }

            foreach (PlannedMessage planned in queue.GetConsumingEnumerable())
            {
                currentSource = planned.Message.Source.SourcePath;
                currentFolder = planned.TargetFolderName;

                long messageSize = 0;
                bool written = false;
                try
                {
                    // Check cancellation BEFORE the predictive split so a cancelled run never
                    // creates an extra part — the in-progress part stays the one deleted on
                    // cancel (keeps WritePlan_CancelledDuringSplit_* correct). Inside the
                    // try so the finally always disposes this dequeued message's attachments
                    // even when cancel fires before the write starts.
                    cancellationToken.ThrowIfCancellationRequested();

                    // Estimate BEFORE writing so we can split predictively. EstimateMessageSize
                    // only reads message fields + stored attachment lengths (no I/O).
                    messageSize = EstimateMessageSize(planned.Message);

                    // Predictive split: if writing this message would push the running
                    // estimate over the cap and the current part already has content, split
                    // first so the crossing message starts a fresh part. messagesInCurrentPart
                    // > 0 guards the single-oversized-message case (a fresh part can't be
                    // split — write the big message whole).
                    if (messagesInCurrentPart > 0 &&
                        templateSize + estimatedContentBytes + messageSize >= plan.MaxSizeBytes)
                    {
                        file.EndSavingChanges();
                        StartNextPartAfterFlush();
                    }

                    PSTFolder folder = GetOrCreateFolder(file, folders, planned.TargetFolderName);
                    WriteMessageCore(file, folder, planned.Message);
                    report.RecordConverted();
                    written = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (IsRecoverableWriteError(ex))
                {
                    report.RecordSkipped(planned.Message.Source, ex.Message);
                }
                finally
                {
                    foreach (MailAttachment attachment in planned.Message.Attachments)
                        attachment.Content.Dispose();
                }

                if (!written) continue;

                estimatedContentBytes += messageSize;
                estimatedOutputBytes += messageSize;
                messagesInCurrentPart++;
                messagesSinceCheck++;
                messagesSinceProgress++;
                if (messagesSinceProgress >= _progressIntervalMessages)
                {
                    EmitProgress();
                    messagesSinceProgress = 0;
                }

                if (messagesSinceCheck >= _checkIntervalMessages)
                {
                    file.EndSavingChanges();
                    EmitProgress();
                    long size = Math.Max(file.BaseStream.Length, templateSize + estimatedContentBytes);
                    if (size >= plan.MaxSizeBytes)
                    {
                        StartNextPartAfterFlush();   // resets messagesSinceCheck + leaves BeginSavingChanges active
                    }
                    else
                    {
                        messagesSinceCheck = 0;
                        file.BeginSavingChanges();
                    }
                }
            }

            file.EndSavingChanges();
            EmitProgress();
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancel: stop writing, let `finally` close the handle, then
            // delete the in-progress part below (a closed handle is required first).
            cancelled = true;
            cts.Cancel();
        }
        catch (Exception ex)
        {
            // Fatal write failure: capture (preserving the original stack), stop the
            // producer, let `finally` close the handle, then delete the in-progress part
            // below — never leave a half-written PST that looks usable.
            faultCapture = ExceptionDispatchInfo.Capture(ex);
            cts.Cancel();
        }
        finally
        {
            producer.Wait();
            file.CloseFile();

            // Drain any messages still queued after a fatal error and dispose their
            // (possibly temp-file-backed) attachments so temp files don't linger
            // until process exit. On the normal path the queue is already empty,
            // and AttachmentContent.Dispose is idempotent.
            while (queue.TryTake(out PlannedMessage? leftover))
            {
                foreach (MailAttachment attachment in leftover.Message.Attachments)
                    attachment.Content.Dispose();
            }
        }

        if (cancelled)
        {
            // The handle is closed; remove the current (incomplete) part and record
            // both the deletion and any completed parts that remain on disk.
            TryDeletePart(currentPath);
            report.RecordDeletedFile(currentPath);
            report.AddOutputFiles(outputFiles.Where(p => !string.Equals(p, currentPath, StringComparison.Ordinal)));
            throw new OperationCanceledException(cancellationToken);
        }

        // Fatal path: a consumer write fault (faultCapture) or a producer/parse fault
        // (producerException) aborted the run. The handle is closed, so delete only the
        // in-progress part (currentPath always points to the newest part — covers a fatal
        // right after a split, before any message was written to the new part), keep any
        // completed split parts, then rethrow the ORIGINAL exception so ConversionRunner
        // emits the fatal `error` event (exit 1).
        ExceptionDispatchInfo? fatal = faultCapture
            ?? (producerException is not null ? ExceptionDispatchInfo.Capture(producerException) : null);
        if (fatal is not null)
        {
            TryDeletePart(currentPath);
            fatal.Throw();
        }

        return outputFiles;
    }

    private static string GetPlainTextBody(MailMessage message)
    {
        if (!string.IsNullOrEmpty(message.TextBody))
        {
            return message.TextBody;
        }

        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            return HtmlToPlainText(message.HtmlBody);
        }

        return string.Empty;
    }

    private static readonly string[] ThreadingPrefixes = { "Re:", "Fwd:", "FW:", "AW:", "SV:", "TR:" };

    private static string StripThreadingPrefixes(string? subject)
    {
        if (string.IsNullOrEmpty(subject)) return string.Empty;
        string current = subject.Trim();
        bool stripped;
        do
        {
            stripped = false;
            foreach (string prefix in ThreadingPrefixes)
            {
                if (current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    current = current.Substring(prefix.Length).Trim();
                    stripped = true;
                    break;
                }
            }
        } while (stripped);
        return current;
    }

    // Lightweight HTML-to-text fallback for clients that don't render PidTagHtml; not a full HTML parser.
    private static string HtmlToPlainText(string html)
    {
        string withoutScriptsAndStyles = Regex.Replace(html, "(?is)<(script|style)\\b[^>]*>.*?</\\1>", " ");
        string withoutTags = Regex.Replace(withoutScriptsAndStyles, "<[^>]*>", " ");
        string decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"[ \t]+", " ").Trim();
    }

    internal static long EstimateMessageSize(MailMessage message)
    {
        long size = message.Subject?.Length ?? 0;
        size += message.TextBody?.Length ?? 0;
        size += message.HtmlBody?.Length ?? 0;
        size += message.Attachments.Sum(a => a.Content.Length);
        return size;
    }

    // Best-effort delete of an in-progress part on cancel. The handle is already
    // closed by the time this runs, so a lock is the rare exception — never let a
    // failed delete turn a clean cancel into a crash.
    private static void TryDeletePart(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string StartNewFile(string groupName, int? partNumber, string outputDirectory)
    {
        string fullPath = ResolveOutputPath(groupName, partNumber, outputDirectory);
        File.Copy(_templatePath, fullPath, overwrite: true);
        return fullPath;
    }

    // Resolves the output path for a group, validating the name and asserting the
    // resolved path stays under the output directory (defense-in-depth: the runner
    // also validates names up front). A null partNumber yields "{name}.pst" (the
    // common single-file case); a value yields "{name}-{partNumber}.pst".
    private static string ResolveOutputPath(string groupName, int? partNumber, string outputDirectory)
    {
        OutputNameValidator.Validate(groupName);

        string fileName = partNumber is null ? $"{groupName}.pst" : $"{groupName}-{partNumber}.pst";
        string fullDir = Path.GetFullPath(outputDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(fullDir, fileName));
        string relative = Path.GetRelativePath(fullDir, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ConfigValidationException($"Resolved output path escapes the output directory: {fullPath}");

        return fullPath;
    }

    private static PSTFolder GetOrCreateFolder(PSTFile file, Dictionary<string, PSTFolder> folders, string folderName)
    {
        if (folders.TryGetValue(folderName, out PSTFolder? existing))
        {
            return existing;
        }

        PSTFolder root = file.TopOfPersonalFolders;
        PSTFolder folder = root.FindChildFolder(folderName) ?? root.CreateChildFolder(folderName, FolderItemTypeName.Note);
        folders[folderName] = folder;
        return folder;
    }

    // Per-message write, extracted as an overridable seam so tests can inject a
    // deterministic write failure without contriving exotic message data. Production
    // behavior is unchanged — it simply calls WriteMessage. internal: visible to the
    // test assembly via InternalsVisibleTo("Mbox2Pst.Core.Tests").
    internal virtual void WriteMessageCore(PSTFile file, PSTFolder folder, MailMessage message)
        => WriteMessage(file, folder, message);

    // A PST writer failure is fatal unless PROVEN to be one bad message. By the time we
    // write, the message has parsed successfully, so a write failure is almost certainly
    // a bug, a vendored PST-library failure, invalid internal state, or a filesystem/temp
    // problem — all of which must surface as fatal (mirroring the parse-side taxonomy in
    // MboxParser). This allowlist is intentionally EMPTY: add a specific exception type
    // ONLY after a real data-driven write failure is observed and proven safely skippable.
    // Keeping the predicate + catch-filter makes that a one-line change. This is a
    // deliberate, documented extension seam — NOT dead code to "clean up".
    internal static bool IsRecoverableWriteError(Exception ex) => false;

    private static void WriteMessage(PSTFile file, PSTFolder folder, MailMessage message)
    {
        Note note = Note.CreateNewNote(file, folder.NodeID);
        note.Subject = message.Subject ?? string.Empty;

        if (message.From is not null)
        {
            string senderName = message.From.Name ?? message.From.Email;
            note.SenderName = senderName;
            note.SenderAddressType = "SMTP";
            note.SenderEmailAddress = message.From.Email;
            note.SentRepresentingName = senderName;
            note.SentRepresentingAddressType = "SMTP";
            note.SentRepresentingEmailAddress = message.From.Email;
        }

        note.Body = GetPlainTextBody(message);

        if (!string.IsNullOrEmpty(message.HtmlBody))
        {
            note.PC.SetBytesProperty(PropertyID.PidTagHtml, Encoding.UTF8.GetBytes(message.HtmlBody));
            note.PC.SetInt32Property(PropertyID.PidTagNativeBody, 3);
            note.InternetCodepage = 65001;
        }

        if (message.Date.HasValue)
        {
            DateTime utcDate = ClampToFileTimeRange(message.Date.Value.UtcDateTime);
            note.ClientSubmitTime = utcDate;
            note.MessageDeliveryTime = utcDate;
        }

        static IEnumerable<MessageRecipient> ToRecipients(IEnumerable<MailAddress> addresses, RecipientType type) =>
            addresses.Select(address =>
                new MessageRecipient(address.Name ?? address.Email, address.Email, isOrganizer: false, type));

        List<MessageRecipient> recipients = ToRecipients(message.To, RecipientType.To)
            .Concat(ToRecipients(message.Cc, RecipientType.Cc))
            .Concat(ToRecipients(message.Bcc, RecipientType.Bcc))
            .ToList();

        if (recipients.Count > 0)
        {
            note.AddRecipients(recipients);
        }

        if (message.MessageId is not null)
            note.PC.SetStringProperty(PropertyID.PidTagInternetMessageId, message.MessageId);
        if (message.InReplyTo is not null)
            note.PC.SetStringProperty((PropertyID)0x1042, message.InReplyTo);
        if (message.References is not null)
            note.PC.SetStringProperty((PropertyID)0x1039, message.References);

        note.PC.SetInt32Property(PropertyID.PidTagImportance, (int)message.Importance);
        note.PC.SetInt32Property(PropertyID.PidTagPriority, (int)message.Importance - 1);

        string normalizedSubject = StripThreadingPrefixes(message.Subject);
        note.PC.SetStringProperty(PropertyID.PidTagConversationTopic, normalizedSubject);
        note.PC.SetStringProperty((PropertyID)0x0E1D, normalizedSubject);

        foreach (MailAttachment attachment in message.Attachments)
        {
            WriteAttachment(file, note, attachment);
        }

        // Set message flags AFTER attachments are written: the vendored
        // AddAttachment unconditionally sets MSGFLAG_HASATTACH for any attachment
        // (including inline ones), so we must have the final word here. We only
        // control the two bits we own (MSGFLAG_READ, MSGFLAG_HASATTACH) and
        // preserve everything else Note.CreateNewNote/AddAttachment set.
        int messageFlags = note.PC.GetInt32Property(PropertyID.PidTagMessageFlags) ?? 0;
        if (message.IsRead) messageFlags |= 0x0001;
        else messageFlags &= ~0x0001;
        // Only set MSGFLAG_HASATTACH for non-inline attachments. Inline (CID)
        // images are part of the HTML body, not user-visible attachments, so a
        // message whose only attachments are inline must not show a paperclip.
        if (message.Attachments.Any(a => !a.IsInline)) messageFlags |= 0x0010;
        else messageFlags &= ~0x0010;
        note.PC.SetInt32Property(PropertyID.PidTagMessageFlags, messageFlags);

        note.SaveChanges();
        folder.AddMessage(note);
        folder.SaveChanges();
    }

    private static void WriteAttachment(PSTFile file, Note note, MailAttachment a)
    {
        note.CreateSubnodeBTreeIfNotExist();
        AttachmentObject attachment = AttachmentObject.CreateNewAttachmentObject(file, note.SubnodeBTree);

        attachment.PC.SetStringProperty(PropertyID.PidTagAttachLongFilename, a.FileName);
        attachment.PC.SetStringProperty(PropertyID.PidTagAttachFilename, a.FileName);
        attachment.PC.SetStringProperty(PropertyID.PidTagDisplayName, a.FileName);
        attachment.PC.SetStringProperty(PropertyID.PidTagAttachExtension, Path.GetExtension(a.FileName));
        attachment.PC.SetStringProperty(PropertyID.PidTagAttachMimeTag, a.MimeType);
        attachment.PC.SetInt32Property(PropertyID.PidTagAttachMethod, (int)AttachMethod.ByValue);
        attachment.PC.SetBytesProperty(PropertyID.PidTagAttachData, a.Content.ReadAllBytes());
        attachment.PC.SetInt32Property(PropertyID.PidTagAttachSize, (int)a.Content.Length);

        if (!string.IsNullOrEmpty(a.ContentId))
        {
            attachment.PC.SetStringProperty(PropertyID.PidTagAttachContentId, a.ContentId);
        }

        if (!string.IsNullOrEmpty(a.ContentLocation))
        {
            attachment.PC.SetStringProperty(PropertyID.PidTagAttachContentLocation, a.ContentLocation);
        }

        if (a.IsInline)
        {
            attachment.PC.SetInt32Property(PropertyID.PidTagAttachFlags, 4); // ATT_MHTML_REF
            // Hide inline (CID) images from the attachment list so Outlook does
            // not render a paperclip for body-embedded images.
            attachment.PC.SetBooleanProperty(PropertyID.PidTagAttachmentHidden, true);
        }

        // attachment.PC's property writes above only update the in-memory
        // property context; SaveChanges(note.SubnodeBTree) flushes it to the
        // attachment's data tree and updates the entry this subnode occupies
        // in the note's subnode B-tree (Subnode.SaveChanges(SubnodeBTree)),
        // so the attachment's properties survive a reopen of the PST.
        attachment.SaveChanges(note.SubnodeBTree);
        note.AddAttachment(attachment);
    }

    // Windows FILETIME epoch is 1601-01-01. DateTime.ToFileTimeUtc() throws
    // ArgumentOutOfRangeException for dates before that, which would skip the
    // whole message. Clamp to the valid range instead.
    private static readonly DateTime MinFileTime = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime ClampToFileTimeRange(DateTime utcDate) =>
        utcDate < MinFileTime ? MinFileTime : utcDate;
}
