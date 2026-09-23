// Golden dataset generator for CALC-talad-005@v1 (constrains BR-talad-012@v1)
// โปรโมชั่น ซื้อ a + b แถม y — จำนวน a, b ต่อชุดตั้งได้ · แถมซ้ำตามชุดครบคู่ (ฝั่งที่น้อยกว่า) ·
// ของแถมเป็นสินค้าอื่น หรือเป็น a / b เอง (ชิ้นที่ฟรีไม่นับเป็นชิ้นที่ซื้อ)
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → declared by the contract but NEVER exercised: there is no rounding point
//   rounding_points none     → free_discount = int × money(2) is exact in satang; no rounding helper is called
//   free = other    sets = floor(min(qty_a / n_a, qty_b / n_b)) ; entitled = sets × y
//                   free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free
//   free = a        sets = floor(min(qty_a / (n_a + y), qty_b / n_b)) ; free_qty = sets × y
//                   free_discount = free_qty × unit_price_a   (free = b: swap a and b)
//   price           unit price AT CHECKOUT (BR-talad-038) — never the price when the item was added
//
// Output is named for CALC-talad-001's input: free_discount lands as item_promo_discount on the line that
// carries the free item. Bill totals (sum of the promo lines) are printed so examples can quote them.
//
// Run: node <state-dir>/golden/CALC-talad-005@v1.mjs   → prints rows as JSON

import { pathToFileURL } from 'node:url';

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  if (f.length > 2) throw new Error(`money(2) input has more than 2 decimals: ${baht}`);
  return BigInt(i.replace(/,/g, '')) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => `${s / 100n}.${String(s % 100n).padStart(2, '0')}`;
const checkInt = (name, v, min) => { if (!Number.isInteger(v) || v < min) throw new Error(`${name} must be int ≥ ${min}: ${v}`); };

function line(name, unit_price, qty, discount) {
  const gross = toSatang(unit_price) * BigInt(qty);
  if (discount > gross) throw new Error(`discount exceeds line gross on ${name}`);
  return { item: name, unit_price, qty, line_gross: gross, item_promo_discount: discount, line_net: gross - discount };
}
function finish(core, lines) {
  const sum = (k) => lines.reduce((t, l) => t + l[k], 0n);
  return {
    ...core,
    lines: lines.map((l) => ({ ...l, line_gross: toBaht(l.line_gross), item_promo_discount: toBaht(l.item_promo_discount), line_net: toBaht(l.line_net) })),
    total_gross: toBaht(sum('line_gross')),
    promo_discount: toBaht(sum('item_promo_discount')),
    total_pay: toBaht(sum('line_net')),
  };
}

// ของแถมเป็นสินค้าอื่น (y ≠ a, b)
export function freeOther({ n_a, n_b, y, qty_a, unit_price_a, qty_b, unit_price_b, qty_free_in_cart, unit_price_free }) {
  for (const [k, v] of Object.entries({ n_a, n_b, y })) checkInt(k, v, 1);
  for (const [k, v] of Object.entries({ qty_a, qty_b, qty_free_in_cart })) checkInt(k, v, 0);
  const sets = Math.min(Math.floor(qty_a / n_a), Math.floor(qty_b / n_b));   // floor(min(p,q)) = min(floor p, floor q)
  const entitled = sets * y;
  const free_qty = Math.min(qty_free_in_cart, entitled);
  const free_discount = BigInt(free_qty) * toSatang(unit_price_free);         // exact — no rounding point
  return finish(
    { sets, entitled, free_qty, charged_free_item_qty: qty_free_in_cart - free_qty, free_discount: toBaht(free_discount) },
    [line('a', unit_price_a, qty_a, 0n), line('b', unit_price_b, qty_b, 0n), line('free', unit_price_free, qty_free_in_cart, free_discount)],
  );
}

// ของแถมเป็น a เอง — กรณีของแถมเป็น b ให้เรียกโดยสลับ a กับ b
export function freeIsA({ n_a, n_b, y, qty_a, unit_price_a, qty_b, unit_price_b }) {
  for (const [k, v] of Object.entries({ n_a, n_b, y })) checkInt(k, v, 1);
  for (const [k, v] of Object.entries({ qty_a, qty_b })) checkInt(k, v, 0);
  const sets = Math.min(Math.floor(qty_a / (n_a + y)), Math.floor(qty_b / n_b));
  const free_qty = sets * y;
  const free_discount = BigInt(free_qty) * toSatang(unit_price_a);            // exact — no rounding point
  return finish(
    { sets, free_qty, free_discount: toBaht(free_discount) },
    [line('a', unit_price_a, qty_a, free_discount), line('b', unit_price_b, qty_b, 0n)],
  );
}

const ORANGE = '45.00';      // ส้มสายน้ำผึ้ง (EX-talad-001)
const MANGOSTEEN = '120.00'; // มังคุด แพ็ก (EX-talad-003)
const LONGAN = '19.75';      // ลำไย — ราคามีสตางค์ (GD-talad-003)
const ab = { unit_price_a: ORANGE, unit_price_b: MANGOSTEEN };
const other = { ...ab, unit_price_free: LONGAN };

const otherCases = [
  { note: 'ordinary: ซื้อ ส้ม 1 + มังคุด 1 แถม ลำไย 1 ครบพอดี', input: { n_a: 1, n_b: 1, y: 1, qty_a: 1, qty_b: 1, qty_free_in_cart: 1, ...other } },
  { note: 'ordinary: ตั้งจำนวนต่อชุดได้ — ส้ม 2 + มังคุด 1 แถม ลำไย 1 ครบ 1 ชุด (SRC-036 L1)', input: { n_a: 2, n_b: 1, y: 1, qty_a: 2, qty_b: 1, qty_free_in_cart: 1, ...other } },
  { note: 'ordinary: แถมซ้ำตามชุด — ส้ม 4 มังคุด 2 = 2 ชุด (SRC-036 L2)', input: { n_a: 2, n_b: 1, y: 1, qty_a: 4, qty_b: 2, qty_free_in_cart: 2, ...other } },
  { note: 'ordinary: y > 1 — ส้ม 1 + มังคุด 1 แถม ลำไย 2 · ซื้อ 2 ชุด สิทธิ์ 4', input: { n_a: 1, n_b: 1, y: 2, qty_a: 2, qty_b: 2, qty_free_in_cart: 4, ...other } },
  { note: 'boundary ①: a ไม่ครบต่อชุด (ส้ม 1 < 2) → ไม่ได้แถม ลำไยที่หยิบคิดราคาปกติ', input: { n_a: 2, n_b: 1, y: 1, qty_a: 1, qty_b: 1, qty_free_in_cart: 1, ...other } },
  { note: 'boundary ①: b ไม่มีในตะกร้า → ไม่ได้แถม', input: { n_a: 2, n_b: 1, y: 1, qty_a: 2, qty_b: 0, qty_free_in_cart: 1, ...other } },
  { note: 'boundary ②: ฝั่ง a มีเกิน (ส้มพอ 3 ชุด มังคุดพอ 1 ชุด) → 1 ชุด', input: { n_a: 1, n_b: 1, y: 1, qty_a: 3, qty_b: 1, qty_free_in_cart: 1, ...other } },
  { note: 'boundary ③: หยิบของแถมเกินสิทธิ์ (สิทธิ์ 1 หยิบ 3) → ฟรี 1 ส่วนเกิน 2 คิดราคาปกติ', input: { n_a: 1, n_b: 1, y: 1, qty_a: 1, qty_b: 1, qty_free_in_cart: 3, ...other } },
  { note: 'boundary ③: หยิบน้อยกว่าสิทธิ์ (สิทธิ์ 2 หยิบ 1) → ฟรี 1 ระบบไม่เพิ่มให้เอง', input: { n_a: 1, n_b: 1, y: 1, qty_a: 2, qty_b: 2, qty_free_in_cart: 1, ...other } },
  { note: 'boundary ④: ราคาลำไยตอนหยิบ 19.75 แต่แก้เป็น 22.00 ก่อนกดชำระ → ใช้ 22.00 (แถวนี้ทดสอบการเลือกราคา ไม่ใช่เลขคณิต — price_at_add ไม่ถูกใช้)', input: { n_a: 1, n_b: 1, y: 1, qty_a: 1, qty_b: 1, qty_free_in_cart: 1, ...ab, price_at_add_free: LONGAN, unit_price_free: '22.00' } },
  { note: 'boundary ⑤: ลำไยหมดสต็อก หยิบไม่ได้ (qty_free_in_cart 0) → ส้ม + มังคุด ราคาปกติ ชำระได้ · ข้อความ "คงเหลือไม่พอ" เป็นของ BR-talad-007', input: { n_a: 1, n_b: 1, y: 1, qty_a: 1, qty_b: 1, qty_free_in_cart: 0, ...other } },
];
const freeACases = [
  { note: 'ordinary: ของแถมเป็น a — ส้ม 2 + มังคุด 1 แถม ส้ม 1 · หยิบส้ม 3 มังคุด 1 → ฟรี 1 (SRC-036 L4)', input: { n_a: 2, n_b: 1, y: 1, qty_a: 3, qty_b: 1, ...ab } },
  { note: 'boundary ⑥: ของแถมเป็น a — หยิบส้ม 2 มังคุด 1 (ไม่ถึง n_a + y = 3) → ไม่ได้แถม จ่ายเต็ม (SRC-036 L7)', input: { n_a: 2, n_b: 1, y: 1, qty_a: 2, qty_b: 1, ...ab } },
  { note: 'ordinary: ของแถมเป็น a แถมซ้ำ — ส้ม 6 มังคุด 2 → 2 ชุด ฟรี 2', input: { n_a: 2, n_b: 1, y: 1, qty_a: 6, qty_b: 2, ...ab } },
  { note: 'boundary ②: ของแถมเป็น a แต่ b พอชุดเดียว — ส้ม 6 มังคุด 1 → 1 ชุด ฟรี 1', input: { n_a: 2, n_b: 1, y: 1, qty_a: 6, qty_b: 1, ...ab } },
  { note: 'boundary ⑥: ของแถมเป็น a ชุดที่ 2 ไม่ครบ — ส้ม 5 มังคุด 2 → 1 ชุด ฟรี 1 (ส้ม 2 ชิ้นที่เหลือจ่ายเต็ม)', input: { n_a: 2, n_b: 1, y: 1, qty_a: 5, qty_b: 2, ...ab } },
];
// ของแถมเป็น b: เรียก freeIsA โดยสลับ — a ในฟังก์ชัน = มังคุด (ของแถม), b ในฟังก์ชัน = ส้ม
const freeBCases = [
  { note: 'ordinary: ของแถมเป็น b — ส้ม 1 + มังคุด 1 แถม มังคุด 1 · หยิบส้ม 1 มังคุด 2 → ฟรี 1', input: { n_a: 1, n_b: 1, y: 1, qty_a: 1, qty_b: 2, ...ab } },
  { note: 'boundary ⑥: ของแถมเป็น b — หยิบส้ม 1 มังคุด 1 (ไม่ถึง n_b + y = 2) → ไม่ได้แถม', input: { n_a: 1, n_b: 1, y: 1, qty_a: 1, qty_b: 1, ...ab } },
];
const swap = (i) => ({ n_a: i.n_b, n_b: i.n_a, y: i.y, qty_a: i.qty_b, unit_price_a: i.unit_price_b, qty_b: i.qty_a, unit_price_b: i.unit_price_a });
const unswapLines = (r) => ({ ...r, lines: r.lines.map((l) => ({ ...l, item: l.item === 'a' ? 'b' : 'a' })).reverse() });

export const rows = [
  ...otherCases.map((c) => ({ input: { kind: 'free_other', ...c.input }, expected: freeOther(c.input), note: c.note })),
  ...freeACases.map((c) => ({ input: { kind: 'free_is_a', ...c.input }, expected: freeIsA(c.input), note: c.note })),
  ...freeBCases.map((c) => ({ input: { kind: 'free_is_b', ...c.input }, expected: unswapLines(freeIsA(swap(c.input))), note: c.note })),
];
if (import.meta.url === pathToFileURL(process.argv[1]).href) console.log(JSON.stringify(rows, null, 2));
