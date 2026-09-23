// Golden dataset generator for CALC-talad-002@v1 (constrains BR-talad-027@v1)
// กฎปัดส่วนลด %: discount = round_HALF_UP(base × rate / 100, 0.01) ; after = base − discount
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → implemented directly, NOT via a language default
//                              (.NET Math.Round / JS toFixed are not HALF_UP)
//   rounding_points discount rounded to 0.01 baht the moment it is computed
//   inputs          base money(2), rate int percent 0–100
//
// Each row also reports what HALF_EVEN would have produced, so a reader can see
// which rows distinguish the two modes. That column is informational only.
//
// Run: node <state-dir>/golden/CALC-talad-002@v1.mjs   → prints rows as JSON

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  if (f.length > 2) throw new Error(`money(2) input has more than 2 decimals: ${baht}`);
  return BigInt(i) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => `${s / 100n}.${String(s % 100n).padStart(2, '0')}`;
// exact value of base × rate / 100 expressed in 1/100 satang (i.e. 4 decimals of baht)
const exact4 = (base, rate) => {
  const x = base * BigInt(rate);                 // satang × percent = 1/100 satang units
  return `${x / 10000n}.${String(x % 10000n).padStart(4, '0')}`;
};
const halfUp = (base, rate) => (base * BigInt(rate) + 50n) / 100n;
const halfEven = (base, rate) => {
  const x = base * BigInt(rate), q = x / 100n, r = x % 100n;
  if (r > 50n) return q + 1n;
  if (r < 50n) return q;
  return q % 2n === 0n ? q : q + 1n;
};

export function discount({ base, rate }) {
  if (!Number.isInteger(rate) || rate < 0 || rate > 100) throw new Error(`rate must be int 0–100: ${rate}`);
  const b = toSatang(base);
  if (b < 0n) throw new Error('base must be ≥ 0');
  const d = halfUp(b, rate);
  return { exact: exact4(b, rate), discount: toBaht(d), after: toBaht(b - d) };
}

const cases = [
  { note: 'boundary: เศษครึ่งสตางค์พอดี → ปัดขึ้น (9.045 → 9.05) · HALF_EVEN จะได้ 9.04', input: { base: '180.90', rate: 5 } },
  { note: 'boundary: เศษครึ่งสตางค์พอดี ยอดเล็ก (0.005 → 0.01) · HALF_EVEN จะได้ 0.00', input: { base: '0.10', rate: 5 } },
  { note: 'ordinary: เศษครึ่งสตางค์ที่ HALF_UP กับ HALF_EVEN ให้ผลเท่ากัน (4.515 → 4.52)', input: { base: '90.30', rate: 5 } },
  { note: 'boundary: ต่ำกว่าครึ่งสตางค์ → ปัดลง (SRC-008: 15% ของ 19.75 = 2.9625 → 2.96)', input: { base: '19.75', rate: 15 } },
  { note: 'ordinary: สูงกว่าครึ่งสตางค์ → ปัดขึ้น (7% ของ 0.99)', input: { base: '0.99', rate: 7 } },
  { note: 'boundary: ลงตัว 2 ตำแหน่งอยู่แล้ว → ไม่เปลี่ยน (5% ของ 90 = 4.50)', input: { base: '90.00', rate: 5 } },
  { note: 'boundary: base = 0 → 0.00', input: { base: '0.00', rate: 10 } },
  { note: 'boundary: rate = 0 → 0.00', input: { base: '45.00', rate: 0 } },
  { note: 'boundary: rate = 100 → ส่วนลดเท่ากับยอดฐาน', input: { base: '45.00', rate: 100 } },
];

const rows = cases.map((c) => {
  const b = toSatang(c.input.base);
  return {
    input: c.input,
    expected: discount(c.input),
    note: `${c.note} · [ข้อมูลประกอบ] HALF_EVEN = ${toBaht(halfEven(b, c.input.rate))}`,
  };
});
console.log(JSON.stringify(rows, null, 2));
