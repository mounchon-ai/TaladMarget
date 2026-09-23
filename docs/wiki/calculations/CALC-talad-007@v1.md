---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-014@v1
description: sets = floor(qty / x) ; disc = round_half_up(sets × x × unit_price × y / 100, 0.01)   — disc ลงเป็น item_promo_discount ของบรรทัดสินค้านั้น (CALC-talad-001) · ชิ้นที่ไม่ครบชุดไม่ได้ลด · disc เป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
resource: ../rules/BR-talad-014@v1.md
tags: [talad, calculation]
id: CALC-talad-007@v1
status: draft
constrains: BR-talad-014@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-007]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:84326f3195d9404707ea50f03c73e2d47fad403e37182c21a7fb2988b1aba745
---

# CALC-talad-007@v1

## สูตร

```
sets = floor(qty / x) ; disc = round_half_up(sets × x × unit_price × y / 100, 0.01)   — disc ลงเป็น item_promo_discount ของบรรทัดสินค้านั้น (CALC-talad-001) · ชิ้นที่ไม่ครบชุดไม่ได้ลด · disc เป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
```

ผูกกับกฎ [BR-talad-014@v1](../rules/BR-talad-014@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `x` | int (≥ 1) | จำนวนชิ้นต่อชุด ที่เจ้าของร้านตั้งในโปร |
| `y` | int (0–100) | % ส่วนลด เต็ม 0–100 (เหมือน CALC-talad-003) |
| `qty` | int | จำนวนสินค้านั้นในบรรทัดของตะกร้า |
| `unit_price` | money(2) | ราคาต่อหน่วย ณ ตอนกดชำระ (BR-talad-038) |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ปัดครั้งเดียวต่อบรรทัด — disc ปัดที่ 0.01 บาท ทันทีที่คำนวณ (ไม่ปัดต่อชิ้น ไม่ปัดต่อชุด) — แบบเดียวกับ CALC-talad-003 และ CALC-talad-006 |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- qty < x → sets = 0 · ไม่ลด
- qty = x พอดี → sets = 1 · ลด 1 ชุด
- ชิ้นที่เกินชุดครบ (qty mod x) คิดราคาปกติ
- ราคาใช้ค่า ณ ตอนกดชำระ (BR-talad-038)
- y = 0 → ไม่ลด · y = 100 → ชิ้นในชุดฟรี ยอดบรรทัดไม่ติดลบ
- x = 1 → ทุกชิ้นอยู่ในชุด ลดทุกชิ้น

## เลขเฉลย

- [GD-talad-007](../golden/GD-talad-007.md) — 11 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-014@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-001](../questions/DQ-talad-001.md)
- [DQ-talad-002](../questions/DQ-talad-002.md)
- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-007@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
disc เป็นอินพุต item_promo_discount[i] ของ CALC-talad-001 · การนับชุดเหมือน CALC-talad-006 แต่มีสินค้าฝั่งเดียว · จุดปัดและชนิด % เหมือน CALC-talad-003 · ชนิดตัวเลขตรงกับ CALC-talad-001 ถึง 006
