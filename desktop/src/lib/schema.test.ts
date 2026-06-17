// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, it, expect } from "vitest";
import { checkSchemaVersion, EXPECTED_SCHEMA_VERSION } from "./schema";

describe("checkSchemaVersion", () => {
  it("returns null for the expected version", () => {
    expect(checkSchemaVersion(EXPECTED_SCHEMA_VERSION)).toBeNull();
  });

  it("warns when the version is newer", () => {
    const msg = checkSchemaVersion(EXPECTED_SCHEMA_VERSION + 1);
    expect(msg).toMatch(/newer/i);
  });

  it("warns when the version is older", () => {
    const msg = checkSchemaVersion(EXPECTED_SCHEMA_VERSION - 1);
    expect(msg).toMatch(/older/i);
  });

  it("warns when the version is missing", () => {
    const msg = checkSchemaVersion(undefined);
    expect(msg).toMatch(/no schemaversion|missing/i);
  });
});
