// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { Ban, FolderOpen } from "lucide-react";
import { Button } from "@/components/ui/button";
import { openFolder } from "@/lib/engine";
import type { ConvertState } from "@/lib/useConvert";

export function CancelledView({ state, onConvertAnother }: { state: ConvertState; onConvertAnother: () => void }) {
  return (
    <div className="flex flex-1 flex-col">
      <div className="flex items-center gap-3.5">
        <div className="flex size-11 items-center justify-center rounded-full bg-muted">
          <Ban className="size-6 text-foreground" />
        </div>
        <div>
          <h1 className="text-xl font-semibold text-foreground">Conversion cancelled</h1>
          <p className="text-sm text-muted-foreground">
            The in-progress PST was deleted.
            {state.outputs.length > 0
              ? ` ${state.outputs.length} completed part${state.outputs.length === 1 ? "" : "s"} remain in the output folder.`
              : ""}
          </p>
        </div>
      </div>
      <div className="mt-auto flex gap-3 pt-5">
        {(state.outputs.length > 0 || state.outputDir) && (
          <Button variant="outline" onClick={() => void openFolder(state.outputs[0] ?? state.outputDir)}>
            <FolderOpen /> Open folder
          </Button>
        )}
        <Button variant="outline" onClick={onConvertAnother}>
          Convert another
        </Button>
      </div>
    </div>
  );
}
