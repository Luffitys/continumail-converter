// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, it, expect } from "vitest";
import { formatBytes, formatShortDate, formatDateRange } from "./format";

describe("formatBytes", () => {
  it("formats bytes, KB, MB, GB", () => {
    expect(formatBytes(512)).toBe("512 B");
    expect(formatBytes(1536)).toBe("1.5 KB");
    expect(formatBytes(5 * 1024 * 1024)).toBe("5.0 MB");
    expect(formatBytes(3 * 1024 ** 3)).toBe("3.0 GB");
  });
});

describe("formatShortDate", () => {
  it("formats an ISO date to YYYY-MM-DD", () => {
    expect(formatShortDate("2012-03-04T05:06:07Z")).toBe("2012-03-04");
  });
  it("returns an em dash for null", () => {
    expect(formatShortDate(null)).toBe("—");
  });
});

describe("formatDateRange", () => {
  it("joins two dates with an en dash", () => {
    expect(formatDateRange("2012-01-01T00:00:00Z", "2024-12-31T00:00:00Z")).toBe(
      "2012-01-01 – 2024-12-31",
    );
  });
  it("returns an em dash when both are null", () => {
    expect(formatDateRange(null, null)).toBe("—");
  });
  it("shows a single date when both ends are the same day", () => {
    expect(formatDateRange("2020-05-05T01:00:00Z", "2020-05-05T23:00:00Z")).toBe("2020-05-05");
  });
  it("shows only the start when the end is null", () => {
    expect(formatDateRange("2012-01-01T00:00:00Z", null)).toBe("2012-01-01");
  });
  it("shows only the end when the start is null", () => {
    expect(formatDateRange(null, "2024-12-31T00:00:00Z")).toBe("2024-12-31");
  });
});
