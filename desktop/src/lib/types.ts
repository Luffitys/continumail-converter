// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

export interface ScanTotals {
  messages: number;
  bytes: number;
  sourceBytes: number;
  sources: number;
}

export interface SourceRow {
  id: string;
  path: string;
  displayName: string;
  messages: number;
  bytes: number;
  sourceBytes: number;
  dateFrom: string | null;
  dateTo: string | null;
  warnings: number;
  skipped: number;
}

export interface SourceConfigEntry {
  path: string;
  type: "mbox";
  targetFolder?: string;
}

export interface OutputGroupConfig {
  name: string;
  maxSizeMB: number;
  folderMapping: "mirror" | "flatten";
  includeEmptyFolders: boolean;
  sources: SourceConfigEntry[];
}

export interface ConversionConfig {
  outputs: OutputGroupConfig[];
}

export interface FileStat {
  path: string;
  size: number;
}

export type ConvertEvent =
  | { type: "started"; input?: string; outputDirectory?: string }
  | { type: "scan"; totalMessages: number }
  | {
      type: "progress";
      converted: number;
      total: number;
      warnings: number;
      skipped: number;
      bytes: number;
      currentSource?: string | null;
      currentFolder?: string | null;
    }
  | { type: "warning"; source: string; identifier: string; reason: string }
  | {
      type: "done";
      converted: number;
      skipped: number;
      warnings: number;
      outputs: string[];
      outputDirectory?: string;
      report?: string;
      elapsedMs: number;
    }
  | { type: "error"; stage?: string; message: string; fatal: boolean }
  | {
      type: "cancelled";
      deleted: string[];
      outputs: string[];
      converted: number;
      skipped: number;
      warnings: number;
      elapsedMs?: number;
    };
