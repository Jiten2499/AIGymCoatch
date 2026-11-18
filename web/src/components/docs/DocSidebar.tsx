import Link from "next/link";
import { articleHref, categoriesFor } from "../../content/docs/catalog";
import type { DocProduct } from "../../content/docs/types";
import { productMeta } from "../../content/docs/types";

export function DocSidebar({ product, current }: { product: DocProduct; current?: string }) {
  const groups = categoriesFor(product);
  return (
    <nav aria-label={`${productMeta[product].name} guides`} className="docs-side">
      <Link href={`/docs/${product}`}>{productMeta[product].name}</Link>
      {groups.map((group) => (
        <div key={group.category}>
          <h2>{group.category}</h2>
          {group.articles.map((article) => (
            <Link aria-current={article.slug === current ? "page" : undefined} href={articleHref(article)} key={article.slug}>
              {article.title}
            </Link>
          ))}
        </div>
      ))}
    </nav>
  );
}
