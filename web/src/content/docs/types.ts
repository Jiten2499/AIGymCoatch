export type DocProduct = "growventur" | "growfit";

export type DocField = { name: string; detail: string };

export type DocSection = {
  heading: string;
  paragraphs?: string[];
  steps?: string[];
  bullets?: string[];
  fields?: DocField[];
  note?: string;
  warning?: string;
};

export type DocArticle = {
  product: DocProduct;
  slug: string;
  title: string;
  description: string;
  category: string;
  roles: string[];
  updated: string;
  popular?: boolean;
  sections: DocSection[];
  related: string[];
};

export const productMeta: Record<DocProduct, { name: string; blurb: string }> = {
  growventur: {
    name: "Growventur",
    blurb: "Business management platform for gyms and studios.",
  },
  growfit: {
    name: "GrowFit",
    blurb: "Fitness app for members and trainers.",
  },
};
