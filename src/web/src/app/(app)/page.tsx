import { Suspense } from "react";
import type { Metadata } from "next";
import { CartPanel } from "@/components/cart-panel";
import { ProductGrid, ProductGridSkeleton } from "@/components/product-grid";
import { requireScreen } from "@/lib/me";
import { getCart, searchProducts } from "@/lib/sales-api";
import { addToCart, changeQty, removeFromCart } from "./cart-actions";

export const metadata: Metadata = { title: "หน้าขาย · ตลาดมาร์เก็ต" };

type Search = Promise<{ search?: string; page?: string }>;

// UI-talad-002 หน้าขาย — this unit draws the product browser and the cart (UC-talad-001). Member, totals
// after discounts, price shortcut and checkout are the zones other units add to this same page.
export default async function SalesPage({ searchParams }: { searchParams: Search }) {
  await requireScreen("UI-talad-002");
  const { search = "", page = "1" } = await searchParams;
  const pageNo = Math.max(1, Number.parseInt(page, 10) || 1);

  // async-parallel — products and cart do not wait for each other
  const productsPromise = searchProducts(search, pageNo);
  productsPromise.catch(() => undefined); // awaited inside <Suspense>; this only stops an early rejection being reported as unhandled
  const cart = await getCart();

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
          <CartPanel cart={cart} changeQty={changeQty} remove={removeFromCart} />
        </aside>
      </div>
    </div>
  );
}

async function Products({ promise, search }: { promise: ReturnType<typeof searchProducts>; search: string }) {
  return <ProductGrid page={await promise} search={search} addToCart={addToCart} />;
}
