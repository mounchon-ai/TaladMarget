import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AdjustStockDialog } from "@/components/adjust-stock-dialog";
import { DiscontinueProductZone } from "@/components/discontinue-product-zone";
import { EditPriceDialog } from "@/components/edit-price-dialog";
import { PriceHistory } from "@/components/price-history";
import { StockAdjustments } from "@/components/stock-adjustments";
import { requireScreen } from "@/lib/me";
import { formatBaht } from "@/lib/money";
import { getProduct } from "@/lib/products-api";
import { adjustStockAction, discontinueAction, repriceAction } from "./actions";

export const metadata: Metadata = { title: "รายละเอียดสินค้า · ตลาดมาร์เก็ต" };

type Params = Promise<{ id: string }>;
type Search = Promise<{ historyPage?: string; adjustmentsPage?: string }>;

// UI-talad-012 รายละเอียดสินค้า (UC-talad-017 · FE-talad-020) — the owner's (ACL-019). Summary, price history and
// stock adjustments, each section paged on its own; แก้ราคา opens UI-talad-013 over it and ปรับสต็อก opens UI-talad-014
// (FE-talad-024). ลบสินค้า (FE-talad-022) is the danger zone, last. Not drawn yet: แก้ไขข้อมูล (UI-talad-011 has no unit
// — GAP-talad-002). Opened as its own page from the stock list, as the members' detail is; UIC-003's modal over the
// list is still open.
export default async function ProductPage({ params, searchParams }: { params: Params; searchParams: Search }) {
  await requireScreen("UI-talad-012");
  const id = Number.parseInt((await params).id, 10);
  const search = await searchParams;
  const historyPage = Math.max(1, Number.parseInt(search.historyPage ?? "1", 10) || 1);
  const adjustmentsPage = Math.max(1, Number.parseInt(search.adjustmentsPage ?? "1", 10) || 1);
  const product = Number.isSafeInteger(id) && id > 0 ? await getProduct(id, historyPage, adjustmentsPage) : null;
  if (!product) redirect("/stock?gone=1");

  return (
    <div data-screen="UI-talad-012">
      <div className="pagehead">
        <div className="grow">
          <div className="crumbs">
            <Link href="/stock">สต็อก</Link> ›
          </div>
          <h1>รายละเอียดสินค้า</h1>
        </div>
      </div>

      <div className="card formcard">
        <div className="field">
          <span className="lbl">ชื่อสินค้า</span>
          <div className="value" data-testid="ui-talad-012-ent-001-name">
            {product.name}
          </div>
        </div>
        <div className="field">
          <span className="lbl">คงเหลือ</span>
          <div className="value" data-testid="ui-talad-012-ent-001-stock-qty">
            {product.stockQty.toLocaleString("th-TH")}
          </div>
        </div>
        <div className="field">
          <span className="lbl">จุดเตือน</span>
          <div className="value" data-testid="ui-talad-012-ent-001-low-stock-threshold">
            {product.lowStockThreshold.toLocaleString("th-TH")}
          </div>
        </div>
        <div className="field">
          <span className="lbl">ราคาปัจจุบัน</span>
          <div className="value" data-testid="ui-talad-012-ent-002-price">
            {formatBaht(product.price)}
          </div>
        </div>
        <div className="formactions">
          <EditPriceDialog current={product.price} action={repriceAction.bind(null, product.id)} />
          <AdjustStockDialog current={product.stockQty} action={adjustStockAction.bind(null, product.id)} />
          <Link href="/stock" className="btn btn-ghost" data-testid="ui-talad-012-back">
            กลับ
          </Link>
        </div>
      </div>

      <section className="card">
        <h2>ประวัติราคา</h2>
        <PriceHistory history={product.priceHistory} productId={product.id} adjustmentsPage={adjustmentsPage} />
      </section>

      <section className="card">
        <h2>รายการปรับสต็อก</h2>
        <StockAdjustments adjustments={product.adjustments} productId={product.id} historyPage={historyPage} />
      </section>

      <DiscontinueProductZone name={product.name} discontinue={discontinueAction.bind(null, product.id)} />
    </div>
  );
}
