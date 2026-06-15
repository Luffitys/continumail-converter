// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { toScanResult, type ScanResult } from "./parse";

export type ScanLineEvent =
  | { type: "scanProgress"; bytes: number; totalBytes: number }
  | { type: "scan"; result: ScanResult };

/** Classify one JSON-Lines stdout line from `scan --progress`: a `scanProgress`
 * advisory, the final `scan` result, or null for blank / non-JSON / partial-
 * pretty / unknown-type lines. Pure — no Tauri imports. */
export function parseScanLine(line: string): ScanLineEvent | null {
  const trimmed = line.trim();
  if (trimmed.length === 0 || trimmed[0] !== "{") return null;
  let obj: unknown;
  try {
    obj = JSON.parse(trimmed);
  } catch {
    return null;
  }
  if (typeof obj !== "object" || obj === null) return null;
  const o = obj as Record<string, unknown>;
  if (o.type === "scanProgress") {
    if (typeof o.bytes !== "number" || typeof o.totalBytes !== "number") return null;
    return { type: "scanProgress", bytes: o.bytes, totalBytes: o.totalBytes };
  }
  if (o.type === "scan") return { type: "scan", result: toScanResult(o) };
  return null;
}
