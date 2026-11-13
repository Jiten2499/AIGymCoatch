import { NextResponse } from "next/server";

const apiRoot = process.env.NEXT_PUBLIC_API_URL ?? "https://api.growventur.com/api/v1";

export async function GET(request: Request) {
  const incoming = new URL(request.url);
  const target = new URL(`${apiRoot.replace(/\/$/, "")}/integrations/google-drive/callback`);
  target.search = incoming.search;

  const response = await fetch(target, { redirect: "manual", cache: "no-store" });
  const location = response.headers.get("location");
  if (location && response.status >= 300 && response.status < 400) {
    return NextResponse.redirect(toBrowserUrl(location, incoming.origin), 302);
  }

  return new NextResponse("Google Drive could not be connected.", { status: response.status || 502 });
}

function toBrowserUrl(location: string, siteOrigin: string) {
  const parsed = new URL(location, siteOrigin);
  const local = parsed.hostname === "localhost" || parsed.hostname === "127.0.0.1";
  return local ? new URL(`${parsed.pathname}${parsed.search}`, siteOrigin).toString() : parsed.toString();
}
