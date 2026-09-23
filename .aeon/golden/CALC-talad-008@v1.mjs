// Golden dataset generator for CALC-talad-008@v1 (constrains BR-talad-029@v1)
// เลือกโปรระดับสินค้า — หนึ่งบรรทัดใช้ได้โปรเดียวทั้งบรรทัด · ไล่ทีละโปร (greedy) เลือกโปรที่ลดมากสุด
// (บาท หลังปัด) · บรรทัดที่ถูกใช้แล้วหลุดจากโปรอื่น · ลดเท่ากันที่อันดับสูงสุด → พนักงานเลือก
//
// Contract fields implemented:
//   numeric_type    decimal  → money held as integer satang (BigInt), never float baht
//   rounding_mode   HALF_UP  → not exercised here: this contract compares, it does not round
//   rounding_points none of its own — every disc_p is the ALREADY-ROUNDED value produced by the promo's
//                   own contract, obtained by CALLING that contract's golden script, never re-derived:
//                     pct (BR-009)        CALC-talad-003  itemDiscount
//                     xy_diff / xy_same   CALC-talad-004  differentItem / sameItem
//                     ab_free_other/_a    CALC-talad-005  freeOther / freeIsA
//                     ab_pct (BR-013)     CALC-talad-006  abPercent
//                     x_pct  (BR-014)     CALC-talad-007  xPercent
//   formula         loop { cand = promos not yet applied, evaluated on FREE lines only, disc > 0 ;
//                          top = cand with max disc ; one → pick it · several → staff_choice picks ;
//                          remove every line the pick touches (whole lines) } until cand is empty
//   after           bill-level discount afterwards, via CALC-talad-001 net() (rows that set bill_rate)
//   price           unit price AT CHECKOUT (BR-talad-038)
//
// CALC-talad-001/003/004's scripts print their own rows unconditionally when loaded, and they back
// signed answer keys, so they are NOT edited: console.log is muted while they are imported instead.
//
// Informational only: every row also reports the best total any DISJOINT set of promos could reach
// (brute force). Greedy is what the owner chose (SRC-040 L3); the gap is shown, never "fixed".
//
// Run: node <state-dir>/golden/CALC-talad-008@v1.mjs   → prints rows as JSON

import { pathToFileURL } from 'node:url';

const quiet = async (file) => {
  const log = console.log;
  console.log = () => {};
  try { return await import(new URL(file, import.meta.url)); } finally { console.log = log; }
};
const c1 = await quiet('./CALC-talad-001@v1.mjs');
const c3 = await quiet('./CALC-talad-003@v1.mjs');
const c4 = await quiet('./CALC-talad-004@v1.mjs');
const c5 = await import(new URL('./CALC-talad-005@v1.mjs', import.meta.url));
const c6 = await import(new URL('./CALC-talad-006@v1.mjs', import.meta.url));
const c7 = await import(new URL('./CALC-talad-007@v1.mjs', import.meta.url));

const toSatang = (baht) => {
  const [i, f = ''] = String(baht).split('.');
  return BigInt(i) * 100n + BigInt((f + '00').slice(0, 2));
};
const toBaht = (s) => `${s / 100n}.${String(s % 100n).padStart(2, '0')}`;

// Evaluate one promo against the lines still free. A line that is taken or absent counts as qty 0,
// so the promo's own contract decides whether anything is left to discount.
function evaluate(p, cart, free) {
  const line = (name) => (free.has(name) ? cart.get(name) : undefined);
  const qty = (name) => line(name)?.qty ?? 0;
  const price = (name) => line(name)?.unit_price ?? cart.get(name)?.unit_price ?? '0.00';
  let disc;                                               // { product: baht string }
  switch (p.kind) {
    case 'pct':
      disc = { [p.product]: c3.itemDiscount({ unit_price: price(p.product), qty: qty(p.product), item_rate: p.rate }).item_pct_discount };
      break;
    case 'xy_diff': {
      const r = c4.differentItem({ x: p.x, y: p.y, qty_buy: qty(p.buy), unit_price_buy: price(p.buy), qty_free_in_cart: qty(p.free), unit_price_free: price(p.free) });
      disc = { [p.buy]: '0.00', [p.free]: r.free_discount };
      break;
    }
    case 'xy_same':
      disc = { [p.product]: c4.sameItem({ x: p.x, y: p.y, qty_in_cart: qty(p.product), unit_price: price(p.product) }).free_discount };
      break;
    case 'ab_free_other': {
      const r = c5.freeOther({ n_a: p.n_a, n_b: p.n_b, y: p.y, qty_a: qty(p.a), unit_price_a: price(p.a), qty_b: qty(p.b), unit_price_b: price(p.b), qty_free_in_cart: qty(p.free), unit_price_free: price(p.free) });
      disc = { [p.a]: r.lines[0].item_promo_discount, [p.b]: r.lines[1].item_promo_discount, [p.free]: r.lines[2].item_promo_discount };
      break;
    }
    case 'ab_free_a': {
      const r = c5.freeIsA({ n_a: p.n_a, n_b: p.n_b, y: p.y, qty_a: qty(p.a), unit_price_a: price(p.a), qty_b: qty(p.b), unit_price_b: price(p.b) });
      disc = { [p.a]: r.lines[0].item_promo_discount, [p.b]: r.lines[1].item_promo_discount };
      break;
    }
    case 'ab_pct': {
      const r = c6.abPercent({ n_a: p.n_a, n_b: p.n_b, y: p.y, qty_a: qty(p.a), unit_price_a: price(p.a), qty_b: qty(p.b), unit_price_b: price(p.b) });
      disc = { [p.a]: r.lines[0].item_promo_discount, [p.b]: r.lines[1].item_promo_discount };
      break;
    }
    case 'x_pct':
      disc = { [p.product]: c7.xPercent({ x: p.x, y: p.y, qty: qty(p.product), unit_price: price(p.product) }).item_promo_discount };
      break;
    default:
      throw new Error(`unknown promo kind: ${p.kind}`);
  }
  const touched = Object.keys(disc).filter((name) => free.has(name));   // whole lines the promo takes
  const total = Object.values(disc).reduce((t, b) => t + toSatang(b), 0n);
  return { id: p.id, name: p.name, total, disc, touched };
}

export function allocate({ cart: cartLines, promos, staff_choice = [] }) {
  const cart = new Map(cartLines.map((l) => [l.product, l]));
  const free = new Set(cart.keys());
  const applied = [];
  const steps = [];
  let choice = 0;
  for (;;) {
    const cand = promos.filter((p) => !applied.some((a) => a.id === p.id)).map((p) => evaluate(p, cart, free)).filter((c) => c.total > 0n);
    if (cand.length === 0) break;
    const max = cand.reduce((m, c) => (c.total > m ? c.total : m), 0n);
    const top = cand.filter((c) => c.total === max);
    let pick = top[0];
    if (top.length > 1) {
      const want = staff_choice[choice++];
      if (want === undefined) return { needs_staff_choice: top.map((c) => c.name), steps };
      pick = top.find((c) => c.id === want);
      if (!pick) throw new Error(`staff_choice ${want} is not among the tied promos`);
    }
    steps.push({ candidates: Object.fromEntries(cand.map((c) => [c.name, toBaht(c.total)])), tie: top.length > 1 ? top.map((c) => c.name) : null, picked: pick.name, lines_taken: pick.touched });
    applied.push(pick);
    for (const name of pick.touched) free.delete(name);
  }
  const lines = cartLines.map((l) => {
    const by = applied.find((a) => a.touched.includes(l.product));
    const d = by ? toSatang(by.disc[l.product]) : 0n;
    const gross = toSatang(l.unit_price) * BigInt(l.qty);
    return { product: l.product, qty: l.qty, unit_price: l.unit_price, promo: by ? by.name : null, line_gross: toBaht(gross), item_promo_discount: toBaht(d), line_net: toBaht(gross - d) };
  });
  const promo_discount = toBaht(lines.reduce((t, l) => t + toSatang(l.item_promo_discount), 0n));
  return { steps, lines, promo_discount };
}

// Informational: best total over every set of promos whose lines do not overlap (each on the full cart).
function bestDisjoint({ cart: cartLines, promos }) {
  const cart = new Map(cartLines.map((l) => [l.product, l]));
  const all = new Set(cart.keys());
  const ev = promos.map((p) => evaluate(p, cart, all)).filter((c) => c.total > 0n);
  let best = 0n;
  for (let mask = 1; mask < 1 << ev.length; mask++) {
    const used = new Set();
    let sum = 0n, ok = true;
    ev.forEach((c, i) => {
      if (!(mask & (1 << i)) || !ok) return;
      if (c.touched.some((t) => used.has(t))) ok = false;
      c.touched.forEach((t) => used.add(t));
      sum += c.total;
    });
    if (ok && sum > best) best = sum;
  }
  return toBaht(best);
}

const ORANGE = '45.00';      // ส้มสายน้ำผึ้ง (EX-talad-001)
const MANGOSTEEN = '120.00'; // มังคุด แพ็ก (EX-talad-003)
const LONGAN = '19.75';      // ลำไย — ราคามีสตางค์ (GD-talad-003)
const S = 'ส้มสายน้ำผึ้ง', M = 'มังคุด แพ็ก', L = 'ลำไย';

// Search: two promos sharing the ลำไย line whose rounded discounts differ by exactly 0.01 baht.
// Two promos on ลำไย alone can never do it — both are round(19.75 × integer), 19–20 satang apart —
// so the rival also covers ส้ม, whose price moves the total by a different step.
const oneSatang = (() => {
  for (let qty = 1; qty <= 6; qty++) for (let rate = 1; rate <= 50; rate++) for (let y = 1; y <= 50; y++) {
    const cart = [{ product: L, qty, unit_price: LONGAN }, { product: S, qty: 1, unit_price: ORANGE }];
    const promos = [
      { id: 'A', name: `ลำไย ลด ${rate}%`, kind: 'pct', product: L, rate },
      { id: 'B', name: `ลำไย 1 + ส้ม 1 ลด ${y}%`, kind: 'ab_pct', a: L, b: S, n_a: 1, n_b: 1, y },
    ];
    const m = new Map(cart.map((l) => [l.product, l])), f = new Set(m.keys());
    const a = evaluate(promos[0], m, f).total, b = evaluate(promos[1], m, f).total;
    if (a > 0n && b - a === 1n) return { cart, promos };
  }
  throw new Error('no ลำไย pair found that differs by exactly 0.01');
})();

// Search: a cart where greedy ends below the best disjoint total — the cost of the owner's choice ①c.
const greedyGap = (() => {
  for (let qs = 1; qs <= 6; qs++) for (let y1 = 5; y1 <= 50; y1 += 5) for (let y2 = 5; y2 <= 50; y2 += 5) for (let y3 = 5; y3 <= 50; y3 += 5) {
    const input = {
      cart: [{ product: S, qty: qs, unit_price: ORANGE }, { product: M, qty: 1, unit_price: MANGOSTEEN }],
      promos: [
        { id: 'P1', name: `ส้ม 1 + มังคุด 1 ลด ${y1}%`, kind: 'ab_pct', a: S, b: M, n_a: 1, n_b: 1, y: y1 },
        { id: 'P2', name: `ซื้อส้ม ${qs} ชิ้น ลด ${y2}%`, kind: 'x_pct', product: S, x: qs, y: y2 },
        { id: 'P3', name: `มังคุด ลด ${y3}%`, kind: 'pct', product: M, rate: y3 },
      ],
    };
    const g = allocate(input);
    if (g.needs_staff_choice) continue;
    if (toSatang(bestDisjoint(input)) > toSatang(g.promo_discount)) return input;
  }
  throw new Error('no cart found where greedy is below the best disjoint total');
})();

const cases = [
  { note: 'boundary ①: ไม่มีโปรเข้าเงื่อนไข → ไม่ลด',
    input: { cart: [{ product: S, qty: 2, unit_price: ORANGE }], promos: [] } },
  { note: 'boundary ②: เข้าโปรเดียว → ใช้โปรนั้น (ตรงกับ GD-talad-007 แถว 1)',
    input: { cart: [{ product: S, qty: 7, unit_price: ORANGE }], promos: [{ id: 'A', name: 'ซื้อส้ม 3 ชิ้น ลด 10%', kind: 'x_pct', product: S, x: 3, y: 10 }] } },
  { note: 'ordinary: สองโปรบนบรรทัดเดียวกัน → ใช้อันที่ลดมากกว่า',
    input: { cart: [{ product: S, qty: 3, unit_price: ORANGE }], promos: [
      { id: 'A', name: 'ส้ม ลด 10%', kind: 'pct', product: S, rate: 10 },
      { id: 'B', name: 'ซื้อส้ม 3 ชิ้น ลด 20%', kind: 'x_pct', product: S, x: 3, y: 20 }] } },
  { note: 'boundary ③: โปรที่ลด 0 (ยังไม่ครบชุด · y = 0) ไม่ถูกเลือกและไม่กินบรรทัด → มังคุดยังเข้าโปรอื่นได้',
    input: { cart: [{ product: S, qty: 1, unit_price: ORANGE }, { product: M, qty: 1, unit_price: MANGOSTEEN }], promos: [
      { id: 'A', name: 'ส้ม 2 + มังคุด 1 ลด 10%', kind: 'ab_pct', a: S, b: M, n_a: 2, n_b: 1, y: 10 },
      { id: 'C', name: 'ส้ม ลด 0%', kind: 'pct', product: S, rate: 0 },
      { id: 'B', name: 'มังคุด ลด 5%', kind: 'pct', product: M, rate: 5 }] } },
  { note: 'boundary ④: ลดเท่ากันที่อันดับสูงสุด → ระบบต้องให้พนักงานเลือก (ยังไม่ได้เลือก)',
    input: { cart: [{ product: S, qty: 3, unit_price: ORANGE }], promos: [
      { id: 'A', name: 'ส้ม ลด 10%', kind: 'pct', product: S, rate: 10 },
      { id: 'B', name: 'ซื้อส้ม 3 ชิ้น ลด 10%', kind: 'x_pct', product: S, x: 3, y: 10 }] } },
  { note: 'boundary ④: ลดเท่ากัน → พนักงานเลือก "ส้ม ลด 10%"',
    input: { cart: [{ product: S, qty: 3, unit_price: ORANGE }], staff_choice: ['A'], promos: [
      { id: 'A', name: 'ส้ม ลด 10%', kind: 'pct', product: S, rate: 10 },
      { id: 'B', name: 'ซื้อส้ม 3 ชิ้น ลด 10%', kind: 'x_pct', product: S, x: 3, y: 10 }] } },
  { note: 'boundary ④: ลดเท่ากัน → พนักงานเลือก "ซื้อส้ม 3 ชิ้น ลด 10%" (ยอดเท่ากัน ชื่อโปรต่างกัน)',
    input: { cart: [{ product: S, qty: 3, unit_price: ORANGE }], staff_choice: ['B'], promos: [
      { id: 'A', name: 'ส้ม ลด 10%', kind: 'pct', product: S, rate: 10 },
      { id: 'B', name: 'ซื้อส้ม 3 ชิ้น ลด 10%', kind: 'x_pct', product: S, x: 3, y: 10 }] } },
  { note: 'boundary ⑤: ต่างกัน 0.01 ก็นับว่ามากกว่า (ค้นด้วยสคริปต์)', input: oneSatang },
  { note: 'boundary ⑥ / ตัวอย่างของเจ้าของงาน ①c ①d: ส้ม 4 มังคุด 1 — โปรคู่กับโปรส้ม แย่งบรรทัดส้ม · ได้ไปทั้งบรรทัด ชิ้นนอกชุดคิดราคาปกติ (SRC-040 L3–4)',
    input: { cart: [{ product: S, qty: 4, unit_price: ORANGE }, { product: M, qty: 1, unit_price: MANGOSTEEN }], promos: [
      { id: 'P1', name: 'ส้ม 1 + มังคุด 1 ลด 10%', kind: 'ab_pct', a: S, b: M, n_a: 1, n_b: 1, y: 10 },
      { id: 'P2', name: 'ซื้อส้ม 3 ชิ้น ลด 20%', kind: 'x_pct', product: S, x: 3, y: 20 }] } },
  { note: 'boundary ⑥: โปรแถมได้ทั้งบรรทัดที่ซื้อและบรรทัดของแถม → โปรส้มหลุดเพราะบรรทัดส้มถูกใช้แล้ว',
    input: { cart: [{ product: S, qty: 2, unit_price: ORANGE }, { product: M, qty: 1, unit_price: MANGOSTEEN }], promos: [
      { id: 'A', name: 'ซื้อส้ม 2 แถมมังคุด 1', kind: 'xy_diff', buy: S, free: M, x: 2, y: 1 },
      { id: 'B', name: 'มังคุด ลด 10%', kind: 'pct', product: M, rate: 10 },
      { id: 'C', name: 'ซื้อส้ม 2 ชิ้น ลด 10%', kind: 'x_pct', product: S, x: 2, y: 10 }] } },
  { note: 'boundary ⑦ (เทียบ): ตะกร้าและโปรเดียวกับแถวถัดไป แต่ส้มราคา 45.00 — ผลนี้คือที่จะได้ถ้าระบบใช้ราคาตอนหยิบ',
    input: { cart: [{ product: S, qty: 3, unit_price: ORANGE }, { product: M, qty: 1, unit_price: MANGOSTEEN }], promos: [
      { id: 'A', name: 'ส้ม ลด 10%', kind: 'pct', product: S, rate: 10 },
      { id: 'B', name: 'ส้ม 1 + มังคุด 1 ลด 10%', kind: 'ab_pct', a: S, b: M, n_a: 1, n_b: 1, y: 10 }] } },
  { note: 'boundary ⑦: ราคา ณ ตอนกดชำระ — ส้มตอนหยิบ 45.00 แก้เป็น 70.00 ก่อนชำระ → เลือกโปรจากราคา 70.00 (price_at_add ไม่ถูกใช้) · เทียบกับแถวก่อนหน้า',
    input: { cart: [{ product: S, qty: 3, price_at_add: ORANGE, unit_price: '70.00' }, { product: M, qty: 1, unit_price: MANGOSTEEN }], promos: [
      { id: 'A', name: 'ส้ม ลด 10%', kind: 'pct', product: S, rate: 10 },
      { id: 'B', name: 'ส้ม 1 + มังคุด 1 ลด 10%', kind: 'ab_pct', a: S, b: M, n_a: 1, n_b: 1, y: 10 }] } },
  { note: 'ข้อมูลประกอบ ①c: ตะกร้าที่ไล่ทีละโปรได้ส่วนลดรวมน้อยกว่าชุดโปรที่ดีที่สุด (ค้นด้วยสคริปต์) — เจ้าของงานเลือกแบบนี้เอง', input: greedyGap },
  { note: 'after: ส่วนลดทั้งบิลคิดทีหลังจากยอดหลังหักโปรระดับสินค้า (CALC-talad-001 · ทั้งบิล 5%) — ตะกร้าเดียวกับแถว ⑥ ของเจ้าของงาน',
    input: { bill_rate: 5, cart: [{ product: S, qty: 4, unit_price: ORANGE }, { product: M, qty: 1, unit_price: MANGOSTEEN }], promos: [
      { id: 'P1', name: 'ส้ม 1 + มังคุด 1 ลด 10%', kind: 'ab_pct', a: S, b: M, n_a: 1, n_b: 1, y: 10 },
      { id: 'P2', name: 'ซื้อส้ม 3 ชิ้น ลด 20%', kind: 'x_pct', product: S, x: 3, y: 20 }] } },
];

export const rows = cases.map((c) => {
  const expected = allocate(c.input);
  if (!expected.needs_staff_choice && c.input.bill_rate !== undefined) {
    expected.bill = c1.net({ items: expected.lines.map((l) => ({ unit_price: l.unit_price, qty: l.qty, item_promo_discount: l.item_promo_discount })), bill_rate: c.input.bill_rate, member_rate: 0 });
  }
  const note = expected.needs_staff_choice ? c.note : `${c.note} · [ข้อมูลประกอบ] ชุดโปรที่ไม่ทับกันซึ่งลดได้มากสุด = ${bestDisjoint(c.input)}`;
  return { input: c.input, expected, note };
});
if (import.meta.url === pathToFileURL(process.argv[1]).href) console.log(JSON.stringify(rows, null, 2));
