---
type: Golden Dataset
title: เลขเฉลยของ BR-talad-029@v1 · CALC-talad-008@v1
description: 14 แถว · ยืนยันแล้ว
resource: ../rules/BR-talad-029@v1.md
tags: [talad, golden]
id: GD-talad-008
status: validated
proves: [BR-talad-029@v1, CALC-talad-008@v1]
verified_by: เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-029@v1)
verified_at: 2026-09-23T15:50+07:00
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:0f52b3629dd5e9cea3d6ea358c9519c85dbe6221cfb41c67982cd47b209e4cdc
---

# GD-talad-008

## สถานะการยืนยัน

✅ **เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-029@v1)** ยืนยันเมื่อ 2026-09-23T15:50+07:00 — ตัวเลขชุดนี้ใช้ยันกับลูกค้าได้

คำนวณโดย `golden/CALC-talad-008@v1.mjs` เมื่อ 2026-09-23T15:45+07:00

## พิสูจน์

- [BR-talad-029@v1](../rules/BR-talad-029@v1.md) — สินค้าแต่ละรายการ (บรรทัด) ในตะกร้าใช้โปรโมชั่นระดับสินค้าได้เพียงโปรเดียว ทั้งบรรทัด — ชิ้นที่ไม่อยู่ในชุดของโปรนั้นคิดราคาปกติ ไม่เข้าโปรอื่น · ถ้าเข้าเงื่อนไขหลายโปร ระบบเลือกโปรที่ลูกค้าได้ลดมากที่สุด (ส่วนลดเป็นบาทหลังปัด) ให้อัตโนมัติ ไล่ทีละโปร บรรทัดที่ถูกใช้แล้วหลุดจากโปรอื่น · ถ้าโปรที่ลดมากที่สุดลดเท่ากันพอดี ให้พนักงานเลือก · ส่วนลดทั้งบิลคิดทีหลังจากยอดที่เหลือหลังหักโปรระดับสินค้าแล้ว
- [CALC-talad-008@v1](../calculations/CALC-talad-008@v1.md) — free = บรรทัดทั้งหมดในตะกร้า ; applied = [] ; loop { cand = { (p, disc_p, lines_p) \| p ∈ โปรระดับสินค้าที่เปิดอยู่ , disc_p = ส่วนลดรวมของ p ที่คิดจากบรรทัดใน free ตาม CALC ของ p (003–007 · ปัดแล้ว) , disc_p > 0 } ; if cand ว่าง → break ; top = { c ∈ cand \| c.disc = max(cand.disc) } ; pick = top ถ้ามีตัวเดียว · พนักงานเลือกจาก top ถ้ามีหลายตัว ; applied += pick ; free −= pick.lines (ทั้งบรรทัด) }   — item_promo_discount ของแต่ละบรรทัด (CALC-talad-001) มาจาก pick ที่ใช้บรรทัดนั้น · บรรทัดที่เหลือใน free ได้ 0 · ส่วนลดทั้งบิลคิดทีหลังตาม CALC-talad-001/003

## ตาราง (14 แถว)

| cart | promos | staff_choice | bill_rate | steps | lines | promo_discount | needs_staff_choice | bill | มาจากแถวไหน |
|---|---|---|---|---|---|---|---|---|---|
| [object Object] |  | — | — |  | [object Object] | 0.00 | — | — | — |
| [object Object] | [object Object] | — | — | [object Object] | [object Object] | 27.00 | — | — | — |
| [object Object] | [object Object],[object Object] | — | — | [object Object] | [object Object] | 27.00 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 6.00 | — | — | — |
| [object Object] | [object Object],[object Object] | — | — |  | — | — | ส้ม ลด 10%,ซื้อส้ม 3 ชิ้น ลด 10% | — | — |
| [object Object] | [object Object],[object Object] | A | — | [object Object] | [object Object] | 13.50 | — | — | — |
| [object Object] | [object Object],[object Object] | B | — | [object Object] | [object Object] | 13.50 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 7.12 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 27.00 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 120.00 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 16.50 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 21.00 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object],[object Object] | — | — | [object Object] | [object Object],[object Object] | 8.25 | — | — | — |
| [object Object],[object Object] | [object Object],[object Object] | — | 5 | [object Object] | [object Object],[object Object] | 27.00 | — | [object Object] | — |

## หมายเหตุ
ไม่มี sample_data — แถวมาจาก boundary_behavior ทั้ง 7 ข้อ + ตัวอย่างโปรชนของเจ้าของงาน (SRC-040 ①c ①d) + ส่วนลดทั้งบิลที่คิดทีหลัง · ส่วนลดของแต่ละโปรได้จากการเรียกสคริปต์ golden ของ CALC-talad-003 ถึง 007 ไม่ได้คิดซ้ำ · แถว 5 ไม่มีคำตอบเป็นตัวเลข — ระบบต้องให้พนักงานเลือก (แถว 6–7 คือผลของแต่ละทางเลือก) · แถว 8 และ 13 ค้นด้วยสคริปต์ · แถว 13 แสดงว่าไล่ทีละโปรได้ 8.25 ขณะที่ชุดโปรที่ดีที่สุดได้ 10.50 — เป็นผลของทางเลือก ①c ของเจ้าของงาน ไม่ใช่ข้อผิดพลาด · note ทุกแถวมีค่าชุดโปรที่ไม่ทับกันซึ่งลดได้มากสุดเป็นข้อมูลประกอบ ไม่ใช่คำตอบ
