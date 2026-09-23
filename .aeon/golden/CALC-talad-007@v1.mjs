// Golden dataset generator for CALC-talad-007@v1 (constrains BR-talad-014@v1)
// โปรโมชั่น ซื้อ x ชิ้น ลด y% — ผูกสินค้าตัวเดียว · นับเป็นชุดละ x ชิ้น ลดซ้ำตามชุด ·
// ชิ้นที่ไม่ครบชุดคิดราคาปกติ · คิดและปัดครั้งเดียวต่อบรรทัด
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → implemented directly, not via a language default
//   rounding_points disc rounded ONCE per line, at 0.01 baht, the moment it is computed
//                   (not per unit, not per set) — same as CALC-talad-003 / CALC-talad-006
//   formula         sets = floor(qty / x)
//                   disc = round(sets × x × unit_price × y / 100)
//   price           unit price AT CHECKOUT (BR-talad-038)
//
// Every row also reports what rounding PER SET and rounding PER UNIT would have produced
// (informational only), so a reader can see which rows distinguish the three readings of ⑤.
//
// Run: node <state-dir>/golden/CALC-talad-007@v1.mjs   → prints rows as JSON

import { pathToFileURL } from 'node:url';

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  if (f.length > 2) throw new Error(`money(2) input has more than 2 decimals: ${baht}`);
  return BigInt(i.replace(/,/g, '')) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => `${s / 100n}.${String(s % 100n).padStart(2, '0')}`;
const checkInt = (name, v, min) => { if (!Number.isInteger(v) || v < min) throw new Error(`${name} must be int ≥ ${min}: ${v}`); };
const checkRate = (r) => { if (!Number.isInteger(r) || r < 0 || r > 100) throw new Error(`y must be int 0–100: ${r}`); };
const pctHalfUp = (amount, rate) => (amount * BigInt(rate) + 50n) / 100n;   // amount ≥ 0, result in satang

export function xPercent({ x, y, qty, unit_price }) {
  checkInt('x', x, 1); checkRate(y); checkInt('qty', qty, 0);
  const sets = Math.floor(qty / x);
  const p = toSatang(unit_price);
  const discounted_qty = sets * x;
  const disc = pctHalfUp(p * BigInt(discounted_qty), y);    // the one rounding point
  const gross = p * BigInt(qty);
  return {
    sets,
    discounted_qty,
    line_gross: toBaht(gross),
    item_promo_discount: toBaht(disc),
    line_net: toBaht(gross - disc),
    info_if_rounded_per_set: toBaht(pctHalfUp(p * BigInt(x), y) * BigInt(sets)),
    info_if_rounded_per_unit: toBaht(pctHalfUp(p, y) * BigInt(discounted_qty)),
  };
}

const ORANGE = '45.00';   // ส้มสายน้ำผึ้ง (EX-talad-001)
const LONGAN = '19.75';   // ลำไย — ราคามีสตางค์ (GD-talad-003)

// ⑤: pick, by search rather than by hand, the smallest y at which rounding once per line differs from
// BOTH rounding per set and rounding per unit — the row that tells the three readings apart.
const splitCase = { x: 3, qty: 6, unit_price: LONGAN };
const ySplit = Array.from({ length: 100 }, (_, i) => i + 1).find((y) => {
  const r = xPercent({ ...splitCase, y });
  return r.item_promo_discount !== r.info_if_rounded_per_set && r.item_promo_discount !== r.info_if_rounded_per_unit;
});
if (ySplit === undefined) throw new Error('no y in 1–100 separates per-line from per-set and per-unit rounding for the chosen price');

const cases = [
  { note: 'ordinary: ตัวอย่างของเจ้าของงาน — ซื้อส้ม 3 ลด 10% ตะกร้ามีส้ม 7 → ลด 6 ชิ้น (SRC-039 L1)', input: { x: 3, y: 10, qty: 7, unit_price: ORANGE } },
  { note: 'ordinary: ลดซ้ำตามชุด ครบ 2 ชุดพอดี — ส้ม 6', input: { x: 3, y: 10, qty: 6, unit_price: ORANGE } },
  { note: 'boundary ①: qty < x → ไม่ลด (ส้ม 2 < 3)', input: { x: 3, y: 10, qty: 2, unit_price: ORANGE } },
  { note: 'boundary ②: qty = x พอดี → ลด 1 ชุด (ส้ม 3)', input: { x: 3, y: 10, qty: 3, unit_price: ORANGE } },
  { note: 'boundary ③: ชิ้นเกินชุดคิดราคาปกติ — ส้ม 5 → ลด 3 ชิ้น อีก 2 ชิ้นราคาปกติ', input: { x: 3, y: 10, qty: 5, unit_price: ORANGE } },
  { note: 'boundary ④: ส้มตอนหยิบ 45.00 แก้เป็น 50.00 ก่อนกดชำระ → ใช้ 50.00 (แถวนี้ทดสอบการเลือกราคา — price_at_add ไม่ถูกใช้)', input: { x: 3, y: 10, qty: 3, price_at_add: ORANGE, unit_price: '50.00' } },
  { note: 'boundary ⑤: y = 0 → ไม่ลด', input: { x: 3, y: 0, qty: 3, unit_price: ORANGE } },
  { note: 'boundary ⑤: y = 100 → ชิ้นในชุดฟรี ชิ้นเกินจ่ายเต็ม ยอดไม่ติดลบ (ส้ม 7)', input: { x: 3, y: 100, qty: 7, unit_price: ORANGE } },
  { note: 'boundary ⑥: x = 1 → ลดทุกชิ้น (ส้ม 5)', input: { x: 1, y: 10, qty: 5, unit_price: ORANGE } },
  { note: 'rounding: ส่วนลดมีเศษ — ลำไย 7 ชิ้น ซื้อ 3 ลด 7% (ลด 6 ชิ้น ปัดครั้งเดียวต่อบรรทัด)', input: { x: 3, y: 7, qty: 7, unit_price: LONGAN } },
  { note: `rounding: y ที่เล็กที่สุดซึ่งปัดต่อบรรทัดต่างจากทั้งปัดต่อชุดและปัดต่อชิ้น (ค้นด้วยสคริปต์) — ลำไย 6 ชิ้น ซื้อ 3 ลด ${ySplit}%`, input: { ...splitCase, y: ySplit } },
];

export const rows = cases.map((c) => {
  const { x, y, qty, unit_price } = c.input;
  const { info_if_rounded_per_set, info_if_rounded_per_unit, ...rest } = xPercent({ x, y, qty, unit_price });
  return {
    input: c.input,
    expected: { unit_price, qty, ...rest },
    note: `${c.note} · [ข้อมูลประกอบ] ถ้าปัดต่อชุด = ${info_if_rounded_per_set} · ถ้าปัดต่อชิ้น = ${info_if_rounded_per_unit}`,
  };
});
if (import.meta.url === pathToFileURL(process.argv[1]).href) console.log(JSON.stringify(rows, null, 2));
