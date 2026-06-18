// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using Mail2Pst.Core.Writing;
using Xunit;

namespace Mail2Pst.Core.Tests.Writing;

public class PstWriterHtmlToPlainTextTests
{
    [Fact]
    public void HtmlToPlainText_UnderCap_StripsScriptAndStyleContent()
    {
        string html = "<style>.x{}</style><script>secret</script><p>visible</p>";

        string text = PstWriter.HtmlToPlainText(html);

        Assert.Contains("visible", text);
        Assert.DoesNotContain("secret", text);
    }

    [Fact]
    public void HtmlToPlainText_OverCap_SkipsScriptStylePass()
    {
        // Over the 1,000,000-char cap: the script/style pass is skipped, so the
        // linear tag strip removes the <script> tags but leaves their text content.
        string filler = new string('a', 1_000_001);
        string html = filler + "<script>secret</script>";

        string text = PstWriter.HtmlToPlainText(html);

        Assert.Contains("secret", text);
    }

    [Fact]
    public void HtmlToPlainText_PathologicalLazyMatch_DoesNotThrow()
    {
        // Opened <script> with a large body and NO closing tag — the lazy-match
        // backtracking vector. The 2s regex timeout must keep this from hanging
        // or throwing out of HtmlToPlainText.
        string html = "<script>" + new string('a', 200_000);

        string text = PstWriter.HtmlToPlainText(html);

        Assert.NotNull(text);
    }
}
