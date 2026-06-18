// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { TriangleAlert, FolderOpen } from "lucide-react";
import { Button } from "@/components/ui/button";
import { openFolder } from "@/lib/engine";
import type { ConvertState } from "@/lib/useConvert";

export function ErrorView({ state, onConvertAnother }: { state: ConvertState; onConvertAnother: () => void }) {
  return (
    <div className="flex flex-1 flex-col">
      <div className="flex items-center gap-3.5">
        <div className="flex size-11 items-center justify-center rounded-full bg-destructive">
          <TriangleAlert className="size-6 text-white" />
        </div>
        <div>
          <h1 className="text-xl font-semibold text-foreground">Conversion failed</h1>
          <p className="text-sm text-muted-foreground">{state.errorMessage ?? "Something went wrong."}</p>
        </div>
      </div>
      <div className="mt-auto flex gap-3 pt-5">
        {state.outputDir && (
          <Button variant="outline" onClick={() => void openFolder(state.outputDir)}>
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
