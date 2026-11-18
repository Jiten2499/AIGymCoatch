import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArticleBody, headingIds } from "../../../../components/docs/ArticleBody";
import { DocSidebar } from "../../../../components/docs/DocSidebar";
import { articleHref, articles, findArticle, neighbors } from "../../../../content/docs/catalog";
import { productMeta, type DocProduct } from "../../../../content/docs/types";

function asProduct(value: string): DocProduct | null {
  return value === "growventur" || value === "growfit" ? value : null;
}

export function generateStaticParams() {
  return articles.map((article) => ({ product: article.product, slug: article.slug.split("/") }));
}

export async function generateMetadata({ params }: { params: Promise<{ product: string; slug: string[] }> }): Promise<Metadata> {
  const { product, slug } = await params;
  const article = findArticle(product, slug.join("/"));
  if (!article) {
    return {};
  }
  return {
    title: article.title,
    description: article.description,
    alternates: { canonical: articleHref(article) },
  };
}

export default async function DocArticlePage({ params }: { params: Promise<{ product: string; slug: string[] }> }) {
  const { product, slug } = await params;
  const known = asProduct(product);
  const article = known ? findArticle(known, slug.join("/")) : undefined;
  if (!known || !article) {
    notFound();
  }
  const nearby = neighbors(article);
  const related = article.related
    .map((item) => findArticle(known, item))
    .filter((item) => item !== undefined);
  return (
    <div className="docs-layout docs-article-layout">
      <aside>
        <DocSidebar current={article.slug} product={known} />
      </aside>
      <article className="docs-content">
        <p className="docs-kicker">
          <Link href="/docs">Docs</Link> / <Link href={`/docs/${known}`}>{productMeta[known].name}</Link> / {article.category}
        </p>
        <h1>{article.title}</h1>
        <p>{article.description}</p>
        <p className="docs-muted">For {article.roles.join(", ")}. Updated {article.updated}.</p>
        <ArticleBody sections={article.sections} />
        {related.length > 0 ? (
          <section>
            <h2 id="related-articles">Related articles</h2>
            <ul>
              {related.map((item) => (
                <li key={item.slug}>
                  <Link href={articleHref(item)}>{item.title}</Link>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
        <nav className="docs-pager" aria-label="Article">
          {nearby.previous ? <Link href={articleHref(nearby.previous)}>Previous: {nearby.previous.title}</Link> : <span />}
          {nearby.next ? <Link href={articleHref(nearby.next)}>Next: {nearby.next.title}</Link> : <span />}
        </nav>
      </article>
      <aside className="docs-toc">
        <nav aria-label="On this page" className="docs-side">
          <h2>On this page</h2>
          {headingIds(article.sections).map((item) => (
            <a href={`#${item.id}`} key={item.id}>
              {item.heading}
            </a>
          ))}
          {related.length > 0 ? <a href="#related-articles">Related articles</a> : null}
        </nav>
      </aside>
    </div>
  );
}
