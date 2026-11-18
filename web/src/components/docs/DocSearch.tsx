"use client";

import Link from "next/link";
import { useMemo, useState } from "react";

export type DocSearchItem = {
  href: string;
  title: string;
  summary: string;
  product: string;
  category: string;
  text: string;
};

export function DocSearch({ items, label }: { items: DocSearchItem[]; label: string }) {
  const [term, setTerm] = useState("");
  const matches = useMemo(() => {
    const query = term.trim().toLowerCase();
    if (query.length < 2) {
      return [];
    }
    return items
      .filter((item) => `${item.title} ${item.summary} ${item.category} ${item.product} ${item.text}`.toLowerCase().includes(query))
      .slice(0, 8);
  }, [items, term]);

  return (
    <div className="docs-search">
      <label className="docs-muted" htmlFor="doc-search">
        {label}
      </label>
      <input
        id="doc-search"
        onChange={(event) => setTerm(event.target.value)}
        placeholder="Search guides"
        value={term}
      />
      {term.trim().length >= 2 ? (
        <div className="docs-results" role="listbox">
          {matches.length === 0 ? <p className="docs-muted">No matching guides.</p> : null}
          {matches.map((item) => (
            <Link href={item.href} key={item.href} onClick={() => setTerm("")}>
              <strong>{item.title}</strong>
              <span className="docs-muted">
                {" "}
                · {item.product} · {item.category}
              </span>
            </Link>
          ))}
        </div>
      ) : null}
    </div>
  );
}
