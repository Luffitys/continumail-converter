// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { useMemo } from "react";
import { Button } from "@/components/ui/button";
import { calculateReviewTotals } from "@/lib/review";
import { formatBytes, formatDateRange } from "@/lib/format";
import type { ScanResult } from "@/lib/parse";

interface ReviewViewProps {
  scan: ScanResult;
  checkedIds: Set<string>;
  skipEmpty: boolean;
  onToggle: (id: string) => void;
  onSkipEmptyChange: (v: boolean) => void;
  onContinue: () => void;
  onBack: () => void;
}

export function ReviewView({
  scan, checkedIds, skipEmpty, onToggle, onSkipEmptyChange, onContinue, onBack,
}: ReviewViewProps) {
  const totals = useMemo(
    () => calculateReviewTotals(scan.sources, checkedIds, skipEmpty),
    [scan.sources, checkedIds, skipEmpty],
  );
  const canContinue = totals.folders > 0;

  return (
    <div className="flex flex-1 flex-col min-h-0">
      <h1 className="text-xl font-semibold text-foreground">Mail folders found</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        {totals.messages.toLocaleString()} messages · ~{formatBytes(totals.bytes)} estimated PST ·{" "}
        {totals.folders} folder{totals.folders === 1 ? "" : "s"} ·{" "}
        {formatDateRange(totals.dateFrom, totals.dateTo)}
      </p>

      <div className="mt-4 min-h-0 flex-1 overflow-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-card text-left text-xs text-light-gray">
            <tr>
              <th className="w-8 px-3 py-2"></th>
              <th className="px-2 py-2">Folder</th>
              <th className="px-2 py-2 text-right">Messages</th>
              <th className="px-2 py-2">Dates</th>
              <th className="px-3 py-2 text-right">Estimated PST size</th>
            </tr>
          </thead>
          <tbody>
            {scan.sources.map((s) => {
              const isEmpty = s.messages === 0;
              const hiddenBySkip = isEmpty && skipEmpty;
              return (
                <tr key={s.id} className={hiddenBySkip ? "text-light-gray/60 line-through" : "text-foreground"}>
                  <td className="px-3 py-1.5">
                    <input
                      type="checkbox"
                      checked={checkedIds.has(s.id)}
                      onChange={() => onToggle(s.id)}
                      aria-label={`Include ${s.displayName}`}
                    />
                  </td>
                  <td className="px-2 py-1.5">
                    {s.displayName}
                    {isEmpty && (
                      <span className="ml-2 rounded bg-muted px-1.5 py-0.5 text-[10px] uppercase text-muted-foreground">empty</span>
                    )}
                  </td>
                  <td className="px-2 py-1.5 text-right">{s.messages.toLocaleString()}</td>
                  <td className="px-2 py-1.5">{formatDateRange(s.dateFrom, s.dateTo)}</td>
                  <td className="px-3 py-1.5 text-right">{formatBytes(s.bytes)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="mt-3">
        <label className="flex items-center gap-2 text-sm text-foreground">
          <input type="checkbox" checked={skipEmpty} onChange={(e) => onSkipEmptyChange(e.target.checked)} />
          Skip empty folders
        </label>
        <p className="ml-6 text-xs text-light-gray">Empty folders are skipped if checked.</p>
      </div>

      {!canContinue && (
        <p className="mt-3 text-sm text-muted-foreground">No messages found in the selected files.</p>
      )}

      <div className="mt-4 flex items-center justify-end">
        <div className="flex gap-3">
          <Button variant="outline" onClick={onBack}>‹ Back</Button>
          <Button disabled={!canContinue} onClick={onContinue}>Continue to Options ›</Button>
        </div>
      </div>
    </div>
  );
}
