// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

/** JSON-Lines contract version this GUI build expects from the CLI sidecar. */
export const EXPECTED_SCHEMA_VERSION = 1;

/**
 * Permissive forward-compat check for the CLI's `schemaVersion`. Returns a
 * human-readable warning string when the version is missing or not the one this
 * build expects, else null. Callers warn (console) once and keep parsing — the
 * GUI bundles its exact sidecar, so a mismatch is almost always a dev artifact.
 * Pure: no Tauri imports.
 */
export function checkSchemaVersion(v: number | undefined): string | null {
  if (v === undefined) {
    return `Engine sent no schemaVersion (expected ${EXPECTED_SCHEMA_VERSION}); parsing optimistically.`;
  }
  if (v === EXPECTED_SCHEMA_VERSION) return null;
  if (v > EXPECTED_SCHEMA_VERSION) {
    return `Engine schemaVersion ${v} is newer than this app expects (${EXPECTED_SCHEMA_VERSION}); some fields may be ignored.`;
  }
  return `Engine schemaVersion ${v} is older than this app expects (${EXPECTED_SCHEMA_VERSION}); output may be misread.`;
}
