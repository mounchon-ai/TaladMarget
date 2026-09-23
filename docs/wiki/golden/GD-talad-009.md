---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-010@v1 · CALC-talad-009@v1
description: 9 แถว · ยืนยันแล้ว
resource: ../rules/BR-talad-010@v1.md
tags: [talad, golden]
id: GD-talad-009
status: validated
proves: [BR-talad-010@v1, CALC-talad-009@v1]
verified_by: เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-010@v1)
verified_at: 2026-09-23T16:40+07:00
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:eb1c34ed19b1e64924fc6302717d8bdda766331f57bb8ee33028472d8302b8b1
---

# GD-talad-009

## สถานะการยืนยัน

✅ **เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-010@v1)** ยืนยันเมื่อ 2026-09-23T16:40+07:00 — ตัวเลขชุดนี้ใช้ยันกับลูกค้าได้

คำนวณโดย `golden/CALC-talad-009@v1.mjs` เมื่อ 2026-09-23T16:35+07:00

## พิสูจน์

- [BR-talad-010@v1](../rules/BR-talad-010@v1.md) — สมาชิกได้ส่วนลดพิเศษเป็น % เดียวกันทั้งร้าน ที่เจ้าของร้านตั้งแยกจากโปรโมชั่นทั่วไป (% เต็ม 0–100) · ไม่มีช่วงเวลา มีผลตลอดจนกว่าจะแก้ · ตั้ง 0% = ไม่มีส่วนลดสมาชิก · ใช้กับบิลที่ผูกสมาชิกเท่านั้น
- [CALC-talad-009@v1](../calculations/CALC-talad-009@v1.md) — member_rate = has_member ? shop_member_rate (ค่า ณ ตอนกดชำระ) : 0 ; member_discount = round_half_up(after_bill × member_rate / 100, 0.01) ; net = after_bill − member_discount   — member_rate คืออินพุตชื่อเดียวกันของ CALC-talad-001 และ member_discount / net เป็นค่าเดียวกับใน CALC-talad-001 · สัญญานี้เพิ่มเงื่อนไขว่า member_rate มาจากไหน

## ตาราง (9 แถว)

| items | has_member | shop_member_rate_at_checkout | rate_at_attach | member | bill_rate | member_rate | subtotal | bill_discount | after_bill | member_discount | net | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| [object Object] | true | 5 | — | — | — | 5 | 100.00 | 0.00 | 100.00 | 5.00 | 95.00 | — |
| [object Object] | false | 5 | — | — | — | 0 | 100.00 | 0.00 | 100.00 | 0.00 | 100.00 | — |
| [object Object] | true | 0 | — | — | — | 0 | 100.00 | 0.00 | 100.00 | 0.00 | 100.00 | — |
| [object Object] | true | 100 | — | — | — | 100 | 100.00 | 0.00 | 100.00 | 100.00 | 0.00 | — |
| [object Object] | true | 10 | 5 | — | — | 10 | 100.00 | 0.00 | 100.00 | 10.00 | 90.00 | — |
| [object Object] | true | 5 | — | — | — | 5 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | — |
| [object Object] | true | 5 | — | สมหมาย รักดี | — | 5 | 100.00 | 0.00 | 100.00 | 5.00 | 95.00 | — |
| [object Object] | true | 5 | — | — | — | 5 | 39.50 | 0.00 | 39.50 | 1.98 | 37.52 | — |
| [object Object],[object Object] | true | 5 | — | — | 10 | 5 | 210.00 | 21.00 | 189.00 | 9.45 | 179.55 | — |

## หมายเหตุ
ไม่มี sample_data — แถวมาจาก boundary_behavior ทั้ง 7 ข้อ + กรณีคิดต่อจากส่วนลดทั้งบิล · member_discount / net ได้จากการเรียก net() ของ CALC-talad-001 ไม่ได้คิดซ้ำ · ผลตรงกับตัวอย่างที่มีอยู่: แถว 1 และ 7 = EX-talad-016 / 094 (95 บาท) · แถว 3 = EX-talad-095 (100 บาท) · แถว 5 = EX-talad-024 (90 บาท) · แถว 8 ค้นด้วยสคริปต์ให้ส่วนลดตกครึ่งสตางค์พอดี
