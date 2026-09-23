---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-012@v1
description: ของแถมเป็นสินค้าอื่น (y ≠ a, b): sets = floor(min(qty_a / n_a, qty_b / n_b)) ; entitled = sets × y ; free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free   ·   ของแถมเป็น a (ถ้าเป็น b สลับ a กับ b): sets = floor(min(qty_a / (n_a + y), qty_b / n_b)) ; free_qty = sets × y ; free_discount = free_qty × unit_price_a   — free_discount ลงเป็น item_promo_discount ของบรรทัดของแถม (CALC-talad-001) และเป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
resource: ../rules/BR-talad-012@v1.md
tags: [talad, calculation]
id: CALC-talad-005@v1
status: draft
constrains: BR-talad-012@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-005]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:0323b8d56e5c362a50af897a2358a9a7d5b7b1226c1d8d76580bc256c21e93dc
---

# CALC-talad-005@v1

## สูตร

```
ของแถมเป็นสินค้าอื่น (y ≠ a, b): sets = floor(min(qty_a / n_a, qty_b / n_b)) ; entitled = sets × y ; free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free   ·   ของแถมเป็น a (ถ้าเป็น b สลับ a กับ b): sets = floor(min(qty_a / (n_a + y), qty_b / n_b)) ; free_qty = sets × y ; free_discount = free_qty × unit_price_a   — free_discount ลงเป็น item_promo_discount ของบรรทัดของแถม (CALC-talad-001) และเป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
```

ผูกกับกฎ [BR-talad-012@v1](../rules/BR-talad-012@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `n_a` | int (≥ 1) | จำนวนสินค้า a ที่ต้องซื้อต่อชุด |
| `n_b` | int (≥ 1) | จำนวนสินค้า b ที่ต้องซื้อต่อชุด |
| `y` | int (≥ 1) | จำนวนของแถมต่อชุด |
| `qty_a` | int | จำนวนสินค้า a ในตะกร้า (รวมชิ้นที่เป็นของแถม ถ้าของแถมเป็น a) |
| `qty_b` | int | จำนวนสินค้า b ในตะกร้า (รวมชิ้นที่เป็นของแถม ถ้าของแถมเป็น b) |
| `qty_free_in_cart` | int | จำนวนสินค้าของแถมที่พนักงานหยิบใส่ตะกร้า (กรณีของแถมเป็นสินค้าอื่น) |
| `unit_price_free` | money(2) | ราคาต่อหน่วยของสินค้าของแถม ณ ตอนกดชำระ (BR-talad-038) — กรณีของแถมเป็นสินค้าอื่น |
| `unit_price_a` | money(2) | ราคาต่อหน่วยของสินค้า a ณ ตอนกดชำระ (BR-talad-038) — กรณีของแถมเป็น a (หรือ unit_price_b เมื่อของแถมเป็น b) |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ไม่มีจุดปัด — free_discount = จำนวนเต็ม × money(2) ลงตัวเสมอ · HALF_UP ระบุไว้ให้ตรงกับสัญญาอื่น |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- qty_a < n_a หรือ qty_b < n_b → sets = 0 · ของแถมที่หยิบคิดราคาปกติ
- ฝั่งหนึ่งมีเกิน (เช่น a พอ 3 ชุด b พอ 1 ชุด) → sets นับตามฝั่งที่น้อย (min) · ชิ้นที่เกินคิดราคาปกติ
- qty_free_in_cart > entitled → free เฉพาะ entitled ชิ้น ส่วนเกินคิดราคาปกติ · qty_free_in_cart < entitled → free เท่าที่หยิบ ระบบไม่เพิ่มของแถมให้เอง
- มูลค่าของแถมใช้ราคา ณ ตอนกดชำระ (BR-talad-038)
- ของแถมหมดสต็อก → หยิบไม่ได้ ระบบแสดงข้อความของ BR-talad-007 "<ชื่อสินค้า> คงเหลือไม่พอ (เหลือ <จำนวน>)" · a และ b คิดราคาปกติ ชำระได้
- ของแถมเป็น a: qty_a < n_a + y → sets = 0 ไม่ได้แถม จ่ายเต็ม (เช่น ซื้อ ส้ม 2 + มังคุด 1 แถม ส้ม 1 หยิบส้ม 2 มังคุด 1 = จ่ายเต็ม)

## เลขเฉลย

- [GD-talad-005](../golden/GD-talad-005.md) — 18 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-012@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-005@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
free_discount เป็นอินพุต item_promo_discount[i] ของ CALC-talad-001 · ของแถมตัดสต็อกตาม BR-talad-007 · ชนิดตัวเลขตรงกับ CALC-talad-001/002/003/004 · กลไกของแถมเหมือน CALC-talad-004 ทุกข้อ
