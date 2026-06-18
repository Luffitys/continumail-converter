// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

const MB = 1024 * 1024;

/** "1.9 MB/s" (estimated output throughput); "—" when unknown. */
export function formatRate(bps: number | null): string {
  if (bps == null || !Number.isFinite(bps)) return "—";
  return `${(bps / MB).toFixed(1)} MB/s`;
}

/** "45s" / "1m 20s"; "—" when unknown. */
export function formatDuration(sec: number | null): string {
  if (sec == null || !Number.isFinite(sec) || sec < 0) return "—";
  const s = Math.round(sec);
  if (s < 60) return `${s}s`;
  return `${Math.floor(s / 60)}m ${s % 60}s`;
}

export interface RateSample {
  ms: number;
  bytes: number;
  converted: number;
}

/** Throughput over the most recent `windowMs`: latest sample vs the oldest
 * sample still inside the window. null when not computable. */
export function windowedRate(
  samples: RateSample[],
  windowMs: number,
): { bytesPerSec: number | null; msgPerSec: number | null } {
  if (samples.length < 2) return { bytesPerSec: null, msgPerSec: null };
  const latest = samples[samples.length - 1];
  let oldest = latest;
  for (const s of samples) {
    if (latest.ms - s.ms <= windowMs) {
      oldest = s; // first (earliest) sample inside the window
      break;
    }
  }
  const dtSec = (latest.ms - oldest.ms) / 1000;
  if (dtSec <= 0) return { bytesPerSec: null, msgPerSec: null };
  const db = latest.bytes - oldest.bytes;
  const dm = latest.converted - oldest.converted;
  return {
    bytesPerSec: db < 0 ? null : db / dtSec,
    msgPerSec: dm < 0 ? null : dm / dtSec,
  };
}

/** One easing step toward target; snaps when within epsilon. k is clamped to
 * [0,1] so an out-of-range factor can never overshoot the target. */
export function easeToward(current: number, target: number, k = 0.15, epsilon = 0.5): number {
  if (Math.abs(target - current) <= epsilon) return target;
  const kk = Math.max(0, Math.min(1, k));
  return current + (target - current) * kk;
}

/** Seconds remaining from a windowed message rate; null until a stable rate
 * exists (rate null/<=0, converted < 5, or sampleSpanMs < 2000). */
export function etaFromWindowedRate(
  converted: number,
  total: number,
  msgPerSec: number | null,
  sampleSpanMs: number,
): number | null {
  if (msgPerSec == null || msgPerSec <= 0 || converted < 5 || sampleSpanMs < 2000) return null;
  return Math.round(Math.max(0, total - converted) / msgPerSec);
}

/** Human elapsed time (verbose): "0 seconds" / "4 minutes 6 seconds" / "1 hour 10 minutes". */
export function formatElapsed(ms: number): string {
  const s = Math.max(0, Math.round(ms / 1000));
  const u = (n: number, unit: string) => `${n} ${unit}${n === 1 ? "" : "s"}`;
  if (s < 60) return u(s, "second");
  if (s < 3600) {
    const m = Math.floor(s / 60);
    const r = s % 60;
    return r === 0 ? u(m, "minute") : `${u(m, "minute")} ${u(r, "second")}`;
  }
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return m === 0 ? u(h, "hour") : `${u(h, "hour")} ${u(m, "minute")}`;
}
