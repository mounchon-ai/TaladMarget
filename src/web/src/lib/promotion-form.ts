// UI-talad-015 · UI-talad-016 โปรโมชั่น — pure, so the server action, the form and a test read it the same way.
// No rule is checked here: BR-talad-009 · 011..015 are enforced in the domain only (interfaces.json
// ruleEnforcement), and the api answers each fault under its field.

/** ENT-006 attribute names — the form's input names and the api's `errors[].field`, one for one. */
export const PROMOTION_FIELDS = [
  "name",
  "type",
  "productA",
  "qtyA",
  "productB",
  "qtyB",
  "freeProduct",
  "freeQty",
  "ratePercent",
  "minSubtotal",
  "startDate",
  "endDate",
] as const;
export type PromotionField = (typeof PROMOTION_FIELDS)[number];
export type PromotionFieldErrors = Partial<Record<PromotionField, string>>;

/** ENT-006.type — the six codes, labelled as UC-talad-021's main flow names them, in the same order. */
export const PROMOTION_TYPES: ReadonlyArray<{ code: string; label: string }> = [
  { code: "ITEM_PERCENT", label: "ลด % ต่อสินค้า" },
  { code: "BILL_PERCENT", label: "ลด % ทั้งบิล" },
  { code: "BUY_X_GET_Y", label: "ซื้อ x แถม y" },
  { code: "BUY_AB_GET_Y", label: "ซื้อ a + b แถม y" },
  { code: "BUY_AB_PERCENT", label: "ซื้อ a + b ลด y%" },
  { code: "BUY_X_PERCENT", label: "ซื้อ x ชิ้นลด y%" },
];
export const typeLabel = (code: string) => PROMOTION_TYPES.find((t) => t.code === code)?.label ?? code;

/** STM-talad-002 labels. The api sends the C# enum name ("Active"), so the match ignores case. */
const STATUS_LABELS: Record<string, string> = { active: "ใช้อยู่", discontinued: "เลิกใช้" };
export const statusLabel = (status: string) => STATUS_LABELS[status.toLowerCase()] ?? status;

type Condition = "productA" | "qtyA" | "productB" | "qtyB" | "freeProduct" | "freeQty" | "ratePercent" | "minSubtotal";

/**
 * Which condition fields each form asks for — what UI-talad-016's field validations say ("บังคับทุกรูปแบบ
 * ยกเว้นลด % ทั้งบิล" …), the same table the domain's PromotionTypes.Uses* holds. Only these are drawn;
 * the domain clears anything else, so a hidden field can never carry a stray value into a version.
 */
const CONDITIONS: Record<string, readonly Condition[]> = {
  ITEM_PERCENT: ["productA", "ratePercent"],
  BILL_PERCENT: ["ratePercent", "minSubtotal"],
  BUY_X_GET_Y: ["productA", "qtyA", "freeProduct", "freeQty"],
  BUY_AB_GET_Y: ["productA", "qtyA", "productB", "qtyB", "freeProduct", "freeQty"],
  BUY_AB_PERCENT: ["productA", "qtyA", "productB", "qtyB", "ratePercent"],
  BUY_X_PERCENT: ["productA", "qtyA", "ratePercent"],
};
export const conditionsFor = (type: string): readonly Condition[] => CONDITIONS[type] ?? [];

/** What the form holds — every value as text; a product carries its name too, so a refused save redraws it. */
export type PromotionValues = Record<PromotionField, string> & {
  productAName: string;
  productBName: string;
  freeProductName: string;
};

export const EMPTY_VALUES: PromotionValues = {
  name: "",
  type: "",
  productA: "",
  productAName: "",
  qtyA: "",
  productB: "",
  productBName: "",
  qtyB: "",
  freeProduct: "",
  freeProductName: "",
  freeQty: "",
  ratePercent: "",
  minSubtotal: "",
  startDate: "",
  endDate: "",
};

type Stored = {
  name: string;
  type: string;
  productA: { id: number; name: string } | null;
  qtyA: number | null;
  productB: { id: number; name: string } | null;
  qtyB: number | null;
  freeProduct: { id: number; name: string } | null;
  freeQty: number | null;
  ratePercent: number | null;
  minSubtotal: number | null;
  startDate: string;
  endDate: string | null;
};

const text = (n: number | null) => (n === null ? "" : String(n));

/** The conditions in force, as the edit form opens with them. */
export function valuesOf(p: Stored): PromotionValues {
  return {
    name: p.name,
    type: p.type,
    productA: p.productA ? String(p.productA.id) : "",
    productAName: p.productA?.name ?? "",
    qtyA: text(p.qtyA),
    productB: p.productB ? String(p.productB.id) : "",
    productBName: p.productB?.name ?? "",
    qtyB: text(p.qtyB),
    freeProduct: p.freeProduct ? String(p.freeProduct.id) : "",
    freeProductName: p.freeProduct?.name ?? "",
    freeQty: text(p.freeQty),
    ratePercent: text(p.ratePercent),
    minSubtotal: text(p.minSubtotal),
    startDate: p.startDate,
    endDate: p.endDate ?? "",
  };
}

export function readValues(formData: FormData): PromotionValues {
  const values = { ...EMPTY_VALUES };
  for (const key of Object.keys(values) as (keyof PromotionValues)[]) values[key] = String(formData.get(key) ?? "").trim();
  return values;
}

/** API-027 · API-028 body. */
export type PromotionTermsBody = {
  name: string;
  type: string;
  productA: number | null;
  qtyA: number | null;
  productB: number | null;
  qtyB: number | null;
  freeProduct: number | null;
  freeQty: number | null;
  ratePercent: number | null;
  minSubtotal: number | null;
  startDate: string;
  endDate: string | null;
};

// empty or not a number goes as null: the api reads numbers strictly, and a string it cannot read would be
// the framework's refusal with no field to put it under — null is answered under its own field instead
const num = (v: string) => {
  if (v === "") return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
};

/** What the form holds, as the api reads it — values sent as typed, the domain decides whether they are right. */
export function termsOf(v: PromotionValues): PromotionTermsBody {
  return {
    name: v.name,
    type: v.type,
    productA: num(v.productA),
    qtyA: num(v.qtyA),
    productB: num(v.productB),
    qtyB: num(v.qtyB),
    freeProduct: num(v.freeProduct),
    freeQty: num(v.freeQty),
    ratePercent: num(v.ratePercent),
    minSubtotal: num(v.minSubtotal),
    startDate: v.startDate,
    endDate: v.endDate === "" ? null : v.endDate,
  };
}

/** What the form shows after a save that did not go through — the typed values come back so nothing is lost. */
export type PromotionFormState = { values: PromotionValues; errors: PromotionFieldErrors; message?: string } | undefined;

/** Save and cancel both go to UI-talad-015 หน้าโปรโมชั่น. */
export const PROMOTIONS_PATH = "/promotions";
/** A save that went through — the list says so. */
export const SAVED_PARAM = "saved";
export const SAVED = "บันทึกสำเร็จ";
/** UI-talad-015 state "empty" — word for word; also what an edit of a promotion that is gone comes back to. */
export const NO_PROMOTION = "ไม่พบโปรโมชั่น";
// not "missing": the (app) frame answers that one on every page with the member sentence (FE-talad-014)
export const MISSING_PARAM = "gone";
