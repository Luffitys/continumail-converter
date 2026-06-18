// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, it, expect } from "vitest";
import { formatRate, formatDuration, windowedRate, easeToward, etaFromWindowedRate, formatElapsed, type RateSample } from "./progressStats";

describe("formatRate", () => {
  it("formats MB/s and handles null", () => {
    expect(formatRate(2_000_000)).toBe("1.9 MB/s");
    expect(formatRate(null)).toBe("—");
  });
});

describe("formatDuration", () => {
  it("formats seconds and minutes, handles null", () => {
    expect(formatDuration(45)).toBe("45s");
    expect(formatDuration(80)).toBe("1m 20s");
    expect(formatDuration(null)).toBe("—");
  });
});

describe("windowedRate", () => {
  const s = (ms: number, bytes: number, converted: number): RateSample => ({ ms, bytes, converted });
  it("computes rate over the window", () => {
    expect(windowedRate([s(0, 0, 0), s(1000, 2_000_000, 5)], 3000)).toEqual({
      bytesPerSec: 2_000_000,
      msgPerSec: 5,
    });
  });
  it("returns nulls with fewer than 2 samples", () => {
    expect(windowedRate([s(0, 0, 0)], 3000)).toEqual({ bytesPerSec: null, msgPerSec: null });
  });
  it("ignores samples outside the window (uses oldest in-window)", () => {
    expect(windowedRate([s(0, 0, 0), s(5000, 1000, 1), s(6000, 3000, 3)], 2000)).toEqual({
      bytesPerSec: 2000,
      msgPerSec: 2,
    });
  });
  it("returns null for a negative delta", () => {
    expect(windowedRate([s(0, 100, 5), s(1000, 50, 3)], 3000)).toEqual({
      bytesPerSec: null,
      msgPerSec: null,
    });
  });
});

describe("easeToward", () => {
  it("moves a fraction toward the target", () => {
    expect(easeToward(0, 100, 0.15)).toBeCloseTo(15);
  });
  it("never overshoots when target > current", () => {
    expect(easeToward(0, 100, 0.15)).toBeLessThanOrEqual(100);
  });
  it("snaps within epsilon and is idempotent at target", () => {
    expect(easeToward(99.7, 100, 0.15, 0.5)).toBe(100);
    expect(easeToward(100, 100, 0.15)).toBe(100);
  });
  it("clamps an out-of-range k so it never overshoots", () => {
    expect(easeToward(0, 100, 2)).toBe(100); // k clamped to 1
  });
});

describe("etaFromWindowedRate", () => {
  it("returns null without a usable rate / gate not met", () => {
    expect(etaFromWindowedRate(10, 100, null, 5000)).toBeNull();
    expect(etaFromWindowedRate(10, 100, 0, 5000)).toBeNull();
    expect(etaFromWindowedRate(3, 100, 5, 5000)).toBeNull();   // converted < 5
    expect(etaFromWindowedRate(10, 100, 5, 1000)).toBeNull();  // span < 2 s
  });
  it("computes and clamps remaining seconds", () => {
    expect(etaFromWindowedRate(10, 100, 5, 2000)).toBe(18);     // 90 left / 5
    expect(etaFromWindowedRate(100, 100, 5, 5000)).toBe(0);     // clamp
  });
});

describe("formatElapsed", () => {
  it("formats the verbose human ranges with plurals", () => {
    expect(formatElapsed(0)).toBe("0 seconds");
    expect(formatElapsed(1000)).toBe("1 second");
    expect(formatElapsed(45000)).toBe("45 seconds");
    expect(formatElapsed(60000)).toBe("1 minute");
    expect(formatElapsed(246000)).toBe("4 minutes 6 seconds");
    expect(formatElapsed(300000)).toBe("5 minutes");
    expect(formatElapsed(3600000)).toBe("1 hour");
    expect(formatElapsed(4260000)).toBe("1 hour 11 minutes");
  });
});
