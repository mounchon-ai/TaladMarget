---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-011@v1 · CALC-talad-004@v1
description: 15 แถว · ยืนยันแล้ว
resource: ../rules/BR-talad-011@v1.md
tags: [talad, golden]
id: GD-talad-004
status: validated
proves: [BR-talad-011@v1, CALC-talad-004@v1]
verified_by: เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-011@v1)
verified_at: 2026-09-23T13:19+07:00
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:9d27f7800d3c13b3cf71fe51880c62fc0d21482549477a69768720850aaac680
---

# GD-talad-004

## สถานะการยืนยัน

✅ **เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-011@v1)** ยืนยันเมื่อ 2026-09-23T13:19+07:00 — ตัวเลขชุดนี้ใช้ยันกับลูกค้าได้

คำนวณโดย `golden/CALC-talad-004@v1.mjs` เมื่อ 2026-09-23T13:17+07:00

## พิสูจน์

- [BR-talad-011@v1](../rules/BR-talad-011@v1.md) — โปรโมชั่นแบบ ซื้อ x แถม y — ของแถมอาจเป็นสินค้าเดียวกันหรือคนละสินค้า · พนักงานหยิบของแถมใส่ตะกร้าเอง ระบบคิดเป็นฟรีเมื่อครบเงื่อนไข (ไม่หยิบก็ไม่ได้แถม) · แถมซ้ำตามจำนวนชุดที่ครบ · ของแถมแสดงราคาปกติพร้อมส่วนลดเท่าราคา · ของแถมหมดสต็อก สินค้าที่ซื้อยังขายได้ราคาปกติ
- [CALC-talad-004@v1](../calculations/CALC-talad-004@v1.md) — คนละสินค้า (buy ≠ free): entitled = floor(qty_buy / x) × y ; free_qty = min(qty_free_in_cart, entitled) ; free_discount = free_qty × unit_price_free   ·   สินค้าเดียวกัน (buy = free): free_qty = floor(qty_in_cart / (x + y)) × y ; free_discount = free_qty × unit_price   — free_discount ลงเป็น item_promo_discount ของบรรทัดของแถม (CALC-talad-001) และเป็นค่าที่ BR-talad-029 ใช้เทียบว่าโปรไหนลดมากกว่า

## ตาราง (15 แถว)

| kind | x | y | qty_buy | unit_price_buy | qty_free_in_cart | unit_price_free | price_at_add_free | qty_in_cart | unit_price | entitled | free_qty | charged_free_item_qty | buy_line | free_line | free_discount | charged_qty | line | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| different_item | 2 | 1 | 2 | 45.00 | 1 | 120.00 | — | — | — | 1 | 1 | 0 | [object Object] | [object Object] | 120.00 | — | — | — |
| different_item | 2 | 1 | 5 | 45.00 | 2 | 120.00 | — | — | — | 2 | 2 | 0 | [object Object] | [object Object] | 240.00 | — | — | — |
| different_item | 2 | 1 | 1 | 45.00 | 1 | 120.00 | — | — | — | 0 | 0 | 1 | [object Object] | [object Object] | 0.00 | — | — | — |
| different_item | 2 | 1 | 2 | 45.00 | 3 | 120.00 | — | — | — | 1 | 1 | 2 | [object Object] | [object Object] | 120.00 | — | — | — |
| different_item | 2 | 1 | 4 | 45.00 | 1 | 120.00 | — | — | — | 2 | 1 | 0 | [object Object] | [object Object] | 120.00 | — | — | — |
| different_item | 2 | 1 | 2 | 45.00 | 1 | 130.00 | 120.00 | — | — | 1 | 1 | 0 | [object Object] | [object Object] | 130.00 | — | — | — |
| different_item | 2 | 1 | 2 | 45.00 | 0 | 120.00 | — | — | — | 1 | 0 | 0 | [object Object] | [object Object] | 0.00 | — | — | — |
| different_item | 3 | 2 | 7 | 45.00 | 4 | 19.75 | — | — | — | 4 | 4 | 0 | [object Object] | [object Object] | 79.00 | — | — | — |
| different_item | 3 | 2 | 3 | 45.00 | 1 | 19.75 | — | — | — | 2 | 1 | 0 | [object Object] | [object Object] | 19.75 | — | — | — |
| same_item | 2 | 1 | — | — | — | — | — | 2 | 45.00 | — | 0 | — | — | — | 0.00 | 2 | [object Object] | — |
| same_item | 2 | 1 | — | — | — | — | — | 3 | 45.00 | — | 1 | — | — | — | 45.00 | 2 | [object Object] | — |
| same_item | 2 | 1 | — | — | — | — | — | 5 | 45.00 | — | 1 | — | — | — | 45.00 | 4 | [object Object] | — |
| same_item | 2 | 1 | — | — | — | — | — | 6 | 45.00 | — | 2 | — | — | — | 90.00 | 4 | [object Object] | — |
| same_item | 3 | 2 | — | — | — | — | — | 10 | 19.75 | — | 4 | — | — | — | 79.00 | 6 | [object Object] | — |
| same_item | 3 | 2 | — | — | — | — | — | 4 | 19.75 | — | 0 | — | — | — | 0.00 | 4 | [object Object] | — |

## หมายเหตุ
ไม่มี sample_data — แถวมาจาก boundary_behavior ทั้ง 6 ข้อ + กรณีที่เจ้าของงานตอบไว้ใน SRC-035 L2/L5 + EX-talad-068 · ไม่มีจุดปัด (HALF_UP ประกาศไว้แต่ไม่ถูกใช้) · แถว 9 กับ 15 วางคู่กันเพื่อให้เจ้าของงานตัดสินเรื่องชุดไม่ครบ (คนละสินค้าได้ฟรีเท่าที่หยิบ · สินค้าเดียวกันต้องครบ x+y) · สัญญาใช้ unit_price ในสูตรสินค้าเดียวกันแต่ inputs[] ไม่ได้ประกาศ · เจ้าของงานยืนยันว่าแถว 9/15 (ชุดไม่ครบ: คนละสินค้าฟรีเท่าที่หยิบ · สินค้าเดียวกันต้องครบ x+y) เป็นพฤติกรรมที่ตั้งใจ
