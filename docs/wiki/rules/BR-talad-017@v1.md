---
type: Business Rule
title: ทุกรายงาน Export เป็น Excel ได้ · ไฟล์มีแถวเดียวกับที่รายงานแสดงบนจอในช่วงที่เลื
description: ทุกรายงาน Export เป็น Excel ได้ · ไฟล์มีแถวเดียวกับที่รายงานแสดงบนจอในช่วงที่เลือก · ยอดเงินในไฟล์เป็นเซลล์ตัวเลข 2 ตำแหน่ง ค่าตรงกับบนจอทุกสตางค์ · รายงานที่ไม่มีข้อมูล Export ได้เป็นไฟล์ที่มีแต่หัวตาราง
resource: ../requirements/REQ-talad-007.md
tags: [talad, constraint]
id: BR-talad-017@v1
status: draft
belongs_to: REQ-talad-007
kind: constraint
is_current: true
test_design: [EP]
proven_by: [EX-talad-106, EX-talad-107, EX-talad-108]
golden: []
provenance: [SRC-001, SRC-034, SRC-034]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:27ba2879c720d2c4f43b67c57ea290c00817bef7233fe4d5679c24ddea2a5764
---

# BR-talad-017@v1

## ข้อความของกฎ
ทุกรายงาน Export เป็น Excel ได้ · ไฟล์มีแถวเดียวกับที่รายงานแสดงบนจอในช่วงที่เลือก · ยอดเงินในไฟล์เป็นเซลล์ตัวเลข 2 ตำแหน่ง ค่าตรงกับบนจอทุกสตางค์ · รายงานที่ไม่มีข้อมูล Export ได้เป็นไฟล์ที่มีแต่หัวตาราง

## ที่มา

> "7. รายงาน (Reports) ทุกรายงาน Export Excel ได้"
> — [SRC-001](../sources/SRC-001.md) หน้า — §—

> "ระหว่าง /req:example BR-talad-017@v1: กด Export Excel ตอนรายงานไม่มีข้อมูล → a) ได้ไฟล์มีแต่หัวตาราง — ดาวน์โหลดได้ปกติ ไม่มีแถวข้อมูล"
> — [SRC-034](../sources/SRC-034.md) หน้า — §—

> "ระหว่าง /req:example BR-talad-017@v1: ยอดเงินในไฟล์ Excel เป็นแบบไหน → a) ตัวเลขจริง 2 ตำแหน่ง — เซลล์เป็นตัวเลข เอาไป SUM ต่อได้ ค่าตรงกับบนจอทุกสตางค์"
> — [SRC-034](../sources/SRC-034.md) หน้า — §—

## พิสูจน์โดย

- [EX-talad-106](../examples/EX-talad-106.md) — happy: ไฟล์ .xlsx มี 3 แถวข้อมูลตรงกับบนจอ · เซลล์ยอดขายเป็นตัวเลข 280.95 · 120.00 · 45.00 (SUM ได้ 446.95)
- [EX-talad-107](../examples/EX-talad-107.md) — boundary: ดาวน์โหลดไฟล์ .xlsx ได้ตามปกติ · ไฟล์มีแต่หัวตาราง ไม่มีแถวข้อมูล
- [EX-talad-108](../examples/EX-talad-108.md) — alternate: เซลล์ยอดขายในไฟล์เป็นตัวเลข 120.00 ตรงกับบนจอ (ไม่นับบิลที่ยกเลิก)

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล | change set |
|---|---|---|---|
| **BR-talad-017@v1** (หน้านี้) ✅ | — | ตั้งต้น | — |
