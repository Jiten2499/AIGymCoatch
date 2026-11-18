import Link from "next/link";
import type { Metadata } from "next";
import { DocSearch } from "../../components/docs/DocSearch";
import { articles, searchItems } from "../../content/docs/catalog";

export const metadata: Metadata = {
  title: "How can we help you?",
  description: "Find guides, tutorials and answers for Growventur and GrowFit.",
  alternates: { canonical: "/docs" },
};

export default function DocsHomePage() {
  const popular = articles.filter((article) => article.popular);
  const recent = [...articles].reverse().slice(0, 6);
  return (
    <main className="docs-wrap">
      <section className="docs-hero">
        <p className="docs-kicker">Growventur</p>
        <h1>How can we help you?</h1>
        <p>Find guides, tutorials and answers to help you get the most out of Growventur and GrowFit.</p>
        <DocSearch items={searchItems()} label="Search all guides" />
      </section>
      <section className="docs-cards">
        <Link className="docs-card" href="/docs/growventur">
          <span className="docs-kicker">Web</span>
          <strong>Growventur</strong>
          <span>Business management platform.</span>
        </Link>
        <Link className="docs-card" href="/docs/growfit">
          <span className="docs-kicker">Mobile</span>
          <strong>GrowFit</strong>
          <span>Fitness mobile application.</span>
        </Link>
      </section>
      <section className="docs-columns">
        <article className="docs-panel">
          <h2>Popular articles</h2>
          <ul>
            {popular.map((article) => (
              <li key={`${article.product}-${article.slug}`}>
                <Link href={`/docs/${article.product}/${article.slug}`}>{article.title}</Link>
              </li>
            ))}
          </ul>
        </article>
        <article className="docs-panel">
          <h2>Recently updated</h2>
          <ul>
            {recent.map((article) => (
              <li key={`${article.product}-${article.slug}`}>
                <Link href={`/docs/${article.product}/${article.slug}`}>{article.title}</Link>
              </li>
            ))}
          </ul>
        </article>
      </section>
      <section className="docs-panel" style={{ marginTop: "1rem" }}>
        <h2>Need an account change?</h2>
        <p className="docs-muted">
          Use Forgot password on the sign-in page. Workspace access, memberships, and roles are changed by someone with Staff & Access in your business.
        </p>
      </section>
    </main>
  );
}
