---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-027@v1
description: discount = round_HALF_UP(base × rate / 100, 0.01)  ;  after = base − discount   — ใช้ทุกจุดที่มีส่วนลด %: โปร % ต่อสินค้า (BR-talad-009/013/014) · ส่วนลดทั้งบิล · ส่วนลดสมาชิก
resource: ../rules/BR-talad-027@v1.md
tags: [talad, calculation]
id: CALC-talad-002@v1
status: draft
constrains: BR-talad-027@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-002]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:5fe387293dd9578004e752e69a3c9fb9b3657080ab896e242f0ed4b12f0ca0e6
---

# CALC-talad-002@v1

## สูตร

```
discount = round_HALF_UP(base × rate / 100, 0.01)  ;  after = base − discount   — ใช้ทุกจุดที่มีส่วนลด %: โปร % ต่อสินค้า (BR-talad-009/013/014) · ส่วนลดทั้งบิล · ส่วนลดสมาชิก
```

ผูกกับกฎ [BR-talad-027@v1](../rules/BR-talad-027@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `base` | money(2) | ยอดฐานของจุดนั้น (ยอดรายการ / ยอดรวม / ยอดหลังส่วนลดทั้งบิล ตาม CALC-talad-001) — ค่าที่ปัดแล้วที่ 0.01 |
| `rate` | int percent (0–100) | % ส่วนลดของจุดนั้น |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ปัดทันทีที่คำนวณ discount ของแต่ละจุด ที่ 0.01 บาท (2 ตำแหน่ง) — ไม่เก็บค่าเต็มไว้ปัดตอนท้าย · after = base − discount ไม่มีเศษเกินให้ปัด |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- เศษครึ่งสตางค์พอดี (x.xx5) → ปัดขึ้น เช่น 9.045 → 9.05 (ไม่ใช่ 9.04 แบบ HALF_EVEN ซึ่งเป็นค่าเริ่มต้นของ .NET)
- เศษต่ำกว่าครึ่งสตางค์ → ปัดลง เช่น 2.9625 → 2.96
- ลงตัว 2 ตำแหน่งอยู่แล้ว → ไม่เปลี่ยน เช่น 4.50
- base = 0 หรือ rate = 0 → discount = 0.00

## เลขเฉลย

- [GD-talad-002](../golden/GD-talad-002.md) — 9 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-027@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-001](../questions/DQ-talad-001.md)
- [DQ-talad-002](../questions/DQ-talad-002.md)
- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-002@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
ต้องตรงกับ CALC-talad-001@v1 ทุกฟิลด์ที่ซ้ำกัน (ยืนยันใน SRC-029 บรรทัด 2) · ทศนิยม 2 ตำแหน่งและปัดทุกจุด ตอบเชิงธุรกิจของ DQ-talad-001/002 ซึ่งถูก raised_by กฎข้อนี้ — DQ ยังเปิดเพราะปิดได้ผ่าน deferred.mjs เท่านั้น · DQ-talad-003 (decimal(p,s)) เป็นของ Phase 2
