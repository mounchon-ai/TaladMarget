// Golden dataset generator for CALC-talad-009@v1 (constrains BR-talad-010@v1)
// ส่วนลดสมาชิก — % เดียวทั้งร้าน ใช้ค่า ณ ตอนกดชำระ · ใช้กับบิลที่ผูกสมาชิกเท่านั้น ·
// คิดจากยอดหลังโปรและหลังส่วนลดทั้งบิล (after_bill)
//
// Contract fields implemented:
//   formula         member_rate = has_member ? shop_member_rate (value AT CHECKOUT) : 0
//                   member_discount / net → CALC-talad-001 net(), CALLED rather than re-derived: the
//                   contract states it is the same formula, and a second copy is a second thing to drift
//   numeric_type    decimal  → money held as integer satang (BigInt) inside CALC-talad-001
//   rounding_mode   HALF_UP  → CALC-talad-001's own helper
//   rounding_points member_discount rounded once at 0.01 baht — the same point as CALC-talad-001
//
// CALC-talad-001's script prints its own rows unconditionally when loaded and backs a signed answer
// key, so it is NOT edited: console.log is muted while it is imported instead.
//
// Run: node <state-dir>/golden/CALC-talad-009@v1.mjs   → prints rows as JSON

import { pathToFileURL } from 'node:url';

const log = console.log;
console.log = () => {};
const c1 = await import(new URL('./CALC-talad-001@v1.mjs', import.meta.url));
console.log = log;

const checkRate = (r) => { if (!Number.isInteger(r) || r < 0 || r > 100) throw new Error(`shop_member_rate must be int 0–100: ${r}`); };

export function memberBill({ items, bill_rate = 0, has_member, shop_member_rate_at_checkout }) {
  checkRate(shop_member_rate_at_checkout);
  if (typeof has_member !== 'boolean') throw new Error('has_member must be boolean');
  const member_rate = has_member ? shop_member_rate_at_checkout : 0;
  return { member_rate, ...c1.net({ items, bill_rate, member_rate }) };
}

const HUNDRED = [{ unit_price: '100.00', qty: 1, item_promo_discount: '0.00' }];   // "สินค้ารวม 100 บาท" (EX-talad-016/094)
const LONGAN = '19.75';

// Search: the smallest ลำไย quantity whose 5% member discount lands exactly on half a satang,
// so the row shows HALF_UP rounding up rather than a value that happens to be exact.
const halfSatang = (() => {
  for (let qty = 1; qty <= 40; qty++) {
    const gross = 1975n * BigInt(qty);           // satang
    if ((gross * 5n) % 100n === 50n) return qty;
  }
  throw new Error('no ลำไย quantity puts a 5% discount on exactly half a satang');
})();

const cases = [
  { note: 'ordinary: สมาชิก ส่วนลด 5% สินค้ารวม 100 บาท (ตรงกับ EX-talad-016 / 094)',
    input: { items: HUNDRED, has_member: true, shop_member_rate_at_checkout: 5 } },
  { note: 'boundary ①: บิลไม่ผูกสมาชิก → ไม่ลด แม้ร้านตั้ง 5% ไว้',
    input: { items: HUNDRED, has_member: false, shop_member_rate_at_checkout: 5 } },
  { note: 'boundary ②: ร้านตั้ง 0% → ไม่ลด (ตรงกับ EX-talad-095)',
    input: { items: HUNDRED, has_member: true, shop_member_rate_at_checkout: 0 } },
  { note: 'boundary ③: ร้านตั้ง 100% → ยอดชำระ 0 ชำระได้',
    input: { items: HUNDRED, has_member: true, shop_member_rate_at_checkout: 100 } },
  { note: 'boundary ④: ตอนผูกสมาชิกตั้ง 5% แล้วแก้เป็น 10% ก่อนกดชำระ → ใช้ 10% (ตรงกับ EX-talad-024 · rate_at_attach ไม่ถูกใช้)',
    input: { items: HUNDRED, has_member: true, rate_at_attach: 5, shop_member_rate_at_checkout: 10 } },
  { note: 'boundary ⑤: after_bill = 0 (ของแถมล้วน ส่วนลดโปรเท่าราคา) → ไม่ลด',
    input: { items: [{ unit_price: '120.00', qty: 1, item_promo_discount: '120.00' }], has_member: true, shop_member_rate_at_checkout: 5 } },
  { note: 'boundary ⑥: สมาชิกคนที่สอง (สมหมาย) ตะกร้าเดียวกัน % เดียวกัน → ผลเท่าแถว 1 (ตรงกับ EX-talad-094)',
    input: { items: HUNDRED, has_member: true, member: 'สมหมาย รักดี', shop_member_rate_at_checkout: 5 } },
  { note: `boundary ⑦: ส่วนลดตกครึ่งสตางค์พอดี → ปัด HALF_UP ขึ้น (ลำไย ${halfSatang} ชิ้น ค้นด้วยสคริปต์ · ส่วนลด 5%)`,
    input: { items: [{ unit_price: LONGAN, qty: halfSatang, item_promo_discount: '0.00' }], has_member: true, shop_member_rate_at_checkout: 5 } },
  { note: 'ordinary: คิดจากยอดหลังส่วนลดทั้งบิล — ส้ม 2 + มังคุด 1 · ทั้งบิล 10% แล้วสมาชิก 5% (คิดต่อกัน ไม่บวก %)',
    input: { items: [{ unit_price: '45.00', qty: 2, item_promo_discount: '0.00' }, { unit_price: '120.00', qty: 1, item_promo_discount: '0.00' }], bill_rate: 10, has_member: true, shop_member_rate_at_checkout: 5 } },
];

export const rows = cases.map((c) => ({ input: c.input, expected: memberBill(c.input), note: c.note }));
if (import.meta.url === pathToFileURL(process.argv[1]).href) console.log(JSON.stringify(rows, null, 2));
