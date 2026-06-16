// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Text;
using Mbox2Pst.Core.Parsing;
using Xunit;

namespace Mbox2Pst.Core.Tests.Parsing;

public class MboxParserBoundaryTests
{
    private static string WriteTempMbox(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    // #0: a second envelope line with no blank line before it must still split,
    // for both asctime (no tz) and tz-before-year postmark shapes.
    [Theory]
    [InlineData("From alice@example.com Mon Jan  1 10:00:00 2024")]
    [InlineData("From synthetic1@example.com Tue Nov 26 15:46:52 +0000 2024")]
    public void Parse_NoBlankSeparator_PostmarkShapes_SplitIntoTwo(string secondEnvelope)
    {
        string content =
            "From a@example.com Mon Jan  1 00:00:00 2020\n" +
            "Subject: One\n" +
            "\n" +
            "Body one.\n" +
            secondEnvelope + "\n" +
            "Subject: Two\n" +
            "\n" +
            "Body two.\n";
        string path = WriteTempMbox(content);
        try
        {
            var parser = new MboxParser();
            var results = parser.Parse(path).ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal("One", results[0].Message!.Subject);
            Assert.Equal("Two", results[1].Message!.Subject);
            Assert.DoesNotContain("Body two", results[0].Message!.TextBody);
            Assert.Equal(2, parser.CountMessages(path));
        }
        finally { File.Delete(path); }
    }

    // Guard against over-splitting: a body line that begins "From " but is NOT
    // preceded by a blank line and does NOT match the postmark shape stays body.
    [Fact]
    public void Parse_BodyLineStartingFromButNotPostmark_IsNotSplit()
    {
        string content =
            "From a@example.com Mon Jan  1 00:00:00 2020\n" +
            "Subject: One\n" +
            "\n" +
            "This is the body.\n" +
            "From now on it will be different.\n" +
            "More body.\n";
        string path = WriteTempMbox(content);
        try
        {
            var parser = new MboxParser();
            var results = parser.Parse(path).ToList();

            Assert.Single(results);
            Assert.Contains("From now on it will be different.", results[0].Message!.TextBody);
            Assert.Equal(1, parser.CountMessages(path));
        }
        finally { File.Delete(path); }
    }

    // CRLF coverage for the edited boundary/envelope path.
    [Fact]
    public void Parse_NoBlankSeparator_Crlf_SplitIntoTwo()
    {
        string content =
            "From a@example.com Mon Jan  1 00:00:00 2020\r\n" +
            "Subject: One\r\n" +
            "\r\n" +
            "Body one.\r\n" +
            "From b@example.com Tue Jan  2 00:00:00 2020\r\n" +
            "Subject: Two\r\n" +
            "\r\n" +
            "Body two.\r\n";
        string path = WriteTempMbox(content);
        try
        {
            var parser = new MboxParser();
            var results = parser.Parse(path).ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal("Two", results[1].Message!.Subject);
            Assert.Equal(2, parser.CountMessages(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_MboxrdEscapedBodyLines_AreUnescaped()
    {
        // Stored (escaped) forms -> expected original after un-escaping:
        //   ">From the desk"     -> "From the desk"
        //   ">>From the archive"  -> ">From the archive"
        string content =
            "From a@example.com Mon Jan  1 00:00:00 2020\n" +
            "Subject: Esc\n" +
            "\n" +
            ">From the desk of Alice\n" +
            ">>From the archive\n" +
            "Regular line.\n";
        string path = WriteTempMbox(content);
        try
        {
            var parser = new MboxParser();
            var results = parser.Parse(path).ToList();

            Assert.Single(results);
            string body = results[0].Message!.TextBody!;
            Assert.Contains("From the desk of Alice", body);
            Assert.Contains(">From the archive", body);
            // The extra mboxrd escaping '>' must be gone:
            Assert.DoesNotContain(">From the desk", body);
            Assert.DoesNotContain(">>From", body);
        }
        finally { File.Delete(path); }
    }

    // Integration guard (boundary heuristic x un-escaping): an mboxrd-escaped body
    // line whose UN-escaped form would itself look like a postmark must stay body
    // content and never split. Boundary detection runs on the raw ">From ..." line
    // (which fails the "From " marker because it starts with '>'), so it is not a
    // boundary; un-escaping then restores the original "From a@b Mon ..." text.
    [Fact]
    public void Parse_EscapedBodyLineThatUnescapesToPostmark_IsNotSplit()
    {
        string content =
            "From a@example.com Mon Jan  1 00:00:00 2020\n" +
            "Subject: One\n" +
            "\n" +
            "Body line.\n" +
            ">From b@example.com Tue Jan  2 00:00:00 2020\n" +
            "More body.\n";
        string path = WriteTempMbox(content);
        try
        {
            var parser = new MboxParser();
            var results = parser.Parse(path).ToList();

            Assert.Single(results);
            string body = results[0].Message!.TextBody!;
            Assert.Contains("From b@example.com Tue Jan  2 00:00:00 2020", body);
            Assert.DoesNotContain(">From b@example.com", body);
            Assert.Equal(1, parser.CountMessages(path));
        }
        finally { File.Delete(path); }
    }

    // Trailing-line flush coverage: final message has no trailing newline, and
    // there is no blank line before the second envelope.
    [Fact]
    public void Parse_FinalMessageNoTrailingNewline_IsParsed()
    {
        string content =
            "From a@example.com Mon Jan  1 00:00:00 2020\n" +
            "Subject: One\n" +
            "\n" +
            "Body one.\n" +
            "From b@example.com Tue Jan  2 00:00:00 2020\n" +
            "Subject: Two\n" +
            "\n" +
            "Body two."; // no trailing newline
        string path = WriteTempMbox(content);
        try
        {
            var parser = new MboxParser();
            var results = parser.Parse(path).ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal("Two", results[1].Message!.Subject);
            Assert.Contains("Body two.", results[1].Message!.TextBody);
            Assert.Equal(2, parser.CountMessages(path));
        }
        finally { File.Delete(path); }
    }
}
