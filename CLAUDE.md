# TaladMarget — ระบบขายหน้าร้าน (POS) ร้านเดี่ยว

aeon pipeline: req → design → mock → **dev** → qa · state อยู่ที่ `.aeon/` · stack ที่คนยืนยันอยู่ใน `.aeon/dev/features.json` (เขียนโดย `/dev:stack` เท่านั้น ห้ามแก้มือ)

## Commands

```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-init.ps1    # ตรวจ SDK · restore · npm install · ยก PostgreSQL ใน compose
powershell -ExecutionPolicy Bypass -File scripts/dev-test.ps1    # unit test ทั้งสองแอป · exit ≠ 0 เมื่อแดง · exit 2 เมื่อยังไม่มีอะไรให้รัน (ไม่ใช่ผ่าน)
powershell -ExecutionPolicy Bypass -File scripts/dev-build.ps1   # Release build ทั้งสองแอป
```

เครื่องนี้ปิดการรัน script (execution policy) — เรียกผ่าน `-ExecutionPolicy Bypass` เสมอ · สคริปต์เป็น ASCII ล้วน เพราะ PowerShell 5.1 อ่านไฟล์ UTF-8 ไม่มี BOM เป็น ANSI แล้ว em-dash กลายเป็นเครื่องหมายคำพูด

## Stack

| ส่วน | เทคโนโลยี | ที่อยู่ | ที่มา |
|---|---|---|---|
| api | .NET 10 (ASP.NET Core) | `src/api` | NFR-talad-003 |
| web | Next.js 16 (React) · Tailwind CSS 4 · daisyUI 5 | `src/web` | asked · ⚠️ ขัด NFR-talad-003 (ดูด้านล่าง) |
| database | PostgreSQL 18 · `talad_db` | compose | NFR-talad-003 |
| hosting | เครื่องในร้าน (on-premise) ผ่าน LAN | — | DEC-001 |
| รูปสินค้า | ไฟล์บนดิสก์เซิร์ฟเวอร์ · DB เก็บแค่ path | — | DEC-002 |

ไม่มี cache · ไม่มี queue · ไม่มีโค้ดร่วมระหว่างแอป (สัญญาคือ OpenAPI ของ api)

Skills: api → `csharp-lsp` (+ `efcore-patterns` — ยังไม่ได้ติดตั้ง) · web → เขียนโค้ด Next.js ใช้ `vercel-react-best-practices` (ระดับโปรเจกต์ `.claude/skills/` · ติดตั้งซ้ำด้วย `npx skills add vercel-labs/agent-skills --skill vercel-react-best-practices --agent claude-code --copy -y`) · ออกแบบหน้า HTML ใช้ `frontend-design`

## Architecture

- **api** — Clean Architecture สี่ project ใต้ `src/api/src/`:
  `Talad.Domain` ← `Talad.Application` ← `Talad.Infrastructure` ← `Talad.Api` · tests ที่ `src/api/tests`
  Domain อ้างใครไม่ได้ · Application อ้างได้แค่ Domain · Api คือที่เดียวที่ต่อ Infrastructure เข้า DI
- **web** — `src/web/src/lib` ← `components` ← `app` (App Router) · tests ที่ `src/web/tests`
- **ใครต่ออะไรได้**: api → database เท่านั้น · web → api เท่านั้น — **web ห้ามแตะฐานข้อมูลตรง**
  ไม่มีเครื่องมือไหนอ่าน import หรือ connection string จริง คนอ่าน diff คือคนจับ

ตั้งค่าตอนรัน: api ต้องมี `Jwt__SigningKey` (≥ 32 ไบต์ · ไม่ commit) และ `ConnectionStrings__Talad` · web เรียก api ที่ `TALAD_API_URL` (ค่าเริ่มต้น `http://localhost:5010`) · session ของ web คือ JWT ใน cookie `talad_session` (httpOnly)

## Conventions

- ทุก endpoint ยกเว้น login ต้องตอบ 401 เมื่อ JWT หาย · หมดอายุ · ลายเซ็นผิด (NFR-talad-005)
- ตารางค้นหา (สต็อก · ประวัติขาย · รายงาน) แบ่งหน้าที่เซิร์ฟเวอร์ 20 แถว · p95 ≤ 2 วินาทีที่ ~550,000 บิล (NFR-talad-006..008) — index ให้ครบตั้งแต่ migration แรก
- ร้านเดียว — ไม่มีคอลัมน์หรือหน้าจอเรื่องสาขา (NFR-talad-004)
- ทุกหน้าใช้ได้ครบที่ความกว้าง ≥ 768px โดยไม่ต้องเลื่อนแนวนอน · เมนูหลักอยู่ด้านซ้าย (NFR-talad-001, 002)
- สีและขนาดมาจาก token ของ theme (`.aeon/mockup/theme.json`) — map เข้า theme ของ daisyUI ห้ามคิดสีเอง
- `data-testid` กับชื่อฟิลด์มาจาก wireframe และคำประกาศของ design ไม่ใช่จาก mockup L2

## Mistakes already made — อย่าทำซ้ำ

- **`/dev:stack --intake` ไม่ได้จับ NFR-talad-003 เป็นคำตอบ** เลยถาม framework กับ database ซ้ำ ทั้งที่ NFR ล็อกไว้แล้ว — ก่อนถามเรื่อง stack ให้อ่าน `.aeon/design/nfr.json` เองด้วย
- **เสนอเวอร์ชันจากความจำ** (Next.js 15 ทั้งที่ล่าสุดคือ 16 · daisyUI v4 คู่กับ Tailwind รุ่นไหน) — ตรวจจาก registry ก่อนเสนอทุกครั้ง (`npm view <pkg> dist-tags` · release index ของ .NET · postgresql.org/versions.json)
- **NFR-talad-003 เขียน "Tailwind v5 + daisy v4"** — Tailwind v5 ไม่มีจริง (latest 4.3.3 ณ 2026-09-23) และ daisyUI 4 สร้างคู่กับ Tailwind 3 · ทีมเลือก Tailwind 4 + daisyUI 5 แล้วต้องยกแก้ผ่าน `/req:change` → `/design:change` ให้ลูกค้าเซ็นใหม่ · จนกว่าจะแก้ NFR-003 ข้อนี้นับเป็นไม่ผ่าน
- **บันทึก skill ที่ไม่ได้ติดตั้ง** (`dotnet-dev`) ทำให้ `/dev:build` หยุดที่รั้วข้อ 5 — ก่อนบันทึก skill ให้เช็กว่าติดตั้งอยู่จริง · api เปลี่ยนเป็น `csharp-lsp` เมื่อ 2026-09-23
- **Next.js 16 ไม่ใช่ Next ที่จำได้** — อ่าน `src/web/node_modules/next/dist/docs/` ก่อนเขียน · `middleware.ts` เปลี่ยนชื่อเป็น `src/proxy.ts` (export `proxy`) · `searchParams`/`cookies()` เป็น async
- ไฟล์ที่เครื่องมือบังคับให้อยู่รากแอป (`Talad.slnx` · `package.json` · config · `src/proxy.ts`) ต้องประกาศใน `structure.apps[].manifests[]` ผ่าน `/dev:stack` ก่อน `build.mjs --write` ไม่งั้นถูกปฏิเสธ (DV15)
- vitest 5 ต้องใช้ `@types/node` ≥ 22 — Node ของเครื่องนี้คือ 24
- `sitemap.json` ไม่มี `apps[]` roster — การแยก api/web ยังเป็นคำตอบที่ถาม ไม่ใช่การ์ดที่ลูกค้าเซ็น
