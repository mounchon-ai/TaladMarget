---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-010@v1
description: member_rate = has_member ? shop_member_rate (ค่า ณ ตอนกดชำระ) : 0 ; member_discount = round_half_up(after_bill × member_rate / 100, 0.01) ; net = after_bill − member_discount   — member_rate คืออินพุตชื่อเดียวกันของ CALC-talad-001 และ member_discount / net เป็นค่าเดียวกับใน CALC-talad-001 · สัญญานี้เพิ่มเงื่อนไขว่า member_rate มาจากไหน
resource: ../rules/BR-talad-010@v1.md
tags: [talad, calculation]
id: CALC-talad-009@v1
status: draft
constrains: BR-talad-010@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-009]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:c50e0529489aeead0cf3ff40982a78459099bc23cf4daec1580cd4eaf138d13b
---

# CALC-talad-009@v1

## สูตร

```
member_rate = has_member ? shop_member_rate (ค่า ณ ตอนกดชำระ) : 0 ; member_discount = round_half_up(after_bill × member_rate / 100, 0.01) ; net = after_bill − member_discount   — member_rate คืออินพุตชื่อเดียวกันของ CALC-talad-001 และ member_discount / net เป็นค่าเดียวกับใน CALC-talad-001 · สัญญานี้เพิ่มเงื่อนไขว่า member_rate มาจากไหน
```

ผูกกับกฎ [BR-talad-010@v1](../rules/BR-talad-010@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `shop_member_rate` | int percent (0–100) | % ส่วนลดสมาชิกค่าเดียวทั้งร้าน ที่เจ้าของร้านตั้ง · ใช้ค่า ณ ตอนกดชำระ |
| `has_member` | boolean | บิลผูกสมาชิกหรือไม่ |
| `after_bill` | money(2) | ยอดหลังโปรระดับสินค้าและหลังส่วนลดทั้งบิล ตาม CALC-talad-001 |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ปัดครั้งเดียวที่ member_discount ที่ 0.01 บาท ทันทีที่คำนวณ — จุดเดียวกับ CALC-talad-001 |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- บิลไม่ผูกสมาชิก → member_rate = 0 · ไม่ลด แม้ร้านตั้ง % ไว้
- shop_member_rate = 0 → ไม่ลด (ยอดซื้อสะสมยังสะสมตามปกติ — ไม่ใช่ส่วนของสัญญานี้)
- shop_member_rate = 100 → member_discount = after_bill · net = 0 ชำระได้
- แก้ % ระหว่างตะกร้าเปิดอยู่ → ใช้ค่า ณ ตอนกดชำระ
- after_bill = 0 → member_discount = 0
- สมาชิกทุกคนได้ % เท่ากัน
- ส่วนลดมีเศษสตางค์ → ปัด HALF_UP ที่ 0.01

## เลขเฉลย

- [GD-talad-009](../golden/GD-talad-009.md) — 9 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-010@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-001](../questions/DQ-talad-001.md)
- [DQ-talad-002](../questions/DQ-talad-002.md)
- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-009@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
สูตรส่วนลดสมาชิกเป็นสูตรเดียวกับ CALC-talad-001 (constrains BR-talad-028) — สัญญานี้ไม่ได้ตั้งสูตรใหม่ แต่ระบุที่มาของ member_rate และขอบเขตของ BR-talad-010 · ถ้าสูตรใน CALC-talad-001 เปลี่ยน สัญญานี้ต้องเปลี่ยนตามผ่าน /req:change
