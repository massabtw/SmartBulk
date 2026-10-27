import React from "react";

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
}

export function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  return (
    <header className="px-1 sm:px-3 pt-6 pb-3 flex items-center justify-between flex-wrap gap-3">
      <div>
        <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-zinc-100">{title}</h1>
        {subtitle && <p className="text-xs text-zinc-400 mt-0.5">{subtitle}</p>}
      </div>
      {action && <div className="flex items-center gap-2 flex-wrap">{action}</div>}
    </header>
  );
}

