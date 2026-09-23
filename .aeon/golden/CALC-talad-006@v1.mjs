// Golden dataset generator for CALC-talad-006@v1 (constrains BR-talad-013@v1)
// โปรโมชั่น ซื้อ a + b ลด y% — จำนวน a, b ต่อชุดตั้งได้ · นับชุดตามฝั่งที่น้อยกว่า ·
// ลดเฉพาะชิ้นที่อยู่ในชุดครบคู่ · คิดและปัดแยกต่อบรรทัด
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → implemented directly, not via a language default
//   rounding_points disc_a rounded ONCE and disc_b rounded ONCE, at 0.01 baht, the moment each is computed
//                   (not per unit, not per set) — same as CALC-talad-003
//   formula         sets = floor(min(qty_a / n_a, qty_b / n_b))
//                   disc_a = round(sets × n_a × unit_price_a × y / 100) ; disc_b likewise for b
//   price           unit price AT CHECKOUT (BR-talad-038)
//
// Every row also reports what rounding the COMBINED set discount once would have produced
// (informational only), so a reader can see which rows distinguish the two readings of ⑤.
//
// Run: node <state-dir>/golden/CALC-talad-006@v1.mjs   → prints rows as JSON

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

export function abPercent({ n_a, n_b, y, qty_a, unit_price_a, qty_b, unit_price_b }) {
  checkInt('n_a', n_a, 1); checkInt('n_b', n_b, 1); checkRate(y);
  checkInt('qty_a', qty_a, 0); checkInt('qty_b', qty_b, 0);
  const sets = Math.min(Math.floor(qty_a / n_a), Math.floor(qty_b / n_b));
  const pa = toSatang(unit_price_a), pb = toSatang(unit_price_b);
  const inSetA = pa * BigInt(sets * n_a), inSetB = pb * BigInt(sets * n_b);
  const disc_a = pctHalfUp(inSetA, y);                       // rounding point 1 of 2
  const disc_b = pctHalfUp(inSetB, y);                       // rounding point 2 of 2
  const gross_a = pa * BigInt(qty_a), gross_b = pb * BigInt(qty_b);
  const line = (item, unit_price, qty, discounted_qty, gross, d) =>
    ({ item, unit_price, qty, discounted_qty, line_gross: toBaht(gross), item_promo_discount: toBaht(d), line_net: toBaht(gross - d) });
  return {
    sets,
    lines: [line('a', unit_price_a, qty_a, sets * n_a, gross_a, disc_a), line('b', unit_price_b, qty_b, sets * n_b, gross_b, disc_b)],
    promo_discount: toBaht(disc_a + disc_b),
    total_gross: toBaht(gross_a + gross_b),
    total_pay: toBaht(gross_a + gross_b - disc_a - disc_b),
    info_if_rounded_combined: toBaht(pctHalfUp(inSetA + inSetB, y)),
  };
}

const ORANGE = '45.00';      // ส้มสายน้ำผึ้ง (EX-talad-001)
const MANGOSTEEN = '120.00'; // มังคุด แพ็ก (EX-talad-003)
const LONGAN = '19.75';      // ลำไย — ราคามีสตางค์ (GD-talad-003)
const ob = { unit_price_a: ORANGE, unit_price_b: MANGOSTEEN };

// ⑤: pick, by search rather than by hand, the smallest y at which rounding per line and rounding the
// combined set disagree — the row that tells the two readings apart. With ลำไย + ส้ม no such y exists
// (ส้ม's discount is always whole satang, so only one line ever carries a remainder); it takes two lines
// that BOTH carry satang, so the second line uses a hypothetical price, labelled as such in the note.
const lo = { unit_price_a: LONGAN, unit_price_b: ORANGE };
const HYPOTHETICAL = '24.25';
const ll = { unit_price_a: LONGAN, unit_price_b: HYPOTHETICAL };
const ySplit = Array.from({ length: 100 }, (_, i) => i + 1).find((y) => {
  const r = abPercent({ n_a: 1, n_b: 1, y, qty_a: 1, qty_b: 1, ...ll });
  return r.promo_discount !== r.info_if_rounded_combined;
});
if (ySplit === undefined) throw new Error('no y in 1–100 separates per-line from combined rounding for the chosen prices');

const cases = [
  { note: 'ordinary: ซื้อ ส้ม 1 + มังคุด 1 ลด 10% ครบพอดี', input: { n_a: 1, n_b: 1, y: 10, qty_a: 1, qty_b: 1, ...ob } },
  { note: 'ordinary: ตั้งจำนวนต่อชุดได้ — ส้ม 2 + มังคุด 1 ลด 10% (SRC-037 L1)', input: { n_a: 2, n_b: 1, y: 10, qty_a: 2, qty_b: 1, ...ob } },
  { note: 'ordinary: ลดซ้ำตามชุด — ส้ม 4 มังคุด 2 = 2 ชุด', input: { n_a: 2, n_b: 1, y: 10, qty_a: 4, qty_b: 2, ...ob } },
  { note: 'boundary ②: ตัวอย่างของเจ้าของงาน — ส้ม 1 + มังคุด 1 ลด 10% ตะกร้ามีส้ม 3 มังคุด 1 → ลดเฉพาะส้ม 1 ชิ้นในชุด (SRC-037 L2)', input: { n_a: 1, n_b: 1, y: 10, qty_a: 3, qty_b: 1, ...ob } },
  { note: 'boundary ②: ฝั่ง b มีเกิน — ส้ม 1 มังคุด 3 → ลดเฉพาะมังคุด 1 ลูกในชุด', input: { n_a: 1, n_b: 1, y: 10, qty_a: 1, qty_b: 3, ...ob } },
  { note: 'boundary ①: a ไม่ครบต่อชุด (ส้ม 1 < 2) → ไม่ลด', input: { n_a: 2, n_b: 1, y: 10, qty_a: 1, qty_b: 1, ...ob } },
  { note: 'boundary ①: ไม่มี b → ไม่ลด', input: { n_a: 2, n_b: 1, y: 10, qty_a: 2, qty_b: 0, ...ob } },
  { note: 'boundary ③: มังคุดตอนหยิบ 120.00 แก้เป็น 130.00 ก่อนกดชำระ → ใช้ 130.00 (แถวนี้ทดสอบการเลือกราคา — price_at_add ไม่ถูกใช้)', input: { n_a: 1, n_b: 1, y: 10, qty_a: 1, qty_b: 1, unit_price_a: ORANGE, price_at_add_b: MANGOSTEEN, unit_price_b: '130.00' } },
  { note: 'boundary ④: y = 0 → ไม่ลด', input: { n_a: 1, n_b: 1, y: 0, qty_a: 1, qty_b: 1, ...ob } },
  { note: 'boundary ④: y = 100 → ชิ้นในชุดฟรี ชิ้นเกินจ่ายเต็ม ยอดไม่ติดลบ (ส้ม 2 มังคุด 1)', input: { n_a: 1, n_b: 1, y: 100, qty_a: 2, qty_b: 1, ...ob } },
  { note: 'boundary ⑤: ส่วนลดมีเศษ — ลำไย 1 + ส้ม 1 ลด 7% ปัดแยกต่อบรรทัด', input: { n_a: 1, n_b: 1, y: 7, qty_a: 1, qty_b: 1, ...lo } },
  { note: `boundary ⑤: y ที่เล็กที่สุดซึ่งปัดแยกต่อบรรทัดกับปัดรวมได้ผลต่างกัน (ค้นด้วยสคริปต์) — ลำไย 19.75 + สินค้า b ราคาสมมติ ${HYPOTHETICAL} ลด ${ySplit}%`, input: { n_a: 1, n_b: 1, y: ySplit, qty_a: 1, qty_b: 1, ...ll } },
  { note: 'boundary ⑤: ลำไย + ส้ม ไม่มี y ไหนที่ปัดแยกกับปัดรวมต่างกัน (ส่วนลดส้มลงตัวเสมอ) — แถวนี้ยืนยันว่า 2 วิธีเท่ากัน ลด 5%', input: { n_a: 1, n_b: 1, y: 5, qty_a: 1, qty_b: 1, ...lo } },
  { note: 'boundary ⑤: หลายชุดมีเศษ — ลำไย 3 + ส้ม 3 ลด 7% (ปัดครั้งเดียวต่อบรรทัด ไม่ปัดต่อชุด)', input: { n_a: 1, n_b: 1, y: 7, qty_a: 3, qty_b: 3, ...lo } },
];

export const rows = cases.map((c) => {
  const expected = abPercent(c.input);
  const { info_if_rounded_combined, ...rest } = expected;
  return { input: c.input, expected: rest, note: `${c.note} · [ข้อมูลประกอบ] ถ้าปัดรวมทั้งชุดครั้งเดียว = ${info_if_rounded_combined}` };
});
if (import.meta.url === pathToFileURL(process.argv[1]).href) console.log(JSON.stringify(rows, null, 2));
