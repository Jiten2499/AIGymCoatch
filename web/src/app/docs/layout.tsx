import type { Metadata } from "next";
import Link from "next/link";
import { productMeta } from "../../content/docs/types";
import "./docs.css";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "https://growventur.com"),
  title: { default: "Documentation", template: "%s · Growventur Docs" },
  description: "Guides for Growventur and GrowFit.",
};

export default function DocsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="docs">
      <header className="docs-top">
        <Link className="docs-brand" href="/docs">
          Growventur Docs
        </Link>
        <nav aria-label="Documentation products" className="docs-nav">
          <Link href="/docs/growventur">{productMeta.growventur.name}</Link>
          <Link href="/docs/growfit">{productMeta.growfit.name}</Link>
          <Link href="/">Home</Link>
        </nav>
      </header>
      {children}
    </div>
  );
}
