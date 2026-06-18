// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

import clsx from "clsx";

interface CardProps {
  title?: string;
  value?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}

export function Card({ title, value, children, className }: CardProps) {
  return (
    <div className={clsx("rounded-lg border border-border bg-card p-6 shadow-sm", className)}>
      {(title !== undefined || value !== undefined) && (
        <div>
          {title && <p className="text-sm font-medium text-muted-foreground">{title}</p>}
          {value !== undefined && (
            <p className="mt-1 text-2xl font-semibold text-foreground font-serif">{value}</p>
          )}
        </div>
      )}
      {children}
    </div>
  );
}
