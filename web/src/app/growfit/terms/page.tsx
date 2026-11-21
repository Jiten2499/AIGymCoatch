import type { Metadata } from "next";
import Link from "next/link";
import { growfitSupportEmail, growfitTerms, growfitTermsUpdated } from "../../../content/legal/growfitTerms";

const description =
  "Terms of Service for the GrowFit fitness app, including accounts, workouts, trainer tools, messaging, and acceptable use.";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "https://growventur.com"),
  title: "GrowFit Terms of Service | Growventur",
  description,
  alternates: { canonical: "/growfit/terms" },
  robots: { index: true, follow: true },
};

export default function GrowFitTermsPage() {
  return (
    <div className="scroll-smooth bg-background text-foreground">
      <a className="sr-only focus:not-sr-only focus:absolute focus:z-10 focus:m-3 focus:rounded-full focus:bg-accent focus:px-4 focus:py-2 focus:text-background" href="#terms">
        Skip to terms
      </a>
      <header className="border-b border-line">
        <div className="mx-auto flex w-full max-w-3xl items-center justify-between gap-4 px-6 py-5">
          <Link className="text-sm font-semibold tracking-[0.18em] text-accent uppercase" href="/">
            Growventur
          </Link>
          <nav aria-label="Site" className="flex gap-4 text-sm">
            <Link className="text-accent" href="/docs/growfit">
              GrowFit guides
            </Link>
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-3xl px-6 py-12" id="terms">
        <nav aria-label="Breadcrumb" className="text-sm text-muted">
          <ol className="flex flex-wrap items-center gap-2">
            <li>
              <Link className="text-accent" href="/">
                Home
              </Link>
            </li>
            <li aria-hidden="true">/</li>
            <li>
              <Link className="text-accent" href="/docs/growfit">
                GrowFit
              </Link>
            </li>
            <li aria-hidden="true">/</li>
            <li>Terms of Service</li>
          </ol>
        </nav>
        <p className="mt-8 text-sm font-semibold tracking-[0.16em] text-accent uppercase">GrowFit</p>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight sm:text-5xl">GrowFit Terms of Service</h1>
        <p className="mt-4 text-sm text-muted">Last updated: {growfitTermsUpdated}</p>
        <p className="mt-6 text-lg leading-8 text-muted">
          These terms explain the rules for using the GrowFit fitness app. Please read them before you create an account or continue using the app.
        </p>
        <nav aria-label="Table of contents" className="mt-10 rounded-3xl border border-line bg-surface p-6">
          <h2 className="text-sm font-semibold tracking-[0.14em] text-accent uppercase">Contents</h2>
          <ol className="mt-4 grid gap-2 sm:grid-cols-2">
            {growfitTerms.map((section, index) => (
              <li key={section.id}>
                <a className="text-accent underline-offset-4 hover:underline" href={`#${section.id}`}>
                  {index + 1}. {section.title}
                </a>
              </li>
            ))}
          </ol>
        </nav>
        <article className="mt-12 space-y-12">
          {growfitTerms.map((section, index) => (
            <section className="scroll-mt-8" id={section.id} key={section.id}>
              <h2 className="text-2xl font-semibold tracking-tight">
                {index + 1}. {section.title}
              </h2>
              <div className="mt-4 space-y-4 leading-7 text-foreground/90">
                {section.blocks.map((block, blockIndex) =>
                  block.type === "ul" ? (
                    <ul className="list-disc space-y-2 pl-5" key={`${section.id}-list-${blockIndex}`}>
                      {block.items.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  ) : (
                    <p key={`${section.id}-p-${blockIndex}`}>{block.text}</p>
                  ),
                )}
                {section.id === "privacy" ? <PrivacyNotice /> : null}
                {section.id === "contact" ? <ContactNotice /> : null}
              </div>
            </section>
          ))}
        </article>
      </main>
      <footer className="border-t border-line px-6 py-6 text-sm text-muted">
        <div className="mx-auto flex w-full max-w-3xl flex-wrap gap-4">
          <span>Growventur</span>
          <Link className="text-accent" href="/docs">
            Documentation
          </Link>
          <Link className="text-accent" href="/growfit/terms">
            GrowFit Terms of Service
          </Link>
          <Link className="text-accent" href="/growfit/privacy">
            GrowFit Privacy Policy
          </Link>
        </div>
      </footer>
    </div>
  );
}

function PrivacyNotice() {
  return (
    <p>
      The GrowFit Privacy Policy is published at{" "}
      <Link className="text-accent underline-offset-4 hover:underline" href="/growfit/privacy">
        growventur.com/growfit/privacy
      </Link>
      .
    </p>
  );
}

function ContactNotice() {
  if (growfitSupportEmail) {
    return (
      <p>
        Email:{" "}
        <a className="text-accent underline-offset-4 hover:underline" href={`mailto:${growfitSupportEmail}`}>
          {growfitSupportEmail}
        </a>
      </p>
    );
  }

  return <p>Official GrowFit support email: not configured yet. Add the public support address before using this page in Google Play Console.</p>;
}
