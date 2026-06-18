// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, it, expect } from "vitest";
import { parseEngineOutput, EngineParseError } from "./parse";

const VERSION_LINE = '{"type":"version","version":"0.1.0","engine":"Mail2Pst.Cli"}';

const SCAN_JSON = `{
  "type": "scan",
  "totals": { "messages": 2, "bytes": 167, "sourceBytes": 735, "sources": 1 },
  "sources": [
    {
      "id": "sample", "path": "C:/x/sample.mbox", "displayName": "sample",
      "messages": 2, "bytes": 167, "sourceBytes": 735,
      "dateFrom": "2024-01-01T10:00:00Z", "dateTo": "2024-01-02T11:30:00Z",
      "warnings": 0, "skipped": 0
    }
  ],
  "skipped": [],
  "warnings": []
}`;

describe("parseEngineOutput", () => {
  it("parses a version line", () => {
    const r = parseEngineOutput(VERSION_LINE);
    expect(r.kind).toBe("version");
    if (r.kind === "version") expect(r.version).toBe("0.1.0");
  });

  it("carries schemaVersion onto a parsed scan result", () => {
    const withVersion = SCAN_JSON.replace('"type": "scan",', '"type": "scan", "schemaVersion": 1,');
    const r = parseEngineOutput(withVersion);
    expect(r.kind).toBe("scan");
    if (r.kind === "scan") expect(r.schemaVersion).toBe(1);
  });

  it("parses a pretty-printed scan object", () => {
    const r = parseEngineOutput(SCAN_JSON);
    expect(r.kind).toBe("scan");
    if (r.kind === "scan") {
      expect(r.totals.messages).toBe(2);
      expect(r.totals.sources).toBe(1);
      expect(r.sources).toHaveLength(1);
      expect(r.sources[0].id).toBe("sample");
      expect(r.sources[0].dateFrom).toBe("2024-01-01T10:00:00Z");
    }
  });

  it("tolerates a leading log line and trailing whitespace", () => {
    const noisy = `MSBuild version 17\nRestored project\n${SCAN_JSON}\n\n  `;
    const r = parseEngineOutput(noisy);
    expect(r.kind).toBe("scan");
  });

  it("ignores unrecognized JSON objects and picks the first recognized version-or-scan object", () => {
    const multi = `{"type":"started","output":"x"}\n${VERSION_LINE}`;
    const r = parseEngineOutput(multi);
    expect(r.kind).toBe("version");
  });

  it("throws EngineParseError on unrecognized/garbage input", () => {
    expect(() => parseEngineOutput("hello, no json here")).toThrow(EngineParseError);
  });

  it("respects braces inside string values when splitting objects", () => {
    // A displayName containing { and } must not confuse the brace-depth scanner:
    // the inString tracking has to treat them as literal characters.
    const withBraces = SCAN_JSON.replace(
      '"displayName": "sample"',
      '"displayName": "Inbox {2024} }{ archive"',
    );
    const r = parseEngineOutput(withBraces);
    expect(r.kind).toBe("scan");
    if (r.kind === "scan") {
      expect(r.sources).toHaveLength(1);
      expect(r.sources[0].displayName).toBe("Inbox {2024} }{ archive");
    }
  });
});
