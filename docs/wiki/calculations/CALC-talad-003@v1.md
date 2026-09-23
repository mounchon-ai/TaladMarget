---
type: Calculation Contract
title: สัญญาการคำนวณของ BR-talad-009@v1
description: ต่อสินค้า: item_pct_discount[i] = round_HALF_UP(unit_price[i] × qty[i] × item_rate[i] / 100, 0.01)   ·   ทั้งบิล: eligible = { p ∈ bill_promos | subtotal ≥ p.min_subtotal } ; bill_rate = max(p.rate for p in eligible) หรือ 0 ถ้า eligible ว่าง ; bill_discount = round_HALF_UP(subtotal × bill_rate / 100, 0.01)   — subtotal คือยอดหลังโปรระดับสินค้าตาม CALC-talad-001
resource: ../rules/BR-talad-009@v1.md
tags: [talad, calculation]
id: CALC-talad-003@v1
status: draft
constrains: BR-talad-009@v1
is_current: true
numeric_type: decimal
rounding_mode: HALF_UP
golden: [GD-talad-003]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:e6af15a66564b6b28f1303ba424eb74ecd8a153e4c2c0e3937f3b9084626c4f3
---

# CALC-talad-003@v1

## สูตร

```
ต่อสินค้า: item_pct_discount[i] = round_HALF_UP(unit_price[i] × qty[i] × item_rate[i] / 100, 0.01)   ·   ทั้งบิล: eligible = { p ∈ bill_promos | subtotal ≥ p.min_subtotal } ; bill_rate = max(p.rate for p in eligible) หรือ 0 ถ้า eligible ว่าง ; bill_discount = round_HALF_UP(subtotal × bill_rate / 100, 0.01)   — subtotal คือยอดหลังโปรระดับสินค้าตาม CALC-talad-001
```

ผูกกับกฎ [BR-talad-009@v1](../rules/BR-talad-009@v1.md)

## ตัวแปรเข้า

| ชื่อ | ชนิด | ความหมาย |
|---|---|---|
| `unit_price[i]` | money(2) | ราคาต่อหน่วย ณ ตอนกดชำระ |
| `qty[i]` | int | จำนวน (หน่วยนับ) |
| `item_rate[i]` | int percent (0–100) | % ของโปรต่อสินค้าที่ถูกเลือกให้รายการนี้ (โปรเดียวที่ลดมากสุดตาม BR-talad-029) |
| `subtotal` | money(2) | ยอดรวมหลังโปรระดับสินค้า (CALC-talad-001) |
| `bill_promos[]` | list of { rate: int percent (0–100), min_subtotal: money(2) } | โปรส่วนลดทั้งบิลที่อยู่ในช่วงวันที่ ณ ตอนกดชำระ (BR-talad-015, BR-talad-038) |

## การปัดเศษ — ส่วนที่ทำให้ตัวเลขต่างกันได้ทั้งที่สูตรเหมือนกัน

| เรื่อง | ค่า |
|---|---|
| ชนิดตัวเลข | decimal |
| วิธีปัด | HALF_UP |
| ปัดตรงไหน | ปัดครั้งเดียวต่อบรรทัดที่ item_pct_discount[i] (ไม่ปัดต่อชิ้น) และปัดที่ bill_discount — ทั้งสองจุดที่ 0.01 บาท ทันทีที่คำนวณ |
| เศษที่เหลือ | — |

## พฤติกรรมที่ขอบ

- subtotal = min_subtotal พอดี (เช่น 500.00) → ได้ส่วนลดทั้งบิล · 499.99 → ไม่ได้
- min_subtotal เทียบกับยอดหลังโปรระดับสินค้า — ยอดเต็มครบแต่หลังโปรสินค้าไม่ครบ → ไม่ได้ส่วนลดทั้งบิล · ส่วนลดสมาชิกไม่นับ (คิดทีหลัง)
- หลายโปรทั้งบิล → เลือก rate สูงสุดเฉพาะจากโปรที่ครบขั้นต่ำ · rate เท่ากัน → ผลเท่ากัน ไม่ต้องเลือก · ไม่มีโปรที่ครบ → bill_rate = 0
- rate = 0 → ส่วนลด 0.00 · rate = 100 → ส่วนลดเท่ากับยอดฐาน ยอดไม่ติดลบ

## เลขเฉลย

- [GD-talad-003](../golden/GD-talad-003.md) — 12 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-009@v1)

## คำถามที่ผูกอยู่

- [DQ-talad-001](../questions/DQ-talad-001.md)
- [DQ-talad-002](../questions/DQ-talad-002.md)
- [DQ-talad-003](../questions/DQ-talad-003.md)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล |
|---|---|---|
| **CALC-talad-003@v1** (หน้านี้) ✅ | — | ตั้งต้น |

## หมายเหตุ
ฟิลด์ชนิดตัวเลข/วิธีปัด/จุดปัด ต้องตรงกับ CALC-talad-001@v1 และ CALC-talad-002@v1 (SRC-031 บรรทัด 4) · bill_discount ที่นี่คือค่าเดียวกับ bill_discount ใน CALC-talad-001 — สัญญานี้เพิ่มเงื่อนไขว่า bill_rate มาจากไหน
