---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-009@v1 · CALC-talad-003@v1
description: 12 แถว · ยืนยันแล้ว
resource: ../rules/BR-talad-009@v1.md
tags: [talad, golden]
id: GD-talad-003
status: validated
proves: [BR-talad-009@v1, CALC-talad-003@v1]
verified_by: เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-009@v1)
verified_at: 2026-09-23T12:25+07:00
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:c48005990e392392b83367bc39fb84299c1c415b14dc7df75bc5c86d0a5090c3
---

# GD-talad-003

## สถานะการยืนยัน

✅ **เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-009@v1)** ยืนยันเมื่อ 2026-09-23T12:25+07:00 — ตัวเลขชุดนี้ใช้ยันกับลูกค้าได้

คำนวณโดย `golden/CALC-talad-003@v1.mjs` เมื่อ 2026-09-23T12:25+07:00

## พิสูจน์

- [BR-talad-009@v1](../rules/BR-talad-009@v1.md) — ตั้งส่วนลดเป็น % ได้ทั้งแบบต่อสินค้า และแบบทั้งบิล · ส่วนลด % ต่อสินค้าคิดจากยอดทั้งบรรทัด (ราคา × จำนวน) · ส่วนลด % ทั้งบิลมียอดขั้นต่ำที่เจ้าของร้านตั้ง เทียบกับยอดหลังโปรระดับสินค้า (ยอดเท่ากับขั้นต่ำพอดีได้ลด) · ถ้ามีโปรทั้งบิลที่ครบขั้นต่ำหลายอัน ใช้อันที่ลดมากสุดอันเดียว
- [CALC-talad-003@v1](../calculations/CALC-talad-003@v1.md) — ต่อสินค้า: item_pct_discount[i] = round_HALF_UP(unit_price[i] × qty[i] × item_rate[i] / 100, 0.01)   ·   ทั้งบิล: eligible = { p ∈ bill_promos \| subtotal ≥ p.min_subtotal } ; bill_rate = max(p.rate for p in eligible) หรือ 0 ถ้า eligible ว่าง ; bill_discount = round_HALF_UP(subtotal × bill_rate / 100, 0.01)   — subtotal คือยอดหลังโปรระดับสินค้าตาม CALC-talad-001

## ตาราง (12 แถว)

| kind | unit_price | qty | item_rate | items | bill_promos | line_gross | item_pct_discount | line_net | subtotal | eligible_promos | bill_rate | bill_discount | after_bill | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| item | 19.75 | 3 | 7 | — | — | 59.25 | 4.15 | 55.10 | — | — | — | — | — | — |
| item | 45.00 | 2 | 10 | — | — | 90.00 | 9.00 | 81.00 | — | — | — | — | — | — |
| item | 45.00 | 2 | 0 | — | — | 90.00 | 0.00 | 90.00 | — | — | — | — | — | — |
| item | 45.00 | 2 | 100 | — | — | 90.00 | 90.00 | 0.00 | — | — | — | — | — | — |
| bill | — | — | — | [object Object] | [object Object] | — | — | — | 500.00 | 5% ขั้นต่ำ 500.00 | 5 | 25.00 | 475.00 | — |
| bill | — | — | — | [object Object] | [object Object] | — | — | — | 499.99 |  | 0 | 0.00 | 499.99 | — |
| bill | — | — | — | [object Object] | [object Object] | — | — | — | 468.00 |  | 0 | 0.00 | 468.00 | — |
| bill | — | — | — | [object Object] | [object Object],[object Object] | — | — | — | 700.00 | 5% ขั้นต่ำ 500.00 | 5 | 35.00 | 665.00 | — |
| bill | — | — | — | [object Object] | [object Object],[object Object] | — | — | — | 1000.00 | 5% ขั้นต่ำ 500.00,10% ขั้นต่ำ 1000.00 | 10 | 100.00 | 900.00 | — |
| bill | — | — | — | [object Object] | [object Object],[object Object] | — | — | — | 300.00 |  | 0 | 0.00 | 300.00 | — |
| bill | — | — | — | [object Object] | [object Object] | — | — | — | 45.00 | 100% ขั้นต่ำ 0.00 | 100 | 45.00 | 0.00 | — |
| bill | — | — | — | [object Object],[object Object] | [object Object] | — | — | — | 555.10 | 5% ขั้นต่ำ 500.00 | 5 | 27.76 | 527.34 | — |

## หมายเหตุ
ไม่มี sample_data — แถวมาจาก boundary_behavior ทั้ง 4 ข้อ + กรณีปกติ · note ของแถว item มีผลแบบปัดต่อชิ้นเป็นข้อมูลประกอบ ไม่ใช่คำตอบ · แถว 2 ตรงกับส่วนลดโปร 9 บาทใน EX-talad-065 / GD-talad-001 แถว 3
