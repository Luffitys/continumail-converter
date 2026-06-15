// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useState } from "react";
import { scan as runScan } from "./engine";
import { defaultOptions, type OptionsState, FLATTEN_SOURCE_ID } from "./options";
import type { FileStat } from "./types";
import type { ScanResult } from "./parse";

export type FlowStage = "select" | "scanning" | "review" | "options" | "scanError";

export interface PreConvertState {
  inputFiles: FileStat[];
  outputPath: string | null;
  stage: FlowStage;
  scan: ScanResult | null;
  errorMessage: string | null;
  checkedIds: Set<string>;
  skipEmpty: boolean;
  options: OptionsState;
  scanProgress: { bytes: number; totalBytes: number } | null;
}

function initialState(): PreConvertState {
  return {
    inputFiles: [],
    outputPath: null,
    stage: "select",
    scan: null,
    errorMessage: null,
    checkedIds: new Set(),
    skipEmpty: true,
    options: defaultOptions(),
    scanProgress: null,
  };
}

export function useScan() {
  const [state, setState] = useState<PreConvertState>(() => initialState());

  const setInputFiles = useCallback(
    (inputFiles: FileStat[]) => setState((s) => ({ ...s, inputFiles })),
    [],
  );
  const setOutputPath = useCallback(
    (outputPath: string | null) => setState((s) => ({ ...s, outputPath })),
    [],
  );

  const continueToScan = useCallback(async () => {
    const paths = state.inputFiles.map((f) => f.path);
    if (paths.length === 0) {
      setState((s) => ({ ...s, errorMessage: "Select at least one .mbox file." }));
      return;
    }
    setState((s) => ({ ...s, stage: "scanning", errorMessage: null, scanProgress: null }));
    try {
      // Guard the progress update on stage so a late/queued event after the scan
      // resolves can't leak a bar into Review or ScanError.
      const result = await runScan(paths, (p) =>
        setState((s) => (s.stage === "scanning" ? { ...s, scanProgress: p } : s)),
      );
      // Fresh selection + default options each scan.
      setState((s) => ({
        ...s,
        stage: "review",
        scan: result,
        checkedIds: new Set(result.sources.map((x) => x.id)),
        skipEmpty: true,
        options: defaultOptions(),
        scanProgress: null,
      }));
    } catch (e) {
      const errorMessage = e instanceof Error ? e.message : String(e);
      setState((s) => ({ ...s, stage: "scanError", errorMessage, scanProgress: null }));
    }
  }, [state.inputFiles]);

  const toggleChecked = useCallback((id: string) => {
    setState((s) => {
      const next = new Set(s.checkedIds);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return { ...s, checkedIds: next };
    });
  }, []);

  const setSkipEmpty = useCallback(
    (skipEmpty: boolean) => setState((s) => ({ ...s, skipEmpty })),
    [],
  );

  const setOptions = useCallback(
    (patch: Partial<OptionsState>) => setState((s) => ({ ...s, options: { ...s.options, ...patch } })),
    [],
  );

  // Routes the flatten pseudo-folder rename to flattenFolderName; everything
  // else updates the per-source renames map.
  const setRename = useCallback((sourceId: string, name: string) => {
    setState((s) => {
      if (sourceId === FLATTEN_SOURCE_ID) {
        return { ...s, options: { ...s.options, flattenFolderName: name } };
      }
      return { ...s, options: { ...s.options, renames: { ...s.options.renames, [sourceId]: name } } };
    });
  }, []);

  const continueToOptions = useCallback(() => setState((s) => ({ ...s, stage: "options" })), []);
  const backToReview = useCallback(() => setState((s) => ({ ...s, stage: "review" })), []);

  const back = useCallback(
    () => setState((s) => ({ ...s, stage: "select", scan: null, errorMessage: null, scanProgress: null })),
    [],
  );
  const resetFlow = useCallback(() => setState(initialState()), []);

  return {
    state,
    setInputFiles,
    setOutputPath,
    continueToScan,
    toggleChecked,
    setSkipEmpty,
    setOptions,
    setRename,
    continueToOptions,
    backToReview,
    back,
    resetFlow,
  };
}
