---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-028@v1
description: line_net[i] = unit_price[i] × qty[i] − item_promo_discount[i]  ;  subtotal = Σ line_net[i]  ;  bill_discount = round(subtotal × bill_rate / 100)  ;  after_bill = subtotal − bill_discount  ;  member_discount = round(after_bill × member_rate / 100)  ;  net = after_bill − member_discount   (ยอดรายการหลังโปรระดับสินค้า → ส่วนลดทั้งบิล → ส่วนลดสมาชิก คิดต่อกัน ไม่บวก % กัน)
resource: ../rules/BR-talad-028@v1.md
tags: [talad, calculation]
id: CALC-talad-001@v1
status: draft
constrains: BR-talad-028@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-001]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:478683a076a967329e9dbab127ef7bcac12654286a7be67805911899b98bccdd
---

# CALC-talad-001@v1

## สูตร

```
line_net[i] = unit_price[i] × qty[i] − item_promo_discount[i]  ;  subtotal = Σ line_net[i]  ;  bill_discount = round(subtotal × bill_rate / 100)  ;  after_bill = subtotal − bill_discount  ;  member_discount = round(after_bill × member_rate / 100)  ;  net = after_bill − member_discount   (ยอดรายการหลังโปรระดับสินค้า → ส่วนลดทั้งบิล → ส่วนลดสมาชิก คิดต่อกัน ไม่บวก % กัน)
```

ผูกกับกฎ [BR-talad-028@v1](../rules/BR-talad-028@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `unit_price[i]` | money(2) | ราคาต่อหน่วยของรายการ i ณ ตอนกดชำระ (BR-talad-038) |
| `qty[i]` | int | จำนวนของรายการ i — ขายเป็นหน่วยนับเท่านั้น (SRC-002) |
| `item_promo_discount[i]` | money(2) | ส่วนลดโปรระดับสินค้าของรายการ i — โปรเดียวที่ลดมากสุด (BR-talad-029) · ปัดแล้วที่ 0.01 · มาจากสูตรของโปรแต่ละแบบ ไม่ใช่สัญญานี้ |
| `bill_rate` | int percent (0–100) | % ส่วนลดทั้งบิล · 0 ถ้าไม่มี |
| `member_rate` | int percent (0–100) | % ส่วนลดสมาชิก · 0 ถ้าไม่ผูกสมาชิก |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ปัดทันทีที่คำนวณส่วนลดแต่ละจุด ที่ 0.01 บาท (2 ตำแหน่ง): item_promo_discount[i] · bill_discount · member_discount — ยอดอื่น (line_net, subtotal, after_bill, net) เป็นผลบวก/ลบของค่าที่ปัดแล้ว จึงไม่มีเศษเกินให้ปัด |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- ไม่มีส่วนลดทั้งบิล (bill_rate = 0) → bill_discount = 0, after_bill = subtotal
- ไม่ผูกสมาชิก (member_rate = 0) → member_discount = 0, net = after_bill
- rate = 100 ได้ → ส่วนลดขั้นนั้นเท่ากับยอดตั้งต้นของขั้น, net = 0 และยังชำระได้เป็นบิลปกติ
- net ≥ 0 เสมอ — rate อยู่ใน 0–100 และคิดต่อกัน ไม่บวก % กัน
- subtotal = 0 (ของแถมล้วน) → bill_discount = member_discount = 0, net = 0

## เลขเฉลย

- [GD-talad-001](../golden/GD-talad-001.md) — 8 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-028@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-001](../questions/DQ-talad-001.md)
- [DQ-talad-002](../questions/DQ-talad-002.md)
- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-001@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
ทศนิยม 2 ตำแหน่งและปัดทุกจุดส่วนลด ตอบคำถามเชิงธุรกิจของ DQ-talad-001/002 แล้ว แต่ DQ ยังเปิดอยู่เพราะปิดได้ผ่าน deferred.mjs จาก desk ของ design เท่านั้น — design ต้องยืนยันให้ตรงกับสัญญานี้ · DQ-talad-003 (decimal(p,s)) ยังเป็นคำถามของ Phase 2
