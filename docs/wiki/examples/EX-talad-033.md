---
type: Example
title: happy — เข้าสู่หน้าขายในชื่อ somchai
description: เข้าสู่หน้าขายในชื่อ somchai
resource: ../rules/BR-talad-005@v1.md
tags: [talad, example, happy]
id: EX-talad-033
status: draft
kind: happy
proves: [BR-talad-005@v1]
has_ui: true
timestamp: 2026-09-23T11:00:00+07:00
spec_hash: sha256:43c8c039cc702c17c25cbab4fcd985933d969d5d05beb662250e14babd73b37d
---

# EX-talad-033

## กำหนดให้ (given)
มีบัญชีพนักงานขาย ชื่อผู้ใช้ "somchai" รหัสผ่าน "Somchai#2569" · อยู่ที่หน้าล็อกอิน

## เมื่อ (when)
กรอกชื่อผู้ใช้ "somchai" รหัสผ่าน "Somchai#2569" แล้วกดเข้าสู่ระบบ

## แล้ว (then)
เข้าสู่หน้าขายในชื่อ somchai

## พิสูจน์กฎ

- [BR-talad-005@v1](../rules/BR-talad-005@v1.md) ✅ ปัจจุบัน — ผู้ใช้ต้องล็อกอินด้วยชื่อผู้ใช้และรหัสผ่านก่อนเข้าใช้งานหน้าขาย · ล็อกอินไม่สำเร็จ ระบบบอกแยกว่าไม่พบชื่อผู้ใช้ หรือรหัสผ่านไม่ถูกต้อง · เปิดหน้าขายโดยยังไม่ล็อกอิน ระบบพาไปหน้าล็อกอิน และล็อกอินแล้วกลับมาหน้าขาย
