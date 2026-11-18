import type { DocSection } from "../../content/docs/types";

function headingId(heading: string): string {
  return heading
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function ArticleBody({ sections }: { sections: DocSection[] }) {
  return (
    <>
      {sections.map((section) => (
        <section key={section.heading}>
          <h2 id={headingId(section.heading)}>{section.heading}</h2>
          {section.paragraphs?.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
          {section.steps ? (
            <ol>
              {section.steps.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
          ) : null}
          {section.bullets ? (
            <ul>
              {section.bullets.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          ) : null}
          {section.fields ? (
            <table className="docs-fields">
              <thead>
                <tr>
                  <th>Field</th>
                  <th>What it is for</th>
                </tr>
              </thead>
              <tbody>
                {section.fields.map((field) => (
                  <tr key={field.name}>
                    <td>{field.name}</td>
                    <td>{field.detail}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
          {section.note ? <p className="docs-note">{section.note}</p> : null}
          {section.warning ? <p className="docs-warning">{section.warning}</p> : null}
        </section>
      ))}
    </>
  );
}

export function headingIds(sections: DocSection[]): { id: string; heading: string }[] {
  return sections.map((section) => ({ id: headingId(section.heading), heading: section.heading }));
}
