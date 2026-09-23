// Golden dataset generator for CALC-talad-003@v1 (constrains BR-talad-009@v1)
// ส่วนลด % ต่อสินค้า และส่วนลด % ทั้งบิล (มียอดขั้นต่ำ · หลายโปรใช้อันที่ลดมากสุด)
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → implemented directly, not via a language default
//   rounding_points item discount rounded ONCE per line (price × qty × rate), not per unit;
//                   bill_discount rounded once — both at 0.01 baht, the moment they are computed
//   eligibility     subtotal ≥ min_subtotal (subtotal = after item-level promos, per CALC-talad-001)
//   selection       highest rate among eligible bill promos; none eligible → rate 0
//
// Item rows also report what PER-UNIT rounding would have produced (informational only),
// so a reader can see which rows distinguish the two readings of ①a.
//
// Run: node <state-dir>/golden/CALC-talad-003@v1.mjs   → prints rows as JSON

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  if (f.length > 2) throw new Error(`money(2) input has more than 2 decimals: ${baht}`);
  return BigInt(i.replace(/,/g, '')) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => `${s / 100n}.${String(s % 100n).padStart(2, '0')}`;
const checkRate = (r) => { if (!Number.isInteger(r) || r < 0 || r > 100) throw new Error(`rate must be int 0–100: ${r}`); };
const pctHalfUp = (amount, rate) => (amount * BigInt(rate) + 50n) / 100n;   // amount ≥ 0

export function itemDiscount({ unit_price, qty, item_rate }) {
  checkRate(item_rate);
  if (!Number.isInteger(qty) || qty < 0) throw new Error(`qty must be int ≥ 0: ${qty}`);
  const gross = toSatang(unit_price) * BigInt(qty);
  const d = pctHalfUp(gross, item_rate);
  return { line_gross: toBaht(gross), item_pct_discount: toBaht(d), line_net: toBaht(gross - d) };
}

export function billDiscount({ items, bill_promos }) {
  let subtotal = 0n;
  for (const it of items) {
    const gross = toSatang(it.unit_price) * BigInt(it.qty);
    subtotal += gross - pctHalfUp(gross, it.item_rate ?? 0);
  }
  const eligible = bill_promos.filter((p) => { checkRate(p.rate); return subtotal >= toSatang(p.min_subtotal); });
  const bill_rate = eligible.reduce((m, p) => Math.max(m, p.rate), 0);
  const d = pctHalfUp(subtotal, bill_rate);
  return {
    subtotal: toBaht(subtotal),
    eligible_promos: eligible.map((p) => `${p.rate}% ขั้นต่ำ ${p.min_subtotal}`),
    bill_rate,
    bill_discount: toBaht(d),
    after_bill: toBaht(subtotal - d),
  };
}

const perUnit = ({ unit_price, qty, item_rate }) => toBaht(pctHalfUp(toSatang(unit_price), item_rate) * BigInt(qty));

const P500 = { rate: 5, min_subtotal: '500.00' };
const P1000 = { rate: 10, min_subtotal: '1000.00' };

const itemCases = [
  { note: 'ordinary: ต่อสินค้า คิดทั้งบรรทัด ปัดครั้งเดียว (ลำไย 3 × 19.75 ลด 7%)', input: { unit_price: '19.75', qty: 3, item_rate: 7 } },
  { note: 'ordinary: ต่อสินค้า ลงตัว (ส้ม 2 × 45 ลด 10%) — ตรงกับส่วนลดโปร 9 บาทใน EX-talad-065', input: { unit_price: '45.00', qty: 2, item_rate: 10 } },
  { note: 'boundary: item_rate = 0', input: { unit_price: '45.00', qty: 2, item_rate: 0 } },
  { note: 'boundary: item_rate = 100 → ลดเต็มบรรทัด ยอดไม่ติดลบ', input: { unit_price: '45.00', qty: 2, item_rate: 100 } },
];
const billCases = [
  { note: 'boundary: ยอด = ขั้นต่ำพอดี (500.00) → ได้ลด', input: { items: [{ unit_price: '500.00', qty: 1, item_rate: 0 }], bill_promos: [P500] } },
  { note: 'boundary: ยอด 499.99 ต่ำกว่าขั้นต่ำ 0.01 → ไม่ได้ลด', input: { items: [{ unit_price: '499.99', qty: 1, item_rate: 0 }], bill_promos: [P500] } },
  { note: 'boundary: ยอดเต็ม 520 ครบขั้นต่ำ แต่หลังโปรสินค้า 10% ไม่ครบ → ไม่ได้ลดทั้งบิล', input: { items: [{ unit_price: '130.00', qty: 4, item_rate: 10 }], bill_promos: [P500] } },
  { note: 'boundary: หลายโปรบิล ยอด 700 ครบเฉพาะโปร 500 → ได้ 5%', input: { items: [{ unit_price: '700.00', qty: 1, item_rate: 0 }], bill_promos: [P500, P1000] } },
  { note: 'boundary: หลายโปรบิล ยอด 1000 ครบทั้งสอง → เลือก 10%', input: { items: [{ unit_price: '1000.00', qty: 1, item_rate: 0 }], bill_promos: [P500, P1000] } },
  { note: 'boundary: ไม่มีโปรบิลไหนครบขั้นต่ำ → bill_rate 0', input: { items: [{ unit_price: '300.00', qty: 1, item_rate: 0 }], bill_promos: [P500, P1000] } },
  { note: 'boundary: bill rate 100 ขั้นต่ำ 0 → ลดเต็มยอด ยอดไม่ติดลบ', input: { items: [{ unit_price: '45.00', qty: 1, item_rate: 0 }], bill_promos: [{ rate: 100, min_subtotal: '0.00' }] } },
  { note: 'ordinary: ส่วนลดทั้งบิลที่มีเศษ (ยอดหลังโปรสินค้า × 5%)', input: { items: [{ unit_price: '19.75', qty: 3, item_rate: 7 }, { unit_price: '500.00', qty: 1, item_rate: 0 }], bill_promos: [P500] } },
];

const rows = [
  ...itemCases.map((c) => ({ input: { kind: 'item', ...c.input }, expected: itemDiscount(c.input), note: `${c.note} · [ข้อมูลประกอบ] ถ้าปัดต่อชิ้น = ${perUnit(c.input)}` })),
  ...billCases.map((c) => ({ input: { kind: 'bill', ...c.input }, expected: billDiscount(c.input), note: c.note })),
];
console.log(JSON.stringify(rows, null, 2));
