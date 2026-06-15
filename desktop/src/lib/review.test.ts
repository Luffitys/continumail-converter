// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, it, expect } from "vitest";
import { effectiveRows, calculateReviewTotals } from "./review";
import type { SourceRow } from "./types";

function row(over: Partial<SourceRow>): SourceRow {
  return {
    id: "x", path: "x.mbox", displayName: "x", messages: 0, bytes: 0,
    sourceBytes: 0, dateFrom: null, dateTo: null, warnings: 0, skipped: 0,
    ...over,
  };
}

const inbox = row({ id: "inbox", path: "inbox.mbox", messages: 100, bytes: 1000, dateFrom: "2012-01-01T00:00:00Z", dateTo: "2020-01-01T00:00:00Z" });
const sent = row({ id: "sent", path: "sent.mbox", messages: 50, bytes: 500, dateFrom: "2015-01-01T00:00:00Z", dateTo: "2024-01-01T00:00:00Z" });
const empty = row({ id: "empty", path: "empty.mbox", messages: 0, bytes: 0 });
const all = [inbox, sent, empty];

describe("effectiveRows", () => {
  it("keeps checked, non-empty rows when skipEmpty is on", () => {
    const ids = new Set(["inbox", "sent", "empty"]);
    expect(effectiveRows(all, ids, true).map((r) => r.id)).toEqual(["inbox", "sent"]);
  });
  it("keeps a checked empty row when skipEmpty is off", () => {
    const ids = new Set(["inbox", "empty"]);
    expect(effectiveRows(all, ids, false).map((r) => r.id)).toEqual(["inbox", "empty"]);
  });
  it("drops unchecked rows regardless of skipEmpty", () => {
    const ids = new Set(["inbox"]);
    expect(effectiveRows(all, ids, false).map((r) => r.id)).toEqual(["inbox"]);
  });
});

describe("calculateReviewTotals", () => {
  it("sums messages/bytes/folders over effective rows and spans the date range", () => {
    const ids = new Set(["inbox", "sent", "empty"]);
    expect(calculateReviewTotals(all, ids, true)).toEqual({
      messages: 150, bytes: 1500, folders: 2,
      dateFrom: "2012-01-01T00:00:00Z", dateTo: "2024-01-01T00:00:00Z",
    });
  });
  it("returns null dates when all effective rows have null dates", () => {
    const ids = new Set(["empty"]);
    expect(calculateReviewTotals(all, ids, false)).toEqual({
      messages: 0, bytes: 0, folders: 1, dateFrom: null, dateTo: null,
    });
  });
});
