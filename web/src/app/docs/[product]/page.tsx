import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DocSearch } from "../../../components/docs/DocSearch";
import { DocSidebar } from "../../../components/docs/DocSidebar";
import { categoriesFor, searchItems } from "../../../content/docs/catalog";
import { productMeta, type DocProduct } from "../../../content/docs/types";

function asProduct(value: string): DocProduct | null {
  return value === "growventur" || value === "growfit" ? value : null;
}

export function generateStaticParams() {
  return [{ product: "growventur" }, { product: "growfit" }];
}

export async function generateMetadata({ params }: { params: Promise<{ product: string }> }): Promise<Metadata> {
  const { product } = await params;
  const known = asProduct(product);
  if (!known) {
    return {};
  }
  return {
    title: productMeta[known].name,
    description: productMeta[known].blurb,
    alternates: { canonical: `/docs/${known}` },
  };
}

export default async function ProductDocsPage({ params }: { params: Promise<{ product: string }> }) {
  const { product } = await params;
  const known = asProduct(product);
  if (!known) {
    notFound();
  }
  const groups = categoriesFor(known);
  const items = searchItems().filter((item) => item.href.startsWith(`/docs/${known}/`));
  return (
    <div className="docs-layout">
      <aside>
        <DocSidebar current="" product={known} />
      </aside>
      <main className="docs-content">
        <p className="docs-kicker">{productMeta[known].name}</p>
        <h1>{productMeta[known].blurb}</h1>
        <DocSearch items={items} label={`Search ${productMeta[known].name}`} />
        {groups.map((group) => (
          <section key={group.category}>
            <h2>{group.category}</h2>
            <ul>
              {group.articles.map((article) => (
                <li key={article.slug}>
                  <Link href={`/docs/${known}/${article.slug}`}>{article.title}</Link>
                  <p>{article.description}</p>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </main>
    </div>
  );
}
