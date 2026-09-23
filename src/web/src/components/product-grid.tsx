import Link from "next/link";
import { formatBaht } from "@/lib/money";
import type { ProductPage } from "@/lib/sales-api";
import { AddToCartButton } from "./add-to-cart-button";

type Props = {
  page: ProductPage;
  search: string;
  addToCart: (productId: number) => Promise<{ ok: true } | { ok: false; message: string }>;
};

/**
 * UI-talad-002 zone product-browser — cards from API-003, paged by the server (state "overflow").
 * Every data-testid is the wireframe's (MCK-talad-002).
 */
export function ProductGrid({ page, search, addToCart }: Props) {
  if (page.items.length === 0) return <p className="muted">ไม่พบสินค้า</p>;

  const pages = Math.max(1, Math.ceil(page.total / page.pageSize));
  const href = (n: number) => `/?${new URLSearchParams({ ...(search ? { search } : {}), page: String(n) })}`;

  return (
    <>
      <div className="products">
        {page.items.map((p) => (
          <article key={p.id} className="pcard" data-row-key={p.id}>
            <div className="pimg" data-testid="ui-talad-002-ent-001-image">
              {p.hasImage ? (
                // eslint-disable-next-line @next/next/no-img-element -- served by our own route with the session's token
                <img src={`/product-image/${p.id}`} alt={p.name} loading="lazy" />
              ) : (
                "รูปสินค้า"
              )}
            </div>
            <div className="pbody">
              <div className="pname" data-testid="ui-talad-002-ent-001-name" title={p.name}>
                {p.name}
              </div>
              <div className="pprice" data-testid="ui-talad-002-ent-002-price">
                {formatBaht(p.price)}
              </div>
              {p.lowStock ? (
                <div>
                  <span className="tag low" data-testid="ui-talad-002-ent-001-low-stock-threshold">
                    ใกล้หมด
                  </span>
                </div>
              ) : null}
            </div>
            <div className="pact">
              <AddToCartButton productId={p.id} action={addToCart} />
            </div>
          </article>
        ))}
      </div>
      {pages > 1 ? (
        <nav className="pager" aria-label="หน้าของสินค้า">
          {page.page > 1 ? <Link href={href(page.page - 1)}>‹ ก่อนหน้า</Link> : null}
          <span className="muted">
            หน้า {page.page} / {pages}
          </span>
          {page.page < pages ? <Link href={href(page.page + 1)}>ถัดไป ›</Link> : null}
        </nav>
      ) : null}
    </>
  );
}

/** State "loading" — card outlines while the products load; the cart stays usable. */
export function ProductGridSkeleton() {
  return (
    <div className="products" aria-busy="true">
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="pcard skeleton" />
      ))}
    </div>
  );
}
