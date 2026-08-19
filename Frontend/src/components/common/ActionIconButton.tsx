"use client";

import type { ButtonHTMLAttributes, SVGProps } from "react";

type ActionIconButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  icon: "edit" | "delete";
};

export const actionIconButtonClass =
  "inline-flex size-9 shrink-0 items-center justify-center rounded-md border border-slate-200 text-slate-600 transition hover:border-slate-300 hover:bg-slate-50 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800";

export const dangerActionIconButtonClass =
  "inline-flex size-9 shrink-0 items-center justify-center rounded-md border border-rose-100 text-rose-600 transition hover:border-rose-200 hover:bg-rose-50 disabled:opacity-60 dark:border-rose-500/25 dark:text-rose-300 dark:hover:bg-rose-500/10";

export function EditActionIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg aria-hidden="true" fill="none" viewBox="0 0 24 24" {...props}>
      <path d="M13.7 5.3 18.7 10.3M4 20l4.1-.8c.5-.1.9-.3 1.3-.7l9.9-9.9a2.1 2.1 0 0 0 0-3l-.9-.9a2.1 2.1 0 0 0-3 0L5.5 13.6c-.4.4-.6.8-.7 1.3L4 20Z" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" />
    </svg>
  );
}

export function DeleteActionIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg aria-hidden="true" fill="none" viewBox="0 0 24 24" {...props}>
      <path d="M4 7h16M9 7V5.8C9 4.8 9.8 4 10.8 4h2.4c1 0 1.8.8 1.8 1.8V7m-8 0 .7 11.2c.1 1 1 1.8 2 1.8h4.6c1.1 0 1.9-.8 2-1.8L17 7M10 11v5M14 11v5" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" />
    </svg>
  );
}

export function AddActionIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg aria-hidden="true" fill="none" viewBox="0 0 24 24" {...props}>
      <path d="M12 5v14M5 12h14" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.4" />
    </svg>
  );
}

export default function ActionIconButton({ className = "", icon, title, type = "button", ...props }: ActionIconButtonProps) {
  const isDelete = icon === "delete";
  const baseClass = isDelete ? dangerActionIconButtonClass : actionIconButtonClass;
  const Icon = isDelete ? DeleteActionIcon : EditActionIcon;

  return (
    <button className={`${baseClass} ${className}`.trim()} title={title} type={type} {...props}>
      <Icon className="size-5" />
    </button>
  );
}
