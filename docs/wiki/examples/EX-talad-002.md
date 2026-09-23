---
type: Example
title: alternate — รายการ ส้มสายน้ำผึ้ง เหลือจำนวน 2 · ยอดรวม 90 บาท · ไม่มีกล่
description: รายการ ส้มสายน้ำผึ้ง เหลือจำนวน 2 · ยอดรวม 90 บาท · ไม่มีกล่องยืนยัน
resource: ../rules/BR-talad-001@v1.md
tags: [talad, example, alternate]
id: EX-talad-002
status: draft
kind: alternate
proves: [BR-talad-001@v1]
has_ui: true
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:04cb0adb60ce49198e163508eeeeea4b4cfff8cdde92ff29001f609d5f68f2c9
---

# EX-talad-002

## กำหนดให้ (given)
ตะกร้าของสมชาย (ยังไม่ชำระเงิน) มี ส้มสายน้ำผึ้ง จำนวน 3 (45 บาท) · ยอดรวม 135 บาท

## เมื่อ (when)
กดปุ่ม "−" ที่รายการ ส้มสายน้ำผึ้ง 1 ครั้ง

## แล้ว (then)
รายการ ส้มสายน้ำผึ้ง เหลือจำนวน 2 · ยอดรวม 90 บาท · ไม่มีกล่องยืนยัน

## พิสูจน์กฎ

- [BR-talad-001@v1](../rules/BR-talad-001@v1.md) ✅ ปัจจุบัน — พนักงานเพิ่ม ลด หรือลบสินค้าในตะกร้าได้ก่อนชำระเงิน
