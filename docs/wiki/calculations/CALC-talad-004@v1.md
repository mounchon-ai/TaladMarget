---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-011@v1
description: คนละสินค้า (buy ≠ free): entitled = floor(qty_buy / x) × y ; free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free   ·   สินค้าเดียวกัน (buy = free): free_qty = floor(qty_in_cart / (x + y)) × y ; free_discount = free_qty × unit_price   — free_discount ลงเป็น item_promo_discount ของบรรทัดของแถม (CALC-talad-001) และเป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
resource: ../rules/BR-talad-011@v1.md
tags: [talad, calculation]
id: CALC-talad-004@v1
status: draft
constrains: BR-talad-011@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-004]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:eb96cc2dfa90442f95c41659842a464d3794a644350558b20fd892c5ebca9f34
---

# CALC-talad-004@v1

## สูตร

```
คนละสินค้า (buy ≠ free): entitled = floor(qty_buy / x) × y ; free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free   ·   สินค้าเดียวกัน (buy = free): free_qty = floor(qty_in_cart / (x + y)) × y ; free_discount = free_qty × unit_price   — free_discount ลงเป็น item_promo_discount ของบรรทัดของแถม (CALC-talad-001) และเป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า
```

ผูกกับกฎ [BR-talad-011@v1](../rules/BR-talad-011@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `x` | int (≥ 1) | จำนวนที่ต้องซื้อต่อชุด |
| `y` | int (≥ 1) | จำนวนของแถมต่อชุด |
| `qty_buy` | int | จำนวนสินค้าที่ซื้อในตะกร้า (กรณีคนละสินค้า) |
| `qty_free_in_cart` | int | จำนวนสินค้าของแถมที่พนักงานหยิบใส่ตะกร้า (กรณีคนละสินค้า) |
| `qty_in_cart` | int | จำนวนรวมในบรรทัด (กรณีสินค้าเดียวกัน) |
| `unit_price_free` | money(2) | ราคาต่อหน่วยของสินค้าของแถม ณ ตอนกดชำระ (BR-talad-038) |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ไม่มีจุดปัด — free_discount = จำนวนเต็ม × money(2) ลงตัวเสมอ · HALF_UP ระบุไว้ให้ตรงกับสัญญาอื่น |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- qty_buy < x → entitled = 0 · ของแถมที่หยิบคิดราคาปกติ
- qty_free_in_cart > entitled → free เฉพาะ entitled ชิ้น ส่วนเกินคิดราคาปกติ
- qty_free_in_cart < entitled → free เท่าที่หยิบ ระบบไม่เพิ่มของแถมให้เอง
- มูลค่าของแถมใช้ราคา ณ ตอนกดชำระ (BR-talad-038)
- ของแถมหมดสต็อก → หยิบไม่ได้ ระบบแสดงข้อความของ BR-talad-007 "<ชื่อสินค้า> คงเหลือไม่พอ (เหลือ <จำนวน>)" · สินค้าที่ซื้อคิดราคาปกติ ชำระได้
- สินค้าเดียวกัน: qty_in_cart < x + y → free_qty = 0 (เช่น ซื้อ 2 แถม 1 หยิบ 2 ชิ้น = จ่ายเต็ม 2 ชิ้น)

## เลขเฉลย

- [GD-talad-004](../golden/GD-talad-004.md) — 15 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-011@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-004@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
free_discount เป็นอินพุต item_promo_discount[i] ของ CALC-talad-001 · ของแถมตัดสต็อกตาม BR-talad-007 · ชนิดตัวเลขตรงกับ CALC-talad-001/002/003
