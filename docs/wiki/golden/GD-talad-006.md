---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-013@v1 · CALC-talad-006@v1
description: 14 แถว · ยังไม่มีใครเซ็น
resource: ../rules/BR-talad-013@v1.md
tags: [talad, golden]
id: GD-talad-006
status: draft
proves: [BR-talad-013@v1, CALC-talad-006@v1]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:53dd04ca11f829a690377df88040a7c75db71e2afa76fbd89c1e711f46f1dee4
---

# GD-talad-006

## สถานะการยืนยัน

🔴 **ยังไม่มีใครเซ็น** — เลขที่ออกจากสคริปต์เป็นข้อเสนอ ไม่ใช่คำตอบ จนกว่าจะมี `verified_by`

คำนวณโดย `golden/CALC-talad-006@v1.mjs` เมื่อ 2026-09-23T13:36+07:00

## พิสูจน์

- [BR-talad-013@v1](../rules/BR-talad-013@v1.md) — โปรโมชั่นแบบ ซื้อ a + b ลด y% — เจ้าของร้านตั้งจำนวน a และ b ต่อชุดได้ · นับชุดตามฝั่งที่ครบน้อยกว่า · ลด y% เฉพาะชิ้นที่อยู่ในชุดครบคู่ ชิ้นที่เกินคิดราคาปกติ · ส่วนลดคิดและปัดแยกต่อบรรทัด a และ b
- [CALC-talad-006@v1](../calculations/CALC-talad-006@v1.md) — sets = floor(min(qty_a / n_a, qty_b / n_b)) ; disc_a = round_half_up(sets × n_a × unit_price_a × y / 100, 0.01) ; disc_b = round_half_up(sets × n_b × unit_price_b × y / 100, 0.01)   — disc_a / disc_b ลงเป็น item_promo_discount ของบรรทัด a / b (CALC-talad-001) · ชิ้นที่ไม่อยู่ในชุดครบคู่ไม่ได้ลด · ผลรวม disc_a + disc_b เป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า

## ตาราง (14 แถว)

| n_a | n_b | y | qty_a | qty_b | unit_price_a | unit_price_b | price_at_add_b | sets | lines | promo_discount | total_gross | total_pay | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | 10 | 1 | 1 | 45.00 | 120.00 | — | 1 | [object Object],[object Object] | 16.50 | 165.00 | 148.50 | — |
| 2 | 1 | 10 | 2 | 1 | 45.00 | 120.00 | — | 1 | [object Object],[object Object] | 21.00 | 210.00 | 189.00 | — |
| 2 | 1 | 10 | 4 | 2 | 45.00 | 120.00 | — | 2 | [object Object],[object Object] | 42.00 | 420.00 | 378.00 | — |
| 1 | 1 | 10 | 3 | 1 | 45.00 | 120.00 | — | 1 | [object Object],[object Object] | 16.50 | 255.00 | 238.50 | — |
| 1 | 1 | 10 | 1 | 3 | 45.00 | 120.00 | — | 1 | [object Object],[object Object] | 16.50 | 405.00 | 388.50 | — |
| 2 | 1 | 10 | 1 | 1 | 45.00 | 120.00 | — | 0 | [object Object],[object Object] | 0.00 | 165.00 | 165.00 | — |
| 2 | 1 | 10 | 2 | 0 | 45.00 | 120.00 | — | 0 | [object Object],[object Object] | 0.00 | 90.00 | 90.00 | — |
| 1 | 1 | 10 | 1 | 1 | 45.00 | 130.00 | 120.00 | 1 | [object Object],[object Object] | 17.50 | 175.00 | 157.50 | — |
| 1 | 1 | 0 | 1 | 1 | 45.00 | 120.00 | — | 1 | [object Object],[object Object] | 0.00 | 165.00 | 165.00 | — |
| 1 | 1 | 100 | 2 | 1 | 45.00 | 120.00 | — | 1 | [object Object],[object Object] | 165.00 | 210.00 | 45.00 | — |
| 1 | 1 | 7 | 1 | 1 | 19.75 | 45.00 | — | 1 | [object Object],[object Object] | 4.53 | 64.75 | 60.22 | — |
| 1 | 1 | 2 | 1 | 1 | 19.75 | 24.25 | — | 1 | [object Object],[object Object] | 0.89 | 44.00 | 43.11 | — |
| 1 | 1 | 5 | 1 | 1 | 19.75 | 45.00 | — | 1 | [object Object],[object Object] | 3.24 | 64.75 | 61.51 | — |
| 1 | 1 | 7 | 3 | 3 | 19.75 | 45.00 | — | 3 | [object Object],[object Object] | 13.60 | 194.25 | 180.65 | — |

## หมายเหตุ
ไม่มี sample_data — แถวมาจาก boundary_behavior ทั้ง 5 ข้อ + กรณีที่เจ้าของงานตอบไว้ใน SRC-037 · note ของทุกแถวมีผลแบบปัดรวมทั้งชุดเป็นข้อมูลประกอบ ไม่ใช่คำตอบ · แถว 12 ใช้ราคาสมมติ 24.25 เพราะคู่ ลำไย + ส้ม ไม่มี y ไหนที่ปัดแยกกับปัดรวมต่างกัน · แถว 14 บรรทัดลำไย (4.15) ตรงกับ GD-talad-003 แถว 1
