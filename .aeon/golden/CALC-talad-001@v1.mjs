// Golden dataset generator for CALC-talad-001@v1 (constrains BR-talad-028@v1)
// ลำดับคิดยอดสุทธิของบิล: ยอดรายการหลังโปรระดับสินค้า → ส่วนลดทั้งบิล → ส่วนลดสมาชิก (คิดต่อกัน ไม่บวก % กัน)
//
// Contract fields implemented:
//   numeric_type    decimal  → all money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP
//   rounding_points each discount is rounded to 0.01 baht the moment it is computed:
//                   item_promo_discount[i] (arrives already rounded, from the promo contracts),
//                   bill_discount, member_discount. Sums/differences carry no excess digits.
//   rates           int percent 0–100
//
// Run: node <state-dir>/golden/CALC-talad-001@v1.mjs   → prints rows as JSON

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  if (f.length > 2) throw new Error(`money(2) input has more than 2 decimals: ${baht}`);
  return BigInt(i) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => {
  const neg = s < 0n, a = neg ? -s : s;
  return `${neg ? '-' : ''}${a / 100n}.${String(a % 100n).padStart(2, '0')}`;
};
// round(amount × rate / 100) HALF_UP to whole satang; amount ≥ 0, rate int 0–100
const pctHalfUp = (amountSatang, rate) => {
  if (!Number.isInteger(rate) || rate < 0 || rate > 100) throw new Error(`rate must be int 0–100: ${rate}`);
  if (amountSatang < 0n) throw new Error('amount must be ≥ 0');
  const x = amountSatang * BigInt(rate);           // satang × percent
  return (x + 50n) / 100n;                          // HALF_UP for non-negative values
};

export function net({ items, bill_rate = 0, member_rate = 0 }) {
  const lines = items.map((it) => {
    const gross = toSatang(it.unit_price) * BigInt(it.qty);
    const promo = toSatang(it.item_promo_discount ?? '0');
    if (promo > gross) throw new Error('item_promo_discount exceeds line gross');
    return gross - promo;
  });
  const subtotal = lines.reduce((a, b) => a + b, 0n);
  const bill_discount = pctHalfUp(subtotal, bill_rate);
  const after_bill = subtotal - bill_discount;
  const member_discount = pctHalfUp(after_bill, member_rate);
  const net = after_bill - member_discount;
  if (net < 0n) throw new Error('net < 0');
  return {
    subtotal: toBaht(subtotal),
    bill_discount: toBaht(bill_discount),
    after_bill: toBaht(after_bill),
    member_discount: toBaht(member_discount),
    net: toBaht(net),
  };
}

const cases = [
  { note: 'boundary: ไม่มีส่วนลดทั้งบิล และไม่ผูกสมาชิก (rate = 0 ทั้งคู่)',
    input: { items: [{ unit_price: '45.00', qty: 2, item_promo_discount: '0.00' }, { unit_price: '10.00', qty: 1, item_promo_discount: '0.00' }], bill_rate: 0, member_rate: 0 } },
  { note: 'ordinary: SRC-008 — 100 → โปร 10% = 90 → สมาชิก 5% = 85.50',
    input: { items: [{ unit_price: '100.00', qty: 1, item_promo_discount: '10.00' }], bill_rate: 0, member_rate: 5 } },
  { note: 'ordinary: EX-talad-065 — ส้ม 2×45 โปร 9 + มังคุด 120 · สมาชิก 5%',
    input: { items: [{ unit_price: '45.00', qty: 2, item_promo_discount: '9.00' }, { unit_price: '120.00', qty: 1, item_promo_discount: '0.00' }], bill_rate: 0, member_rate: 5 } },
  { note: 'ordinary: ทั้งบิล 10% แล้วสมาชิก 5% คิดต่อกัน — ส่วนลดสมาชิกตกครึ่งสตางค์พอดี (HALF_UP)',
    input: { items: [{ unit_price: '45.00', qty: 2, item_promo_discount: '9.00' }, { unit_price: '120.00', qty: 1, item_promo_discount: '0.00' }], bill_rate: 10, member_rate: 5 } },
  { note: 'rounding: SRC-008 QB-calc-01 — 15% ของ 19.75 = 2.9625',
    input: { items: [{ unit_price: '19.75', qty: 1, item_promo_discount: '0.00' }], bill_rate: 0, member_rate: 15 } },
  { note: 'boundary: rate = 100 → net = 0 ชำระได้',
    input: { items: [{ unit_price: '45.00', qty: 1, item_promo_discount: '0.00' }], bill_rate: 0, member_rate: 100 } },
  { note: 'boundary: net ไม่ติดลบ — ทั้งบิล 100% แล้วสมาชิก 50%',
    input: { items: [{ unit_price: '45.00', qty: 1, item_promo_discount: '0.00' }], bill_rate: 100, member_rate: 50 } },
  { note: 'boundary: subtotal = 0 (ของแถมล้วน) → ส่วนลดทุกขั้น 0',
    input: { items: [{ unit_price: '120.00', qty: 1, item_promo_discount: '120.00' }], bill_rate: 10, member_rate: 5 } },
];

const rows = cases.map((c) => ({ input: c.input, expected: net(c.input), note: c.note }));
console.log(JSON.stringify(rows, null, 2));
