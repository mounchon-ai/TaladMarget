import { apiFetch } from "@/lib/api-client";

// API-043 through the web app: an <img> cannot carry the JWT, so the web server fetches the picture with
// the session's token and hands the bytes on (DEC-002 · the file lives on the api server's disk).
export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^\d+$/.test(id)) return new Response(null, { status: 404 });
  const upstream = await apiFetch(`/api/products/${id}/image`);
  if (!upstream.ok) return new Response(null, { status: upstream.status === 401 ? 401 : 404 });
  return new Response(upstream.body, {
    headers: {
      "content-type": upstream.headers.get("content-type") ?? "application/octet-stream",
      "cache-control": "private, max-age=300",
    },
  });
}
