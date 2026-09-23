---
type: Example
title: happy — บิลแสดงผู้ขายเป็น สมชาย (somchai)
description: บิลแสดงผู้ขายเป็น สมชาย (somchai)
resource: ../rules/BR-talad-006@v1.md
tags: [talad, example, happy]
id: EX-talad-037
status: draft
kind: happy
proves: [BR-talad-006@v1]
has_ui: true
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:63283543292fdee0bb7125f33addae3394530df91af95da1a7990e6d2fc0b9ff
---

# EX-talad-037

## กำหนดให้ (given)
พนักงานขายสมชาย (somchai) เปิดตะกร้า มี ส้มสายน้ำผึ้ง จำนวน 2 · ยอด 90 บาท

## เมื่อ (when)
สมชายกดชำระเงินจนสำเร็จ แล้วเจ้าของร้านเปิดบิลนี้ในประวัติการขาย

## แล้ว (then)
บิลแสดงผู้ขายเป็น สมชาย (somchai)

## พิสูจน์กฎ

- [BR-talad-006@v1](../rules/BR-talad-006@v1.md) ✅ ปัจจุบัน — ทุกบิลขายบันทึกผู้ขาย คือผู้ใช้ที่เปิดตะกร้าและกดชำระเงินบิลนั้น — เป็นได้ทั้งพนักงานขายและเจ้าของร้าน
