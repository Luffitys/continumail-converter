// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

/** Human-readable byte size: "512 B", "1.5 KB", "5.0 MB", … */
export function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let v = n / 1024;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i++;
  }
  return `${v.toFixed(1)} ${units[i]}`;
}

const EM_DASH = "—";
const EN_DASH = "–";

/** ISO timestamp -> "YYYY-MM-DD"; null -> em dash. */
export function formatShortDate(iso: string | null): string {
  if (!iso) return EM_DASH;
  return iso.slice(0, 10);
}

/** "from – to"; same day or one-sided -> single date; both null -> em dash. */
export function formatDateRange(from: string | null, to: string | null): string {
  if (!from && !to) return EM_DASH;
  if (from && !to) return formatShortDate(from);
  if (!from && to) return formatShortDate(to);
  const a = formatShortDate(from);
  const b = formatShortDate(to);
  if (a === b) return a;
  return `${a} ${EN_DASH} ${b}`;
}
