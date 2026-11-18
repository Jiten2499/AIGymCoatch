import Link from "next/link";

export default function DocsNotFound() {
  return (
    <main className="docs-wrap">
      <p className="docs-kicker">Missing guide</p>
      <h1>That article is not in the help center.</h1>
      <p className="docs-muted">The address may be outdated. Choose a product and browse the guides that match the current product.</p>
      <p>
        <Link href="/docs/growventur">Growventur guides</Link>
        {" · "}
        <Link href="/docs/growfit">GrowFit guides</Link>
      </p>
    </main>
  );
}
