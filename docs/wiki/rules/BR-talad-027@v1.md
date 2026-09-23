---
type: Business Rule
title: ทุกจุดที่คำนวณส่วนลดแล้วได้เศษเกินหน่วยเงินที่ใช้ ให้ปัดทันทีที่จุดนั้นแบบ round
description: ทุกจุดที่คำนวณส่วนลดแล้วได้เศษเกินหน่วยเงินที่ใช้ ให้ปัดทันทีที่จุดนั้นแบบ round half up (ปัดครึ่งขึ้น)
resource: ../requirements/REQ-talad-005.md
tags: [talad, calculation]
id: BR-talad-027@v1
status: draft
belongs_to: REQ-talad-005
kind: calculation
is_current: true
test_design: [BVA]
constrained_by: CALC-talad-002@v1
proven_by: [EX-talad-093, EX-talad-091, EX-talad-014]
golden: [GD-talad-002]
provenance: [SRC-008]
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:f08df40a18d1f25b743253caa98fc35c3a0cc68b6871fdd4227f03c19ecb0bcd
---

# BR-talad-027@v1

## ข้อความของกฎ
ทุกจุดที่คำนวณส่วนลดแล้วได้เศษเกินหน่วยเงินที่ใช้ ให้ปัดทันทีที่จุดนั้นแบบ round half up (ปัดครึ่งขึ้น)

คำนวณตามสัญญา [CALC-talad-002@v1](../calculations/CALC-talad-002@v1.md)

## ที่มา

> "QB-calc-01: ส่วนลด % ให้ผลมีเศษเกินสตางค์ (เช่น ลด 15% ของ 19.75 = 2.9625) ระบบทำยังไง → a) ปัดทุกจุดตามมาตรฐาน round half up"
> — [SRC-008](../sources/SRC-008.md) หน้า — §—

## พิสูจน์โดย

- [EX-talad-014](../examples/EX-talad-014.md) — happy: ค้นหาสมหญิงที่หน้าขาย แสดงยอดซื้อสะสม 1,085.50 บาท (เพิ่ม 85.50 ไม่ใช่ 100)
- [EX-talad-091](../examples/EX-talad-091.md) — alternate: บิลแสดงส่วนลดโปรโมชั่น 9 บาท · ส่วนลดทั้งบิล 20.10 บาท · ส่วนลดสมาชิก 9.05 บาท · ยอดชำระ 171.85 บาท (คิดต่อกัน — ไม่ใช่ 170.85 บาทที่ได้จากบวก 10% + 5% เป็น 15%)
- [EX-talad-093](../examples/EX-talad-093.md) — boundary: บิลแสดงส่วนลดสมาชิก 2.96 บาท (15% ของ 19.75 = 2.9625 ปัดลง เพราะต่ำกว่าครึ่งสตางค์) · ยอดชำระ 16.79 บาท
- [GD-talad-002](../golden/GD-talad-002.md) — เลขเฉลย 9 แถว · ✅ เจ้าของงาน (ผู้ตอบในเซสชัน /req:golden BR-talad-027@v1) 2026-09-23

## ประวัติ

| เวอร์ชัน | มีผลตั้งแต่ | เหตุผล | change set |
|---|---|---|---|
| **BR-talad-027@v1** (หน้านี้) ✅ | — | ตั้งต้น | — |
