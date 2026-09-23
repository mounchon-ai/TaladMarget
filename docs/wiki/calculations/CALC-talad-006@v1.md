---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-013@v1
description: sets = floor(min(qty_a / n_a, qty_b / n_b)) ; disc_a = round_half_up(sets × n_a × unit_price_a × y / 100, 0.01) ; disc_b = round_half_up(sets × n_b × unit_price_b × y / 100, 0.01)   — disc_a / disc_b ลงเป็น item_promo_discount ของบรรทัด a / b (CALC-talad-001) · ชิ้นที่ไม่อยู่ในชุดครบคู่ไม่ได้ลด · ผลรวม disc_a + disc_b เป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
resource: ../rules/BR-talad-013@v1.md
tags: [talad, calculation]
id: CALC-talad-006@v1
status: draft
constrains: BR-talad-013@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-006]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:3a57f6604a952d3973cdd1d20afb0d8969c73eabb26d280f4e5f3704be4a1288
---

# CALC-talad-006@v1

## สูตร

```
sets = floor(min(qty_a / n_a, qty_b / n_b)) ; disc_a = round_half_up(sets × n_a × unit_price_a × y / 100, 0.01) ; disc_b = round_half_up(sets × n_b × unit_price_b × y / 100, 0.01)   — disc_a / disc_b ลงเป็น item_promo_discount ของบรรทัด a / b (CALC-talad-001) · ชิ้นที่ไม่อยู่ในชุดครบคู่ไม่ได้ลด · ผลรวม disc_a + disc_b เป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
```

ผูกกับกฎ [BR-talad-013@v1](../rules/BR-talad-013@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `n_a` | int (≥ 1) | จำนวนสินค้า a ที่ต้องซื้อต่อชุด |
| `n_b` | int (≥ 1) | จำนวนสินค้า b ที่ต้องซื้อต่อชุด |
| `y` | int (0–100) | % ส่วนลด เต็ม 0–100 (เหมือน CALC-talad-003) |
| `qty_a` | int | จำนวนสินค้า a ในตะกร้า |
| `qty_b` | int | จำนวนสินค้า b ในตะกร้า |
| `unit_price_a` | money(2) | ราคาต่อหน่วยของ a ณ ตอนกดชำระ (BR-talad-038) |
| `unit_price_b` | money(2) | ราคาต่อหน่วยของ b ณ ตอนกดชำระ (BR-talad-038) |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ปัดแยกต่อบรรทัด — disc_a ปัดครั้งเดียว และ disc_b ปัดครั้งเดียว ที่ 0.01 บาท ทันทีที่คำนวณ (ไม่ปัดต่อชิ้น ไม่ปัดต่อชุด) — แบบเดียวกับ CALC-talad-003 |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- qty_a < n_a หรือ qty_b < n_b → sets = 0 · ไม่ลด
- ฝั่งหนึ่งมีเกิน → sets นับตามฝั่งที่น้อย (min) · ชิ้นที่ไม่อยู่ในชุดครบคู่คิดราคาปกติ
- ราคาใช้ค่า ณ ตอนกดชำระ (BR-talad-038)
- y = 0 → ไม่ลด · y = 100 → ชิ้นในชุดฟรี ยอดบรรทัดไม่ติดลบ
- ส่วนลดที่มีเศษสตางค์ → ปัด HALF_UP ที่ 0.01 ต่อบรรทัด a และ b แยกกัน

## เลขเฉลย

- [GD-talad-006](../golden/GD-talad-006.md) — 14 แถว · 🔴 ยังไม่มีใครเซ็น

## คำถามที่ผูกอยู่

- [DQ-talad-001](../questions/DQ-talad-001.md)
- [DQ-talad-002](../questions/DQ-talad-002.md)
- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-006@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
disc_a / disc_b เป็นอินพุต item_promo_discount[i] ของ CALC-talad-001 · การนับชุดเหมือน CALC-talad-005 · จุดปัดและชนิด % เหมือน CALC-talad-003 · ชนิดตัวเลขตรงกับ CALC-talad-001 ถึง 005
