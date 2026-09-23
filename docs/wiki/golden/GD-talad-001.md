---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-028@v1 · CALC-talad-001@v1
description: 8 แถว · ยืนยันแล้ว
resource: ../rules/BR-talad-028@v1.md
tags: [talad, golden]
id: GD-talad-001
status: validated
proves: [BR-talad-028@v1, CALC-talad-001@v1]
verified_by: เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-028@v1)
verified_at: 2026-09-23T12:08+07:00
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:6cfd3b2853fc43ad1ef7b4e81d0b19bd09e44d9685887e35be9e934019491de4
---

# GD-talad-001

## สถานะการยืนยัน

✅ **เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-028@v1)** ยืนยันเมื่อ 2026-09-23T12:08+07:00 — ตัวเลขชุดนี้ใช้ยันกับลูกค้าได้

คำนวณโดย `golden/CALC-talad-001@v1.mjs` เมื่อ 2026-09-23T12:07+07:00

## พิสูจน์

- [BR-talad-028@v1](../rules/BR-talad-028@v1.md) — ส่วนลดสมาชิกใช้ร่วมกับโปรโมชั่นทั่วไปได้ โดยคิดโปรโมชั่นทั่วไปก่อน แล้วลด % สมาชิกจากยอดที่เหลือ (คิดต่อกัน ไม่ใช่บวก % กัน)
- [CALC-talad-001@v1](../calculations/CALC-talad-001@v1.md) — line_net[i] = unit_price[i] × qty[i] − item_promo_discount[i]  ;  subtotal = Σ line_net[i]  ;  bill_discount = round(subtotal × bill_rate / 100)  ;  after_bill = subtotal − bill_discount  ;  member_discount = round(after_bill × member_rate / 100)  ;  net = after_bill − member_discount   (ยอดรายการหลังโปรระดับสินค้า → ส่วนลดทั้งบิล → ส่วนลดสมาชิก คิดต่อกัน ไม่บวก % กัน)

## ตาราง (8 แถว)

| items | bill_rate | member_rate | subtotal | bill_discount | after_bill | member_discount | net | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|
| [object Object],[object Object] | 0 | 0 | 100.00 | 0.00 | 100.00 | 0.00 | 100.00 | — |
| [object Object] | 0 | 5 | 90.00 | 0.00 | 90.00 | 4.50 | 85.50 | — |
| [object Object],[object Object] | 0 | 5 | 201.00 | 0.00 | 201.00 | 10.05 | 190.95 | — |
| [object Object],[object Object] | 10 | 5 | 201.00 | 20.10 | 180.90 | 9.05 | 171.85 | — |
| [object Object] | 0 | 15 | 19.75 | 0.00 | 19.75 | 2.96 | 16.79 | — |
| [object Object] | 0 | 100 | 45.00 | 0.00 | 45.00 | 45.00 | 0.00 | — |
| [object Object] | 100 | 50 | 45.00 | 45.00 | 0.00 | 0.00 | 0.00 | — |
| [object Object] | 10 | 5 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | — |

## หมายเหตุ
ไม่มี sample_data ในโปรเจกต์ — แถวทั้งหมดมาจาก boundary_behavior ของสัญญา + ตัวอย่างใน SRC-008 และ EX-talad-065 · item_promo_discount เป็นอินพุตที่ปัดแล้ว (มาจากสัญญาของโปรแต่ละแบบ ยังไม่มี)
