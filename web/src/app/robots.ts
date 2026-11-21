import type { MetadataRoute } from "next";

const origin = process.env.NEXT_PUBLIC_SITE_URL ?? "https://growventur.com";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: ["/", "/docs", "/growfit"] },
    sitemap: new URL("/sitemap.xml", origin).toString(),
  };
}
