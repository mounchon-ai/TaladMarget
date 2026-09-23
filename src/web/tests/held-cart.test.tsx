import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// One cookie jar for the whole browser (the shop's shared machine), as next/headers would see it.
const jar = vi.hoisted(() => new Map<string, string>());
vi.mock("next/headers", () => ({
  cookies: async () => ({
    get: (name: string) => (jar.has(name) ? { value: jar.get(name)! } : undefined),
    set: (name: string, value: string) => void jar.set(name, value),
    delete: (name: string) => void jar.delete(name),
  }),
}));
vi.mock("next/navigation", () => ({
  redirect: (to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  },
}));
vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));

const { signIn } = await import("@/app/login/actions");
const logout = await import("@/app/logout/route");
const { getCart } = await import("@/lib/sales-api");
const { CartPanel } = await import("@/components/cart-panel");

/**
 * A stand-in for the api: carts live on the server, one per person, found by whose token asks
 * (API-004 · BR-talad-020@v1). Signing out does not touch them.
 */
function fakeApi() {
  const users: Record<string, string> = { somchai: "Somchai#2569", manee: "Manee#2569", owner: "Owner#2569" };
  const oranges = { productId: 1, name: "ส้มสายน้ำผึ้ง", price: 45, qty: 2, lineTotal: 90, stockQty: 10, lowStock: false };
  const carts: Record<string, { id: number; status: string; lines: (typeof oranges)[]; subtotal: number }> = {
    somchai: { id: 11, status: "Open", lines: [oranges], subtotal: 90 },
  };
  const requests: { path: string; bearer: string | null; cache: RequestCache | undefined }[] = [];

  const fetchImpl = vi.fn(async (url: string, init: RequestInit = {}) => {
    const path = new URL(url).pathname;
    const bearer = new Headers(init.headers).get("authorization");
    requests.push({ path, bearer, cache: init.cache });
    if (path === "/api/auth/login") {
      const { username, password } = JSON.parse(String(init.body));
      if (users[username] !== password) return new Response(JSON.stringify({ code: "WRONG_PASSWORD", message: "รหัสผ่านไม่ถูกต้อง" }), { status: 401 });
      return new Response(JSON.stringify({ accessToken: `jwt-${username}`, expiresAt: "2026-09-24T08:00:00Z" }), { status: 200 });
    }
    if (path === "/api/auth/logout") return new Response(null, { status: 204 });
    if (path === "/api/cart") {
      const who = bearer?.replace("Bearer jwt-", "");
      if (!who) return new Response(null, { status: 401 });
      carts[who] ??= { id: 20 + Object.keys(carts).length, status: "Open", lines: [], subtotal: 0 };
      return new Response(JSON.stringify(carts[who]), { status: 200 });
    }
    return new Response(null, { status: 404 });
  });
  return { fetchImpl, requests };
}

async function signInAs(username: string, password: string) {
  const form = new FormData();
  form.set("username", username);
  form.set("password", password);
  form.set("next", "/");
  await expect(signIn(undefined, form)).rejects.toThrow("NEXT_REDIRECT /");
}

async function signOut() {
  const response = await logout.POST(new Request("http://localhost/logout", { method: "POST" }) as never);
  expect(response.headers.get("location")).toBe("http://localhost/login");
}

const ok = async () => ({ ok: true as const });

beforeEach(() => jar.clear());
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-008 · UC-talad-002 · a held cart on the shop's shared machine", () => {
  it("AC-talad-020 · manee signs in after somchai signed out and sees her own empty cart", async () => {
    const api = fakeApi();
    vi.stubGlobal("fetch", api.fetchImpl);
    await signInAs("somchai", "Somchai#2569");
    await signOut();

    await signInAs("manee", "Manee#2569");
    const cart = await getCart();
    render(<CartPanel cart={cart} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(api.requests.at(-1)).toEqual({ path: "/api/cart", bearer: "Bearer jwt-manee", cache: "no-store" });
    expect(screen.getByText("ยังไม่มีสินค้าในตะกร้า")).toBeTruthy();
    expect(screen.getByTestId("ui-talad-002-subtotal-after-item-promo").textContent).toBe("฿0.00");
    expect(screen.queryByText("ส้มสายน้ำผึ้ง")).toBeNull();
  });

  it("AC-talad-021 · somchai signs back in to his whole cart and can carry on", async () => {
    const api = fakeApi();
    vi.stubGlobal("fetch", api.fetchImpl);
    await signInAs("somchai", "Somchai#2569");
    await signOut();
    await signInAs("manee", "Manee#2569");
    await getCart();
    await signOut();

    await signInAs("somchai", "Somchai#2569");
    const cart = await getCart();
    const changeQty = vi.fn(ok);
    render(<CartPanel cart={cart} changeQty={changeQty} remove={vi.fn(ok)} />);

    expect(api.requests.at(-1)?.bearer).toBe("Bearer jwt-somchai");
    expect(screen.getByTestId("ui-talad-002-ent-010-product").textContent).toBe("ส้มสายน้ำผึ้ง");
    expect(screen.getByTestId("ui-talad-002-ent-010-qty")).toHaveProperty("value", "2");
    expect(screen.getByTestId("ui-talad-002-subtotal-after-item-promo").textContent).toBe("฿90.00");
    await userEvent.click(screen.getByTestId("ui-talad-002-increase-qty"));
    expect(changeQty).toHaveBeenCalledWith(1, 3);
  });

  it("AC-talad-022 · the owner signing in on the same machine gets their own empty cart", async () => {
    const api = fakeApi();
    vi.stubGlobal("fetch", api.fetchImpl);
    await signInAs("somchai", "Somchai#2569");
    await signOut();

    await signInAs("owner", "Owner#2569");
    const cart = await getCart();
    render(<CartPanel cart={cart} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(api.requests.at(-1)?.bearer).toBe("Bearer jwt-owner");
    expect(cart.lines).toEqual([]);
    expect(screen.getByText("ยังไม่มีสินค้าในตะกร้า")).toBeTruthy();
  });

  it("signing out leaves no token behind — the next cart request carries nobody's", async () => {
    const api = fakeApi();
    vi.stubGlobal("fetch", api.fetchImpl);
    await signInAs("somchai", "Somchai#2569");
    await signOut();

    await expect(getCart()).rejects.toThrow("api answered 401");

    expect(jar.size).toBe(0);
    expect(api.requests.at(-1)).toEqual({ path: "/api/cart", bearer: null, cache: "no-store" });
  });
});
