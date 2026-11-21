import type { MetadataRoute } from "next";
import { articleHref, articles } from "../content/docs/catalog";

const origin = process.env.NEXT_PUBLIC_SITE_URL ?? "https://growventur.com";

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date("2026-11-18");
  const pages = ["/", "/docs", "/docs/growventur", "/docs/growfit", ...articles.map((article) => articleHref(article))];
  return [
    ...pages.map((path) => ({ url: new URL(path, origin).toString(), lastModified: now })),
    { url: new URL("/growfit/terms", origin).toString(), lastModified: new Date("2026-11-21") },
  ];
}
