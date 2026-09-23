// Golden dataset generator for CALC-talad-004@v1 (constrains BR-talad-011@v1)
// โปรโมชั่น ซื้อ x แถม y — คนละสินค้า / สินค้าเดียวกัน · แถมซ้ำตามจำนวนชุด · ของแถมลงเป็นส่วนลดเท่าราคา
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → declared by the contract but NEVER exercised: there is no rounding point
//   rounding_points none     → free_discount = int × money(2) is exact in satang, so no rounding helper
//                              is called; the script asserts that instead of rounding
//   different item  entitled = floor(qty_buy / x) × y ; free_qty = min(qty_free_in_cart, entitled)
//                   free_discount = free_qty × unit_price_free  (on the FREE item's line)
//   same item       free_qty = floor(qty_in_cart / (x + y)) × y ; free_discount = free_qty × unit_price
//   price           unit price AT CHECKOUT (BR-talad-038) — never the price when the item was added
//
// Output is named for CALC-talad-001's input: free_discount lands as item_promo_discount on the line
// that carries the free item, so these rows can be cross-checked against GD-talad-001.
//
// Contract gap (not renamed to fit): the same-item formula uses `unit_price`, but inputs[] declares only
// `unit_price_free`. This script uses `unit_price` as the formula writes it.
//
// Run: node <state-dir>/golden/CALC-talad-004@v1.mjs   → prints rows as JSON

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  if (f.length > 2) throw new Error(`money(2) input has more than 2 decimals: ${baht}`);
  return BigInt(i.replace(/,/g, '')) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => `${s / 100n}.${String(s % 100n).padStart(2, '0')}`;
const checkInt = (name, v, min) => { if (!Number.isInteger(v) || v < min) throw new Error(`${name} must be int ≥ ${min}: ${v}`); };
const floorDiv = (a, b) => Math.floor(a / b);   // both non-negative ints

function line(unit_price, qty, item_promo_discount) {
  const gross = toSatang(unit_price) * BigInt(qty);
  if (item_promo_discount > gross) throw new Error('discount exceeds line gross');
  return { unit_price, qty, line_gross: toBaht(gross), item_promo_discount: toBaht(item_promo_discount), line_net: toBaht(gross - item_promo_discount) };
}

// คนละสินค้า (buy ≠ free)
export function differentItem({ x, y, qty_buy, unit_price_buy, qty_free_in_cart, unit_price_free }) {
  checkInt('x', x, 1); checkInt('y', y, 1); checkInt('qty_buy', qty_buy, 0); checkInt('qty_free_in_cart', qty_free_in_cart, 0);
  const entitled = floorDiv(qty_buy, x) * y;
  const free_qty = Math.min(qty_free_in_cart, entitled);
  const free_discount = BigInt(free_qty) * toSatang(unit_price_free);   // exact — no rounding point
  return {
    entitled,
    free_qty,
    charged_free_item_qty: qty_free_in_cart - free_qty,
    buy_line: line(unit_price_buy, qty_buy, 0n),
    free_line: line(unit_price_free, qty_free_in_cart, free_discount),
    free_discount: toBaht(free_discount),
  };
}

// สินค้าเดียวกัน (buy = free)
export function sameItem({ x, y, qty_in_cart, unit_price }) {
  checkInt('x', x, 1); checkInt('y', y, 1); checkInt('qty_in_cart', qty_in_cart, 0);
  const free_qty = floorDiv(qty_in_cart, x + y) * y;
  const free_discount = BigInt(free_qty) * toSatang(unit_price);        // exact — no rounding point
  return { free_qty, charged_qty: qty_in_cart - free_qty, line: line(unit_price, qty_in_cart, free_discount), free_discount: toBaht(free_discount) };
}

const ORANGE = '45.00';     // ส้มสายน้ำผึ้ง (EX-talad-001)
const MANGOSTEEN = '120.00'; // มังคุด แพ็ก (EX-talad-003)
const LONGAN = '19.75';     // ลำไย — ราคามีสตางค์ (GD-talad-003)

const diffCases = [
  { note: 'ordinary: ซื้อส้ม 2 แถมมังคุด 1 ครบพอดี — ตรงกับ EX-talad-068 (SRC-035 L1)',
    input: { x: 2, y: 1, qty_buy: 2, unit_price_buy: ORANGE, qty_free_in_cart: 1, unit_price_free: MANGOSTEEN } },
  { note: 'ordinary: ซื้อส้ม 5 โปรซื้อ 2 แถม 1 → แถมซ้ำตามชุด ได้ 2 (SRC-035 L2)',
    input: { x: 2, y: 1, qty_buy: 5, unit_price_buy: ORANGE, qty_free_in_cart: 2, unit_price_free: MANGOSTEEN } },
  { note: 'boundary ①: qty_buy < x → entitled 0 · มังคุดที่หยิบคิดราคาปกติ',
    input: { x: 2, y: 1, qty_buy: 1, unit_price_buy: ORANGE, qty_free_in_cart: 1, unit_price_free: MANGOSTEEN } },
  { note: 'boundary ②: หยิบของแถมเกินสิทธิ์ (สิทธิ์ 1 หยิบ 3) → ฟรี 1 ส่วนเกิน 2 คิดราคาปกติ',
    input: { x: 2, y: 1, qty_buy: 2, unit_price_buy: ORANGE, qty_free_in_cart: 3, unit_price_free: MANGOSTEEN } },
  { note: 'boundary ③: หยิบน้อยกว่าสิทธิ์ (สิทธิ์ 2 หยิบ 1) → ฟรี 1 ระบบไม่เพิ่มให้เอง',
    input: { x: 2, y: 1, qty_buy: 4, unit_price_buy: ORANGE, qty_free_in_cart: 1, unit_price_free: MANGOSTEEN } },
  { note: 'boundary ④: ราคามังคุดตอนหยิบ 120.00 แต่เจ้าของร้านแก้เป็น 130.00 ก่อนกดชำระ → ใช้ 130.00 (แถวนี้ทดสอบการเลือกราคา ไม่ใช่เลขคณิต — price_at_add ไม่ถูกใช้)',
    input: { x: 2, y: 1, qty_buy: 2, unit_price_buy: ORANGE, qty_free_in_cart: 1, price_at_add_free: MANGOSTEEN, unit_price_free: '130.00' } },
  { note: 'boundary ⑤: มังคุดหมดสต็อก หยิบไม่ได้ (qty_free_in_cart 0) → ส้มคิดราคาปกติ ชำระได้ · ข้อความ "คงเหลือไม่พอ" เป็นของ BR-talad-007 ไม่ใช่ตัวเลข',
    input: { x: 2, y: 1, qty_buy: 2, unit_price_buy: ORANGE, qty_free_in_cart: 0, unit_price_free: MANGOSTEEN } },
  { note: 'ordinary: y > 1 และของแถมราคามีสตางค์ — ซื้อส้ม 3 แถมลำไย 2 · ซื้อ 7 → สิทธิ์ 4',
    input: { x: 3, y: 2, qty_buy: 7, unit_price_buy: ORANGE, qty_free_in_cart: 4, unit_price_free: LONGAN } },
  { note: '⚠ เทียบกับแถว same-item x=3 y=2 qty 4: คนละสินค้า ซื้อ 3 หยิบของแถม 1 (สิทธิ์ 2) → ฟรี 1',
    input: { x: 3, y: 2, qty_buy: 3, unit_price_buy: ORANGE, qty_free_in_cart: 1, unit_price_free: LONGAN } },
];

const sameCases = [
  { note: 'boundary ⑥: สินค้าเดียวกัน qty < x+y — ซื้อ 2 แถม 1 หยิบ 2 → จ่ายเต็ม 2',
    input: { x: 2, y: 1, qty_in_cart: 2, unit_price: ORANGE } },
  { note: 'ordinary: ส้ม ซื้อ 2 แถม 1 หยิบ 3 → ฟรี 1 (SRC-035 L5)',
    input: { x: 2, y: 1, qty_in_cart: 3, unit_price: ORANGE } },
  { note: 'ordinary: ส้ม ซื้อ 2 แถม 1 หยิบ 5 → ฟรี 1 (SRC-035 L5)',
    input: { x: 2, y: 1, qty_in_cart: 5, unit_price: ORANGE } },
  { note: 'ordinary: ส้ม ซื้อ 2 แถม 1 หยิบ 6 → ฟรี 2 (SRC-035 L5)',
    input: { x: 2, y: 1, qty_in_cart: 6, unit_price: ORANGE } },
  { note: 'ordinary: y > 1 ราคามีสตางค์ — ลำไย ซื้อ 3 แถม 2 หยิบ 10 → ฟรี 4',
    input: { x: 3, y: 2, qty_in_cart: 10, unit_price: LONGAN } },
  { note: '⚠ เทียบกับแถว different-item x=3 y=2 ซื้อ 3 หยิบแถม 1: สินค้าเดียวกัน ลำไย ซื้อ 3 แถม 2 หยิบ 4 → ฟรี 0',
    input: { x: 3, y: 2, qty_in_cart: 4, unit_price: LONGAN } },
];

const rows = [
  ...diffCases.map((c) => ({ input: { kind: 'different_item', ...c.input }, expected: differentItem(c.input), note: c.note })),
  ...sameCases.map((c) => ({ input: { kind: 'same_item', ...c.input }, expected: sameItem(c.input), note: c.note })),
];
console.log(JSON.stringify(rows, null, 2));
