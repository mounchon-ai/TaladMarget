import { Suspense } from "react";
import type { Metadata } from "next";
import { CartPanel } from "@/components/cart-panel";
import { MemberPanel } from "@/components/member-panel";
import { ProductGrid, ProductGridSkeleton } from "@/components/product-grid";
import { requireScreen } from "@/lib/me";
import { getCart, getCartPricing, searchProducts } from "@/lib/sales-api";
import { addToCart, bindToCart, changeQty, findMembers, removeFromCart } from "./cart-actions";

export const metadata: Metadata = { title: "หน้าขาย · ตลาดมาร์เก็ต" };

type Search = Promise<{ search?: string; page?: string }>;

// UI-talad-002 หน้าขาย — the product browser and the cart (UC-talad-001) and the member zone
// (UC-talad-009), and the totals after discounts (UC-talad-004 · FE-talad-032). The price shortcut and checkout are
// the zones other units add.
export default async function SalesPage({ searchParams }: { searchParams: Search }) {
  await requireScreen("UI-talad-002");
  const { search = "", page = "1" } = await searchParams;
  const pageNo = Math.max(1, Number.parseInt(page, 10) || 1);

  // async-parallel — products and cart do not wait for each other
  const productsPromise = searchProducts(search, pageNo);
  productsPromise.catch(() => undefined); // awaited inside <Suspense>; this only stops an early rejection being reported as unhandled
  // the cart and its pricing do not wait for each other either; pricing that cannot be read shows — (state "error")
  const [cart, pricing] = await Promise.all([getCart(), getCartPricing().catch(() => null)]);

  return (
    <div data-screen="UI-talad-002">
      <div className="pagehead">
        <h1>หน้าขาย</h1>
      </div>
      <div className="cols">
        <section className="wide">
          <div className="card">
            <h2>สินค้า</h2>
            <form className="inline" role="search" action="/">
              <input
                type="text"
                name="search"
                defaultValue={search}
                className="input"
                placeholder="ชื่อสินค้าบางส่วน หรือเลขบาร์โค้ดเต็ม"
                data-testid="ui-talad-002-product-search"
              />
              <button type="submit" className="btn" data-testid="ui-talad-002-search-product">
                ค้นหาสินค้า
              </button>
            </form>
            <Suspense key={`${search}|${pageNo}`} fallback={<ProductGridSkeleton />}>
              <Products promise={productsPromise} search={search} />
            </Suspense>
          </div>
        </section>
        <aside className="narrow">
          <CartPanel cart={cart} pricing={pricing} changeQty={changeQty} remove={removeFromCart} />
          <MemberPanel member={cart.member ?? null} find={findMembers} bind={bindToCart} />
        </aside>
      </div>
    </div>
  );
}

async function Products({ promise, search }: { promise: ReturnType<typeof searchProducts>; search: string }) {
  return <ProductGrid page={await promise} search={search} addToCart={addToCart} />;
}
