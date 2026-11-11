"use client";

import type { ButtonHTMLAttributes, MouseEventHandler } from "react";

export type GridAction =
  | "view"
  | "details"
  | "edit"
  | "delete"
  | "reset"
  | "activate"
  | "deactivate"
  | "retire"
  | "award";

const labels: Record<GridAction, string> = {
  view: "View",
  details: "Details",
  edit: "Edit",
  delete: "Delete",
  reset: "Send reset link",
  activate: "Activate",
  deactivate: "Deactivate",
  retire: "Retire",
  award: "Award points",
};

function GridActionIcon({ action }: { action: GridAction }) {
  const common = {
    "aria-hidden": true as const,
    className: "size-4",
    fill: "none",
    stroke: "currentColor",
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    strokeWidth: 1.75,
    viewBox: "0 0 24 24",
  };

  switch (action) {
    case "view":
    case "details":
      return (
        <svg {...common}>
          <path d="M2.5 12s3.5-7 9.5-7 9.5 7 9.5 7-3.5 7-9.5 7-9.5-7-9.5-7Z" />
          <circle cx="12" cy="12" r="3" />
        </svg>
      );
    case "edit":
      return (
        <svg {...common}>
          <path d="M12 20h9" />
          <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
        </svg>
      );
    case "delete":
      return (
        <svg {...common}>
          <path d="M3 6h18" />
          <path d="M8 6V4h8v2" />
          <path d="M19 6l-1 14H6L5 6" />
          <path d="M10 11v6M14 11v6" />
        </svg>
      );
    case "reset":
      return (
        <svg {...common}>
          <circle cx="8" cy="15" r="3.5" />
          <path d="M11.5 12.5 21 3" />
          <path d="M16.5 3H21v4.5" />
        </svg>
      );
    case "activate":
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="8" />
          <path d="m9 12 2 2 4-4" />
        </svg>
      );
    case "deactivate":
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="8" />
          <path d="M9 12h6" />
        </svg>
      );
    case "retire":
      return (
        <svg {...common}>
          <path d="M4 8h16v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2Z" />
          <path d="M10 12h4" />
          <path d="M9 8V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V8" />
        </svg>
      );
    case "award":
      return (
        <svg {...common}>
          <path d="m12 3 2.1 4.3 4.7.7-3.4 3.3.8 4.7L12 13.9 7.8 16l.8-4.7-3.4-3.3 4.7-.7Z" />
        </svg>
      );
  }
}

export function GridActionButton({
  action,
  label,
  className,
  onClick,
  ...props
}: {
  action: GridAction;
  label?: string;
  onClick?: MouseEventHandler<HTMLButtonElement>;
} & Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children" | "onClick">) {
  const title = label ?? labels[action];
  return (
    <button
      aria-label={title}
      className={`inline-flex size-11 items-center justify-center rounded-xl border border-line bg-surface text-foreground transition hover:border-accent hover:text-accent disabled:opacity-60 ${className ?? ""}`}
      onClick={onClick}
      title={title}
      type="button"
      {...props}
    >
      <GridActionIcon action={action} />
    </button>
  );
}

export function GridActionGroup({ children }: { children: React.ReactNode }) {
  return <span className="flex flex-wrap items-center gap-2">{children}</span>;
}
