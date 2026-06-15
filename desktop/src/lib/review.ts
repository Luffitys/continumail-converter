// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import type { SourceRow } from "./types";

export interface ReviewTotals {
  messages: number;
  bytes: number;
  folders: number;
  dateFrom: string | null;
  dateTo: string | null;
}

/** The rows that will actually become PST folders: checked, and (when
 * skipEmpty) non-empty. This one predicate backs both the totals and the
 * generated config so they cannot disagree. */
export function effectiveRows(
  sources: SourceRow[],
  checkedIds: Set<string>,
  skipEmpty: boolean,
): SourceRow[] {
  return sources.filter(
    (r) => checkedIds.has(r.id) && !(skipEmpty && r.messages === 0),
  );
}

export function calculateReviewTotals(
  sources: SourceRow[],
  checkedIds: Set<string>,
  skipEmpty: boolean,
): ReviewTotals {
  const rows = effectiveRows(sources, checkedIds, skipEmpty);
  let messages = 0;
  let bytes = 0;
  let dateFrom: string | null = null;
  let dateTo: string | null = null;
  for (const r of rows) {
    messages += r.messages;
    bytes += r.bytes;
    // ISO-8601 UTC strings compare correctly lexicographically.
    if (r.dateFrom && (dateFrom === null || r.dateFrom < dateFrom)) dateFrom = r.dateFrom;
    if (r.dateTo && (dateTo === null || r.dateTo > dateTo)) dateTo = r.dateTo;
  }
  return { messages, bytes, folders: rows.length, dateFrom, dateTo };
}
