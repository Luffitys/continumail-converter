// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

interface StatChipProps {
  label: string;
  value: number;
}

export function StatChip({ label, value }: StatChipProps) {
  return (
    <div className="flex-1 rounded-[10px] border border-border bg-card px-3 py-2.5">
      <div className="text-lg font-bold text-foreground">{value.toLocaleString()}</div>
      <div className="text-[11px] text-light-gray">{label}</div>
    </div>
  );
}
