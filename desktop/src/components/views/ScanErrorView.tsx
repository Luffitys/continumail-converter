// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import { TriangleAlert, ArrowLeft } from "lucide-react";
import { Button } from "@/components/ui/button";

interface ScanErrorViewProps {
  message: string;
  onBack: () => void;
}

export function ScanErrorView({ message, onBack }: ScanErrorViewProps) {
  return (
    <div className="flex flex-1 flex-col">
      <div className="flex items-center gap-2 text-destructive">
        <TriangleAlert className="size-5" />
        <h1 className="text-xl font-semibold">Couldn't scan these files</h1>
      </div>
      <p className="mt-3 max-w-prose text-sm text-foreground">{message}</p>
      <div className="mt-auto pt-5">
        <Button variant="outline" onClick={onBack}>
          <ArrowLeft /> Back
        </Button>
      </div>
    </div>
  );
}
