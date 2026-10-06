"use client";

import { BusinessShell } from "../../components/BusinessShell";

export default function BusinessLayout({ children }: { children: React.ReactNode }) {
  return <BusinessShell>{children}</BusinessShell>;
}
