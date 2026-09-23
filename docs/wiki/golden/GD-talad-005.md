---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-012@v1 · CALC-talad-005@v1
description: 18 แถว · ยืนยันแล้ว
resource: ../rules/BR-talad-012@v1.md
tags: [talad, golden]
id: GD-talad-005
status: validated
proves: [BR-talad-012@v1, CALC-talad-005@v1]
verified_by: เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-012@v1)
verified_at: 2026-09-23T13:30+07:00
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:f58e9fb9edb6d9fe3df1681b376edd86c4cf82a5c40ca61990fd2dcecf00a623
---

# GD-talad-005

## สถานะการยืนยัน

✅ **เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-012@v1)** ยืนยันเมื่อ 2026-09-23T13:30+07:00 — ตัวเลขชุดนี้ใช้ยันกับลูกค้าได้

คำนวณโดย `golden/CALC-talad-005@v1.mjs` เมื่อ 2026-09-23T13:29+07:00

## พิสูจน์

- [BR-talad-012@v1](../rules/BR-talad-012@v1.md) — โปรโมชั่นแบบ ซื้อ a + b แถม y — เจ้าของร้านตั้งจำนวน a และ b ต่อชุดได้ · แถมซ้ำตามจำนวนชุดที่ครบคู่ (นับตามฝั่งที่ครบน้อยกว่า) · ของแถมเป็นสินค้าอื่น หรือเป็น a / b เองก็ได้ (ชิ้นที่ฟรีไม่นับเป็นชิ้นที่ซื้อ) · พนักงานหยิบของแถมใส่ตะกร้าเอง ระบบคิดเป็นฟรีเมื่อครบเงื่อนไข · ของแถมแสดงราคาปกติพร้อมส่วนลดเท่าราคา · ของแถมหมดสต็อก a และ b ยังขายได้ราคาปกติ
- [CALC-talad-005@v1](../calculations/CALC-talad-005@v1.md) — ของแถมเป็นสินค้าอื่น (y ≠ a, b): sets = floor(min(qty_a / n_a, qty_b / n_b)) ; entitled = sets × y ; free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free   ·   ของแถมเป็น a (ถ้าเป็น b สลับ a กับ b): sets = floor(min(qty_a / (n_a + y), qty_b / n_b)) ; free_qty = sets × y ; free_discount = free_qty × unit_price_a   — free_discount ลงเป็น item_promo_discount ของบรรทัดของแถม (CALC-talad-001) และเป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า

## ตาราง (18 แถว)

| kind | n_a | n_b | y | qty_a | qty_b | qty_free_in_cart | unit_price_a | unit_price_b | unit_price_free | price_at_add_free | sets | entitled | free_qty | charged_free_item_qty | free_discount | lines | total_gross | promo_discount | total_pay | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| free_other | 1 | 1 | 1 | 1 | 1 | 1 | 45.00 | 120.00 | 19.75 | — | 1 | 1 | 1 | 0 | 19.75 | [object Object],[object Object],[object Object] | 184.75 | 19.75 | 165.00 | — |
| free_other | 2 | 1 | 1 | 2 | 1 | 1 | 45.00 | 120.00 | 19.75 | — | 1 | 1 | 1 | 0 | 19.75 | [object Object],[object Object],[object Object] | 229.75 | 19.75 | 210.00 | — |
| free_other | 2 | 1 | 1 | 4 | 2 | 2 | 45.00 | 120.00 | 19.75 | — | 2 | 2 | 2 | 0 | 39.50 | [object Object],[object Object],[object Object] | 459.50 | 39.50 | 420.00 | — |
| free_other | 1 | 1 | 2 | 2 | 2 | 4 | 45.00 | 120.00 | 19.75 | — | 2 | 4 | 4 | 0 | 79.00 | [object Object],[object Object],[object Object] | 409.00 | 79.00 | 330.00 | — |
| free_other | 2 | 1 | 1 | 1 | 1 | 1 | 45.00 | 120.00 | 19.75 | — | 0 | 0 | 0 | 1 | 0.00 | [object Object],[object Object],[object Object] | 184.75 | 0.00 | 184.75 | — |
| free_other | 2 | 1 | 1 | 2 | 0 | 1 | 45.00 | 120.00 | 19.75 | — | 0 | 0 | 0 | 1 | 0.00 | [object Object],[object Object],[object Object] | 109.75 | 0.00 | 109.75 | — |
| free_other | 1 | 1 | 1 | 3 | 1 | 1 | 45.00 | 120.00 | 19.75 | — | 1 | 1 | 1 | 0 | 19.75 | [object Object],[object Object],[object Object] | 274.75 | 19.75 | 255.00 | — |
| free_other | 1 | 1 | 1 | 1 | 1 | 3 | 45.00 | 120.00 | 19.75 | — | 1 | 1 | 1 | 2 | 19.75 | [object Object],[object Object],[object Object] | 224.25 | 19.75 | 204.50 | — |
| free_other | 1 | 1 | 1 | 2 | 2 | 1 | 45.00 | 120.00 | 19.75 | — | 2 | 2 | 1 | 0 | 19.75 | [object Object],[object Object],[object Object] | 349.75 | 19.75 | 330.00 | — |
| free_other | 1 | 1 | 1 | 1 | 1 | 1 | 45.00 | 120.00 | 22.00 | 19.75 | 1 | 1 | 1 | 0 | 22.00 | [object Object],[object Object],[object Object] | 187.00 | 22.00 | 165.00 | — |
| free_other | 1 | 1 | 1 | 1 | 1 | 0 | 45.00 | 120.00 | 19.75 | — | 1 | 1 | 0 | 0 | 0.00 | [object Object],[object Object],[object Object] | 165.00 | 0.00 | 165.00 | — |
| free_is_a | 2 | 1 | 1 | 3 | 1 | — | 45.00 | 120.00 | — | — | 1 | — | 1 | — | 45.00 | [object Object],[object Object] | 255.00 | 45.00 | 210.00 | — |
| free_is_a | 2 | 1 | 1 | 2 | 1 | — | 45.00 | 120.00 | — | — | 0 | — | 0 | — | 0.00 | [object Object],[object Object] | 210.00 | 0.00 | 210.00 | — |
| free_is_a | 2 | 1 | 1 | 6 | 2 | — | 45.00 | 120.00 | — | — | 2 | — | 2 | — | 90.00 | [object Object],[object Object] | 510.00 | 90.00 | 420.00 | — |
| free_is_a | 2 | 1 | 1 | 6 | 1 | — | 45.00 | 120.00 | — | — | 1 | — | 1 | — | 45.00 | [object Object],[object Object] | 390.00 | 45.00 | 345.00 | — |
| free_is_a | 2 | 1 | 1 | 5 | 2 | — | 45.00 | 120.00 | — | — | 1 | — | 1 | — | 45.00 | [object Object],[object Object] | 465.00 | 45.00 | 420.00 | — |
| free_is_b | 1 | 1 | 1 | 1 | 2 | — | 45.00 | 120.00 | — | — | 1 | — | 1 | — | 120.00 | [object Object],[object Object] | 285.00 | 120.00 | 165.00 | — |
| free_is_b | 1 | 1 | 1 | 1 | 1 | — | 45.00 | 120.00 | — | — | 0 | — | 0 | — | 0.00 | [object Object],[object Object] | 165.00 | 0.00 | 165.00 | — |

## หมายเหตุ
ไม่มี sample_data — แถวมาจาก boundary_behavior ทั้ง 6 ข้อ + กรณีที่เจ้าของงานตอบไว้ใน SRC-036 · ไม่มีจุดปัด (HALF_UP ประกาศไว้แต่ไม่ถูกใช้) · ของแถมเป็น b คำนวณด้วยสูตรของแถมเป็น a โดยสลับ a กับ b ตามสัญญา · เจ้าของงานยืนยันทั้ง 18 แถว รวมข้อสังเกตแถว 16/14 (ส้ม 5 กับ 6 จ่ายเท่ากัน) และแถว 13/2 (ของแถมเป็น a ต้องครบ n_a + y)
