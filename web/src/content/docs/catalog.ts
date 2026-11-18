import { growfitAccountArticles } from "./growfit-account";
import { growfitArticles } from "./growfit";
import { growfitTrainerArticles } from "./growfit-trainers";
import { growventurGymArticles } from "./growventur-gym";
import { growventurLeadArticles } from "./growventur-leads";
import { growventurOpsArticles } from "./growventur-ops";
import { growventurSettingsArticles } from "./growventur-settings";
import { growventurStartArticles } from "./growventur-start";
import type { DocArticle, DocProduct } from "./types";

export const articles: DocArticle[] = [
  ...growventurStartArticles,
  ...growventurGymArticles,
  ...growventurLeadArticles,
  ...growventurOpsArticles,
  ...growventurSettingsArticles,
  ...growfitArticles,
  ...growfitAccountArticles,
  ...growfitTrainerArticles,
];

export function articlesFor(product: DocProduct): DocArticle[] {
  return articles.filter((article) => article.product === product);
}

export function findArticle(product: string, slug: string): DocArticle | undefined {
  return articles.find((article) => article.product === product && article.slug === slug);
}

export function categoriesFor(product: DocProduct): { category: string; articles: DocArticle[] }[] {
  const groups: { category: string; articles: DocArticle[] }[] = [];
  for (const article of articlesFor(product)) {
    const group = groups.find((item) => item.category === article.category);
    if (group) {
      group.articles.push(article);
    } else {
      groups.push({ category: article.category, articles: [article] });
    }
  }
  return groups;
}

export function neighbors(article: DocArticle): { previous?: DocArticle; next?: DocArticle } {
  const list = articlesFor(article.product);
  const index = list.findIndex((item) => item.slug === article.slug);
  return { previous: list[index - 1], next: list[index + 1] };
}

export function articleHref(article: Pick<DocArticle, "product" | "slug">): string {
  return `/docs/${article.product}/${article.slug}`;
}

export function searchItems() {
  return articles.map((article) => ({
    href: articleHref(article),
    title: article.title,
    summary: article.description,
    product: article.product === "growventur" ? "Growventur" : "GrowFit",
    category: article.category,
    text: searchText(article),
  }));
}

export function searchText(article: DocArticle): string {
  const parts = [article.title, article.description, article.category, article.roles.join(" ")];
  for (const section of article.sections) {
    parts.push(section.heading, ...(section.paragraphs ?? []), ...(section.steps ?? []), ...(section.bullets ?? []));
    for (const field of section.fields ?? []) {
      parts.push(field.name, field.detail);
    }
  }
  return parts.join(" ").toLowerCase();
}
