// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, it, expect } from "vitest";
import { parseEngineOutput, EngineParseError } from "./parse";

const VERSION_LINE = '{"type":"version","version":"0.1.0","engine":"Mbox2Pst.Cli"}';

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
});
